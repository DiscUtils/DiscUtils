//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using DiscUtils.CoreCompat;

namespace DiscUtils.Iscsi
{
    /// <summary>
    /// Represents a connection to a particular Target.
    /// </summary>
    public sealed class Session : IDisposable
    {
        private static int _nextInitiatorSessionId = new Random().Next();

        private readonly IList<TargetAddress> _addresses;

        /// <summary>
        /// The set of all 'parameters' we've negotiated.
        /// </summary>
        private readonly Dictionary<string, string> _negotiatedParameters;

        private ushort _nextConnectionId;

        internal Session(SessionType type, string targetName, params TargetAddress[] addresses)
            : this(type, targetName, null, null, addresses) {}

        internal Session(SessionType type, string targetName, string userName, string password, IList<TargetAddress> addresses)
        {
            InitiatorSessionId = (uint)Interlocked.Increment(ref _nextInitiatorSessionId);
            _addresses = addresses;

            SessionType = type;
            TargetName = targetName;

            CommandSequenceNumber = 1;
            CurrentTaskTag = 1;

            // Default negotiated values...
            MaxConnections = 1;
            InitialR2T = true;
            ImmediateData = true;
            MaxBurstLength = 262144;
            FirstBurstLength = 65536;
            DefaultTime2Wait = 0;
            DefaultTime2Retain = 60;
            MaxOutstandingR2T = 1;
            DataPDUInOrder = true;
            DataSequenceInOrder = true;

            _negotiatedParameters = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(userName))
            {
                ActiveConnection = new Connection(this, _addresses[0], new Authenticator[] { new NullAuthenticator() });
            }
            else
            {
                ActiveConnection = new Connection(this, _addresses[0], new Authenticator[] { new NullAuthenticator(), new ChapAuthenticator(userName, password) });
            }
        }

        internal Connection ActiveConnection { get; private set; }

        internal uint CommandSequenceNumber { get; private set; }

        internal uint CurrentTaskTag { get; private set; }

        internal uint InitiatorSessionId { get; }

        internal ushort TargetSessionId { get; set; }

        /// <summary>
        /// Disposes of this instance, closing the session with the Target.
        /// </summary>
        public void Dispose()
        {
            if (ActiveConnection != null)
            {
                ActiveConnection.Close(LogoutReason.CloseSession);
            }

            ActiveConnection = null;
        }

        /// <summary>
        /// Enumerates all of the Targets.
        /// </summary>
        /// <returns>The list of Targets.</returns>
        /// <remarks>In practice, for an established session, this just returns details of
        /// the connected Target.</remarks>
        public TargetInfo[] EnumerateTargets()
        {
            return ActiveConnection.EnumerateTargets();
        }

        /// <summary>
        /// Gets information about the LUNs available from the Target.
        /// </summary>
        /// <returns>The LUNs available.</returns>
        public LunInfo[] GetLuns()
        {
            ScsiReportLunsCommand cmd = new ScsiReportLunsCommand(ScsiReportLunsCommand.InitialResponseSize);

            ScsiReportLunsResponse resp = Send<ScsiReportLunsResponse>(cmd, null, 0, 0, ScsiReportLunsCommand.InitialResponseSize);

            if (resp.Truncated)
            {
                cmd = new ScsiReportLunsCommand(resp.NeededDataLength);
                resp = Send<ScsiReportLunsResponse>(cmd, null, 0, 0, (int)resp.NeededDataLength);
            }

            if (resp.Truncated)
            {
                throw new InvalidProtocolException("Truncated response");
            }

            LunInfo[] result = new LunInfo[resp.Luns.Count];
            for (int i = 0; i < resp.Luns.Count; ++i)
            {
                result[i] = GetInfo((long)resp.Luns[i]);
            }

            return result;
        }

        /// <summary>
        /// Gets all the block-device LUNs available from the Target.
        /// </summary>
        /// <returns>The block-device LUNs.</returns>
        public long[] GetBlockDeviceLuns()
        {
            List<long> luns = new List<long>();

            foreach (LunInfo info in GetLuns())
            {
                if (info.DeviceType == LunClass.BlockStorage)
                {
                    luns.Add(info.Lun);
                }
            }

            return luns.ToArray();
        }

