//
// Copyright (c) 2009, Kenneth Bell
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
using System.Security;

namespace DiscUtils.Iscsi
{
    internal enum SessionType
    {
        Discovery = 0,
        Normal = 1
    }

    /// <summary>
    /// Represents a connection to a particular Target.
    /// </summary>
    public class Session : IDisposable
    {
        private SessionType _type;
        private string _targetName;
        private TargetAddress[] _addresses;
        private string _userName;
        private string _password;

        private Connection _currentConnection;

        private ushort _targetSessionId; // a.k.a. TSIH
        private uint _commandSequenceNumber;
        private uint _nextInitiaterTaskTag;
        private ushort _nextConnectionId;

        internal Session(SessionType type, string targetName, params TargetAddress[] addresses)
            : this(type, targetName, null, null, addresses)
        {
        }

        internal Session(SessionType type, string targetName, string userName, string password, params TargetAddress[] addresses)
        {
            _type = type;
            _targetName = targetName;
            _addresses = addresses;
            _userName = userName;
            _password = password;

            _targetSessionId = 0;
            _commandSequenceNumber = 1;
            _nextInitiaterTaskTag = 1;

            if (string.IsNullOrEmpty(userName))
            {
                _currentConnection = new Connection(this, addresses[0], new Authenticator[] { new NullAuthenticator() });
            }
            else
            {
                _currentConnection = new Connection(this, addresses[0], new Authenticator[] { new NullAuthenticator(), new ChapAuthenticator(_userName, _password) });
            }
        }

        /// <summary>
        /// Disposes of this instance, closing the session with the Target.
        /// </summary>
        public void Dispose()
        {
            if (_currentConnection != null)
            {
                _currentConnection.Close(LogoutReason.CloseSession);
            }
            _currentConnection = null;
        }

        /// <summary>
        /// The name of the connected Target.
        /// </summary>
        public string TargetName
        {
            get { return _targetName; }
        }

        /// <summary>
        /// Enumerates all of the Targets.
        /// </summary>
        /// <returns>The list of Targets</returns>
        /// <remarks>In practice, for an established session, this just returns details of
        /// the connected Target.</remarks>
        public TargetInfo[] EnumerateTargets()
        {
            return _currentConnection.EnumerateTargets();
        }

        /// <summary>
        /// Gets the LUNs available from the Target.
        /// </summary>
        /// <returns>The LUNs available</returns>
        public long[] GetLuns()
        {
            ScsiReportLunsCommand cmd = new ScsiReportLunsCommand();

            ScsiReportLunsResponse resp = Send<ScsiReportLunsResponse>(cmd);

            if (resp.Truncated)
            {
                cmd.ExpectedResponseDataLength = resp.NeededDataLength;
                resp = Send<ScsiReportLunsResponse>(cmd);
            }
            if (resp.Truncated)
            {
                throw new InvalidProtocolException("Truncated response");
            }

            long[] result = new long[resp.Luns.Count];
            for (int i = 0; i < resp.Luns.Count; ++i)
            {
                result[i] = (long)resp.Luns[i];
            }
            return result;
        }

        /// <summary>
        /// Gets the capacity of a particular LUN.
        /// </summary>
        /// <param name="lun">The LUN to query</param>
        /// <returns>The LUN's capacity</returns>
        public long GetCapacity(long lun)
        {
            ScsiReadCapacityCommand cmd = new ScsiReadCapacityCommand((ulong)lun);

            ScsiReadCapacityResponse resp = Send<ScsiReadCapacityResponse>(cmd);

            if (resp.Truncated)
            {
                throw new InvalidProtocolException("Truncated response");
            }

            return ((long)resp.LogicalBlockSize) * ((long)resp.NumLogicalBlocks);
        }

        /// <summary>
        /// Provides access to a LUN as a VirtualDisk.
        /// </summary>
        /// <param name="lun">The LUN to access</param>
        /// <returns>The new VirtualDisk instance</returns>
        public Disk OpenDisk(long lun)
        {
            return new Disk(this, lun);
        }

        /// <summary>
        /// Reads some data from a LUN.
        /// </summary>
        /// <param name="lun">The LUN to read from</param>
        /// <param name="startBlock">The first block to read</param>
        /// <param name="blockCount">The number of blocks to read</param>
        /// <param name="buffer">The buffer to fill</param>
        /// <param name="offset">The offset of the first byte to fill</param>
        /// <returns>The number of bytes read</returns>
        public int Read(long lun, long startBlock, short blockCount, byte[] buffer, int offset)
        {
            ScsiReadCommand cmd = new ScsiReadCommand((ulong)lun, startBlock, (ushort)blockCount);

            return Read(cmd, buffer, offset);
        }

        internal Connection ActiveConnection
        {
            get { return _currentConnection; }
        }

        internal SessionType Type
        {
            get { return _type; }
        }

        internal ushort TargetSessionId
        {
            get { return _targetSessionId; }
            set { _targetSessionId = value; }
        }

        internal uint CommandSequenceNumber
        {
            get { return _commandSequenceNumber; }
        }

        internal uint NextCommandSequenceNumber()
        {
            return _commandSequenceNumber++;
        }

        internal uint CurrentTaskTag
        {
            get { return _nextInitiaterTaskTag; }
        }

        internal uint NextTaskTag()
        {
            return _nextInitiaterTaskTag++;
        }

        internal ushort NextConnectionId()
        {
            return ++_nextConnectionId;
        }

        #region Scsi Bus
        private T Send<T>(ScsiCommand cmd)
            where T : ScsiResponse, new()
        {
            return _currentConnection.Send<T>(cmd);
        }

        private int Read(ScsiReadCommand readCmd, byte[] buffer, int offset)
        {
            return _currentConnection.Read(readCmd, buffer, offset);
        }
        #endregion
    }
}