        /// <summary>
        /// Gets information about a particular LUN.
        /// </summary>
        /// <param name="lun">The LUN to query.</param>
        /// <returns>Information about the LUN.</returns>
        public LunInfo GetInfo(long lun)
        {
            ScsiInquiryCommand cmd = new ScsiInquiryCommand((ulong)lun, ScsiInquiryCommand.InitialResponseDataLength);

            ScsiInquiryStandardResponse resp = Send<ScsiInquiryStandardResponse>(cmd, null, 0, 0, ScsiInquiryCommand.InitialResponseDataLength);

            TargetInfo targetInfo = new TargetInfo(TargetName, new List<TargetAddress>(_addresses).ToArray());
            return new LunInfo(targetInfo, lun, resp.DeviceType, resp.Removable, resp.VendorId, resp.ProductId, resp.ProductRevision);
        }

        /// <summary>
        /// Gets the capacity of a particular LUN.
        /// </summary>
        /// <param name="lun">The LUN to query.</param>
        /// <returns>The LUN's capacity.</returns>
        public LunCapacity GetCapacity(long lun)
        {
            ScsiReadCapacityCommand cmd = new ScsiReadCapacityCommand((ulong)lun);

            ScsiReadCapacityResponse resp = Send<ScsiReadCapacityResponse>(cmd, null, 0, 0, ScsiReadCapacityCommand.ResponseDataLength);

            if (resp.Truncated)
            {
                throw new InvalidProtocolException("Truncated response");
            }

            return new LunCapacity(resp.NumLogicalBlocks, (int)resp.LogicalBlockSize);
        }

        /// <summary>
        /// Provides read-write access to a LUN as a VirtualDisk.
        /// </summary>
        /// <param name="lun">The LUN to access.</param>
        /// <returns>The new VirtualDisk instance.</returns>
        public Disk OpenDisk(long lun)
        {
            return OpenDisk(lun, FileAccess.ReadWrite);
        }

        /// <summary>
        /// Provides access to a LUN as a VirtualDisk.
        /// </summary>
        /// <param name="lun">The LUN to access.</param>
        /// <param name="access">The type of access desired.</param>
        /// <returns>The new VirtualDisk instance.</returns>
        public Disk OpenDisk(long lun, FileAccess access)
        {
            return new Disk(this, lun, access);
        }

        /// <summary>
        /// Reads some data from a LUN.
        /// </summary>
        /// <param name="lun">The LUN to read from.</param>
        /// <param name="startBlock">The first block to read.</param>
        /// <param name="blockCount">The number of blocks to read.</param>
        /// <param name="buffer">The buffer to fill.</param>
        /// <param name="offset">The offset of the first byte to fill.</param>
        /// <returns>The number of bytes read.</returns>
        public int Read(long lun, long startBlock, short blockCount, byte[] buffer, int offset)
        {
            ScsiReadCommand cmd = new ScsiReadCommand((ulong)lun, (uint)startBlock, (ushort)blockCount);
            return Send(cmd, null, 0, 0, buffer, offset, buffer.Length - offset);
        }

        /// <summary>
        /// Writes some data to a LUN.
        /// </summary>
        /// <param name="lun">The LUN to write to.</param>
        /// <param name="startBlock">The first block to write.</param>
        /// <param name="blockCount">The number of blocks to write.</param>
        /// <param name="blockSize">The size of each block (must match the actual LUN geometry).</param>
        /// <param name="buffer">The data to write.</param>
        /// <param name="offset">The offset of the first byte to write in buffer.</param>
        public void Write(long lun, long startBlock, short blockCount, int blockSize, byte[] buffer, int offset)
        {
            ScsiWriteCommand cmd = new ScsiWriteCommand((ulong)lun, (uint)startBlock, (ushort)blockCount);
            Send(cmd, buffer, offset, blockCount * blockSize, null, 0, 0);
        }

        /// <summary>
        /// Performs a raw SCSI command.
        /// </summary>
        /// <param name="lun">The target LUN for the command.</param>
        /// <param name="command">The command (a SCSI Command Descriptor Block, aka CDB).</param>
        /// <param name="outBuffer">Buffer of data to send with the command (or <c>null</c>).</param>
        /// <param name="outBufferOffset">Offset of first byte of data to send with the command.</param>
        /// <param name="outBufferLength">Amount of data to send with the command.</param>
        /// <param name="inBuffer">Buffer to receive data from the command (or <c>null</c>).</param>
        /// <param name="inBufferOffset">Offset of the first byte position to fill with received data.</param>
        /// <param name="inBufferLength">The expected amount of data to receive.</param>
        /// <returns>The number of bytes of data received.</returns>
        /// <remarks>
        /// <para>This method permits the caller to send raw SCSI commands to a LUN.</para>
        /// <para>The command .</para>
        /// </remarks>
        public int RawCommand(long lun, byte[] command, byte[] outBuffer, int outBufferOffset, int outBufferLength, byte[] inBuffer, int inBufferOffset, int inBufferLength)
        {
            if (outBuffer == null && outBufferLength != 0)
            {
                throw new ArgumentException("outBufferLength must be 0 if outBuffer null", nameof(outBufferLength));
            }

            if (inBuffer == null && inBufferLength != 0)
            {
                throw new ArgumentException("inBufferLength must be 0 if inBuffer null", nameof(inBufferLength));
            }

            ScsiRawCommand cmd = new ScsiRawCommand((ulong)lun, command, 0, command.Length);
            return Send(cmd, outBuffer, outBufferOffset, outBufferLength, inBuffer, inBufferOffset, inBufferLength);
        }

        internal uint NextCommandSequenceNumber()
        {
            return ++CommandSequenceNumber;
        }

        internal uint NextTaskTag()
        {
            return ++CurrentTaskTag;
        }

        internal ushort NextConnectionId()
        {
            return ++_nextConnectionId;
        }

        internal void GetParametersToNegotiate(TextBuffer parameters, KeyUsagePhase phase)
        {
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (PropertyInfo propInfo in properties)
            {
                ProtocolKeyAttribute attr = (ProtocolKeyAttribute)ReflectionHelper.GetCustomAttribute(propInfo, typeof(ProtocolKeyAttribute));

                if (attr != null)
                {
                    object value = propInfo.GetGetMethod(true).Invoke(this, null);

                    if (attr.ShouldTransmit(value, propInfo.PropertyType, phase, SessionType == SessionType.Discovery))
                    {
                        parameters.Add(attr.Name, ProtocolKeyAttribute.GetValueAsString(value, propInfo.PropertyType));
                        _negotiatedParameters.Add(attr.Name, string.Empty);
                    }
                }
            }
        }

        internal void ConsumeParameters(TextBuffer inParameters, TextBuffer outParameters)
        {
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (PropertyInfo propInfo in properties)
            {
                ProtocolKeyAttribute attr = (ProtocolKeyAttribute)ReflectionHelper.GetCustomAttribute(propInfo, typeof(ProtocolKeyAttribute));
                if (attr != null)
                {
                    if (inParameters[attr.Name] != null)
                    {
                        object value = ProtocolKeyAttribute.GetValueAsObject(inParameters[attr.Name], propInfo.PropertyType);

                        propInfo.GetSetMethod(true).Invoke(this, new[] { value });
                        inParameters.Remove(attr.Name);

                        if (attr.Type == KeyType.Negotiated && !_negotiatedParameters.ContainsKey(attr.Name))
                        {
                            value = propInfo.GetGetMethod(true).Invoke(this, null);
                            outParameters.Add(attr.Name, ProtocolKeyAttribute.GetValueAsString(value, propInfo.PropertyType));
                            _negotiatedParameters.Add(attr.Name, string.Empty);
                        }
                    }
                }
            }
        }

        #region Protocol Features

        /// <summary>
        /// Gets the name of the iSCSI target this session is connected to.
        /// </summary>
        [ProtocolKey("TargetName", null, KeyUsagePhase.SecurityNegotiation, KeySender.Initiator, KeyType.Declarative, UsedForDiscovery = true)]
        public string TargetName { get; internal set; }

        /// <summary>
        /// Gets the name of the iSCSI initiator seen by the target for this session.
        /// </summary>
        [ProtocolKey("InitiatorName", null, KeyUsagePhase.SecurityNegotiation, KeySender.Initiator, KeyType.Declarative, UsedForDiscovery = true)]
        public string InitiatorName
        {
            get { return "iqn.2008-2010-04.discutils.codeplex.com"; }
        }

        /// <summary>
        /// Gets the friendly name of the iSCSI target this session is connected to.
        /// </summary>
        [ProtocolKey("TargetAlias", "", KeyUsagePhase.All, KeySender.Target, KeyType.Declarative)]
        public string TargetAlias { get; internal set; }

        [ProtocolKey("SessionType", null, KeyUsagePhase.SecurityNegotiation, KeySender.Initiator, KeyType.Declarative, UsedForDiscovery = true)]
        internal SessionType SessionType { get; set; }

        [ProtocolKey("MaxConnections", "1", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal int MaxConnections { get; set; }

        [ProtocolKey("InitiatorAlias", "", KeyUsagePhase.All, KeySender.Initiator, KeyType.Declarative)]
        internal string InitiatorAlias { get; set; }

        [ProtocolKey("TargetPortalGroupTag", null, KeyUsagePhase.SecurityNegotiation, KeySender.Target, KeyType.Declarative)]
        internal int TargetPortalGroupTag { get; set; }

        [ProtocolKey("InitialR2T", "Yes", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal bool InitialR2T { get; set; }

        [ProtocolKey("ImmediateData", "Yes", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal bool ImmediateData { get; set; }

        [ProtocolKey("MaxBurstLength", "262144", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal int MaxBurstLength { get; set; }

        [ProtocolKey("FirstBurstLength", "65536", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal int FirstBurstLength { get; set; }

        [ProtocolKey("DefaultTime2Wait", "2", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal int DefaultTime2Wait { get; set; }

        [ProtocolKey("DefaultTime2Retain", "20", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal int DefaultTime2Retain { get; set; }

        [ProtocolKey("MaxOutstandingR2T", "1", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal int MaxOutstandingR2T { get; set; }

        [ProtocolKey("DataPDUInOrder", "Yes", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal bool DataPDUInOrder { get; set; }

        [ProtocolKey("DataSequenceInOrder", "Yes", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal bool DataSequenceInOrder { get; set; }

        [ProtocolKey("ErrorRecoveryLevel", "0", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, LeadingConnectionOnly = true)]
        internal int ErrorRecoveryLevel { get; set; }

        #endregion

        #region Scsi Bus

        /// <summary>
        /// Sends an SCSI command (aka task) to a LUN via the connected target.
        /// </summary>
        /// <param name="cmd">The command to send.</param>
        /// <param name="outBuffer">The data to send with the command.</param>
        /// <param name="outBufferOffset">The offset of the first byte to send.</param>
        /// <param name="outBufferCount">The number of bytes to send, if any.</param>
        /// <param name="inBuffer">The buffer to fill with returned data.</param>
        /// <param name="inBufferOffset">The first byte to fill with returned data.</param>
        /// <param name="inBufferMax">The maximum amount of data to receive.</param>
        /// <returns>The number of bytes received.</returns>
        private int Send(ScsiCommand cmd, byte[] outBuffer, int outBufferOffset, int outBufferCount, byte[] inBuffer, int inBufferOffset, int inBufferMax)
        {
            return ActiveConnection.Send(cmd, outBuffer, outBufferOffset, outBufferCount, inBuffer, inBufferOffset, inBufferMax);
        }

        private T Send<T>(ScsiCommand cmd, byte[] buffer, int offset, int count, int expected)
            where T : ScsiResponse, new()
        {
            return ActiveConnection.Send<T>(cmd, buffer, offset, count, expected);
        }

        #endregion
    }
}