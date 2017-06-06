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
using System.Net.Sockets;
using System.Reflection;
using DiscUtils.CoreCompat;
using DiscUtils.Streams;

namespace DiscUtils.Iscsi
{
    internal sealed class Connection : IDisposable
    {
        private readonly Authenticator[] _authenticators;

        /// <summary>
        /// The set of all 'parameters' we've negotiated.
        /// </summary>
        private readonly Dictionary<string, string> _negotiatedParameters;

        private readonly Stream _stream;

        public Connection(Session session, TargetAddress address, Authenticator[] authenticators)
        {
            Session = session;
            _authenticators = authenticators;

#if NETCORE
            TcpClient client = new TcpClient();
            client.ConnectAsync(address.NetworkAddress, address.NetworkPort).Wait();
#else
            TcpClient client = new TcpClient(address.NetworkAddress, address.NetworkPort);
#endif
            client.NoDelay = true;
            _stream = client.GetStream();

            Id = session.NextConnectionId();

            // Default negotiated values
            HeaderDigest = Digest.None;
            DataDigest = Digest.None;
            MaxInitiatorTransmitDataSegmentLength = 131072;
            MaxTargetReceiveDataSegmentLength = 8192;

            _negotiatedParameters = new Dictionary<string, string>();
            NegotiateSecurity();
            NegotiateFeatures();
        }

        internal LoginStages CurrentLoginStage { get; private set; } = LoginStages.SecurityNegotiation;

        internal uint ExpectedStatusSequenceNumber { get; private set; } = 1;

        internal ushort Id { get; }

        internal LoginStages NextLoginStage
        {
            get
            {
                switch (CurrentLoginStage)
                {
                    case LoginStages.SecurityNegotiation:
                        return LoginStages.LoginOperationalNegotiation;
                    case LoginStages.LoginOperationalNegotiation:
                        return LoginStages.FullFeaturePhase;
                    default:
                        return LoginStages.FullFeaturePhase;
                }
            }
        }

        internal Session Session { get; }

        public void Dispose()
        {
            Close(LogoutReason.CloseConnection);
        }

        public void Close(LogoutReason reason)
        {
            LogoutRequest req = new LogoutRequest(this);
            byte[] packet = req.GetBytes(reason);
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            ProtocolDataUnit pdu = ReadPdu();
            LogoutResponse resp = ParseResponse<LogoutResponse>(pdu);

            if (resp.Response != LogoutResponseCode.ClosedSuccessfully)
            {
                throw new InvalidProtocolException("Target indicated failure during logout: " + resp.Response);
            }

            _stream.Dispose();
        }

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
        public int Send(ScsiCommand cmd, byte[] outBuffer, int outBufferOffset, int outBufferCount, byte[] inBuffer, int inBufferOffset, int inBufferMax)
        {
            CommandRequest req = new CommandRequest(this, cmd.TargetLun);

            int toSend = Math.Min(Math.Min(outBufferCount, Session.ImmediateData ? Session.FirstBurstLength : 0), MaxTargetReceiveDataSegmentLength);
            byte[] packet = req.GetBytes(cmd, outBuffer, outBufferOffset, toSend, true, inBufferMax != 0, outBufferCount != 0, (uint)(outBufferCount != 0 ? outBufferCount : inBufferMax));
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            int numApproved = 0;
            int numSent = toSend;
            int pktsSent = 0;
            while (numSent < outBufferCount)
            {
                ProtocolDataUnit pdu = ReadPdu();

                ReadyToTransferPacket resp = ParseResponse<ReadyToTransferPacket>(pdu);
                numApproved = (int)resp.DesiredTransferLength;
                uint targetTransferTag = resp.TargetTransferTag;

                while (numApproved > 0)
                {
                    toSend = Math.Min(Math.Min(outBufferCount - numSent, numApproved), MaxTargetReceiveDataSegmentLength);

                    DataOutPacket pkt = new DataOutPacket(this, cmd.TargetLun);
                    packet = pkt.GetBytes(outBuffer, outBufferOffset + numSent, toSend, toSend == numApproved, pktsSent++, (uint)numSent, targetTransferTag);
                    _stream.Write(packet, 0, packet.Length);
                    _stream.Flush();

                    numApproved -= toSend;
                    numSent += toSend;
                }
            }

            bool isFinal = false;
            int numRead = 0;
            while (!isFinal)
            {
                ProtocolDataUnit pdu = ReadPdu();

                if (pdu.OpCode == OpCode.ScsiResponse)
                {
                    Response resp = ParseResponse<Response>(pdu);

                    if (resp.StatusPresent && resp.Status == ScsiStatus.CheckCondition)
                    {
                        ushort senseLength = EndianUtilities.ToUInt16BigEndian(pdu.ContentData, 0);
                        byte[] senseData = new byte[senseLength];
                        Array.Copy(pdu.ContentData, 2, senseData, 0, senseLength);
                        throw new ScsiCommandException(resp.Status, "Target indicated SCSI failure", senseData);
                    }
                    if (resp.StatusPresent && resp.Status != ScsiStatus.Good)
                    {
                        throw new ScsiCommandException(resp.Status, "Target indicated SCSI failure");
                    }

                    isFinal = resp.Header.FinalPdu;
                }
                else if (pdu.OpCode == OpCode.ScsiDataIn)
                {
                    DataInPacket resp = ParseResponse<DataInPacket>(pdu);

                    if (resp.StatusPresent && resp.Status != ScsiStatus.Good)
                    {
                        throw new ScsiCommandException(resp.Status, "Target indicated SCSI failure");
                    }

                    if (resp.ReadData != null)
                    {
                        Array.Copy(resp.ReadData, 0, inBuffer, (int)(inBufferOffset + resp.BufferOffset), resp.ReadData.Length);
                        numRead += resp.ReadData.Length;
                    }

                    isFinal = resp.Header.FinalPdu;
                }
            }

            Session.NextTaskTag();
            Session.NextCommandSequenceNumber();

            return numRead;
        }

        public T Send<T>(ScsiCommand cmd, byte[] buffer, int offset, int count, int expected)
            where T : ScsiResponse, new()
        {
            byte[] tempBuffer = new byte[expected];
            int numRead = Send(cmd, buffer, offset, count, tempBuffer, 0, expected);

            T result = new T();
            result.ReadFrom(tempBuffer, 0, numRead);
            return result;
        }

        public TargetInfo[] EnumerateTargets()
        {
            TextBuffer parameters = new TextBuffer();
            parameters.Add(SendTargetsParameter, "All");

            byte[] paramBuffer = new byte[parameters.Size];
            parameters.WriteTo(paramBuffer, 0);

            TextRequest req = new TextRequest(this);
            byte[] packet = req.GetBytes(0, paramBuffer, 0, paramBuffer.Length, true);

            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            ProtocolDataUnit pdu = ReadPdu();
            TextResponse resp = ParseResponse<TextResponse>(pdu);

            TextBuffer buffer = new TextBuffer();
            if (resp.TextData != null)
            {
                buffer.ReadFrom(resp.TextData, 0, resp.TextData.Length);
            }

            List<TargetInfo> targets = new List<TargetInfo>();

            string currentTarget = null;
            List<TargetAddress> currentAddresses = null;
            foreach (KeyValuePair<string, string> line in buffer.Lines)
            {
                if (currentTarget == null)
                {
                    if (line.Key != TargetNameParameter)
                    {
                        throw new InvalidProtocolException("Unexpected response parameter " + line.Key + " expected " + TargetNameParameter);
                    }

                    currentTarget = line.Value;
                    currentAddresses = new List<TargetAddress>();
                }
                else if (line.Key == TargetNameParameter)
                {
                    targets.Add(new TargetInfo(currentTarget, currentAddresses.ToArray()));
                    currentTarget = line.Value;
                    currentAddresses.Clear();
                }
                else if (line.Key == TargetAddressParameter)
                {
                    currentAddresses.Add(TargetAddress.Parse(line.Value));
                }
            }

            if (currentTarget != null)
            {
                targets.Add(new TargetInfo(currentTarget, currentAddresses.ToArray()));
            }

            return targets.ToArray();
        }

        internal void SeenStatusSequenceNumber(uint number)
        {
            if (number != 0 && number != ExpectedStatusSequenceNumber)
            {
                throw new InvalidProtocolException("Unexpected status sequence number " + number + ", expected " + ExpectedStatusSequenceNumber);
            }

            ExpectedStatusSequenceNumber = number + 1;
        }

        private void NegotiateSecurity()
        {
            CurrentLoginStage = LoginStages.SecurityNegotiation;

            //
            // Establish the contents of the request
            //
            TextBuffer parameters = new TextBuffer();

            GetParametersToNegotiate(parameters, KeyUsagePhase.SecurityNegotiation, Session.SessionType);
            Session.GetParametersToNegotiate(parameters, KeyUsagePhase.SecurityNegotiation);

            string authParam = _authenticators[0].Identifier;
            for (int i = 1; i < _authenticators.Length; ++i)
            {
                authParam += "," + _authenticators[i].Identifier;
            }

            parameters.Add(AuthMethodParameter, authParam);

            //
            // Send the request...
            //
            byte[] paramBuffer = new byte[parameters.Size];
            parameters.WriteTo(paramBuffer, 0);

            LoginRequest req = new LoginRequest(this);
            byte[] packet = req.GetBytes(paramBuffer, 0, paramBuffer.Length, true);

            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            //
            // Read the response...
            //
            TextBuffer settings = new TextBuffer();

            ProtocolDataUnit pdu = ReadPdu();
            LoginResponse resp = ParseResponse<LoginResponse>(pdu);

            if (resp.StatusCode != LoginStatusCode.Success)
            {
                throw new LoginException("iSCSI Target indicated login failure: " + resp.StatusCode);
            }

            if (resp.Continue)
            {
                MemoryStream ms = new MemoryStream();
                ms.Write(resp.TextData, 0, resp.TextData.Length);

                while (resp.Continue)
                {
                    pdu = ReadPdu();
                    resp = ParseResponse<LoginResponse>(pdu);
                    ms.Write(resp.TextData, 0, resp.TextData.Length);
                }

                settings.ReadFrom(ms.ToArray(), 0, (int)ms.Length);
            }
            else if (resp.TextData != null)
            {
                settings.ReadFrom(resp.TextData, 0, resp.TextData.Length);
            }

            Authenticator authenticator = null;
            for (int i = 0; i < _authenticators.Length; ++i)
            {
                if (settings[AuthMethodParameter] == _authenticators[i].Identifier)
                {
                    authenticator = _authenticators[i];
                    break;
                }
            }

            settings.Remove(AuthMethodParameter);
            settings.Remove("TargetPortalGroupTag");

            if (authenticator == null)
            {
                throw new LoginException("iSCSI Target specified an unsupported authentication method: " + settings[AuthMethodParameter]);
            }

            parameters = new TextBuffer();
            ConsumeParameters(settings, parameters);

            while (!resp.Transit)
            {
                //
                // Send the request...
                //
                parameters = new TextBuffer();
                authenticator.GetParameters(parameters);
                paramBuffer = new byte[parameters.Size];
                parameters.WriteTo(paramBuffer, 0);

                req = new LoginRequest(this);
                packet = req.GetBytes(paramBuffer, 0, paramBuffer.Length, true);

                _stream.Write(packet, 0, packet.Length);
                _stream.Flush();

                //
                // Read the response...
                //
                settings = new TextBuffer();

                pdu = ReadPdu();
                resp = ParseResponse<LoginResponse>(pdu);

                if (resp.StatusCode != LoginStatusCode.Success)
                {
                    throw new LoginException("iSCSI Target indicated login failure: " + resp.StatusCode);
                }

                if (resp.TextData != null && resp.TextData.Length != 0)
                {
                    if (resp.Continue)
                    {
                        MemoryStream ms = new MemoryStream();
                        ms.Write(resp.TextData, 0, resp.TextData.Length);

                        while (resp.Continue)
                        {
                            pdu = ReadPdu();
                            resp = ParseResponse<LoginResponse>(pdu);
                            ms.Write(resp.TextData, 0, resp.TextData.Length);
                        }

                        settings.ReadFrom(ms.ToArray(), 0, (int)ms.Length);
                    }
                    else
                    {
                        settings.ReadFrom(resp.TextData, 0, resp.TextData.Length);
                    }

                    authenticator.SetParameters(settings);
                }
            }

            if (resp.NextStage != NextLoginStage)
            {
                throw new LoginException("iSCSI Target wants to transition to a different login stage: " + resp.NextStage + " (expected: " + NextLoginStage + ")");
            }

            CurrentLoginStage = resp.NextStage;
        }

        private void NegotiateFeatures()
        {
            //
            // Send the request...
            //
            TextBuffer parameters = new TextBuffer();
            GetParametersToNegotiate(parameters, KeyUsagePhase.OperationalNegotiation, Session.SessionType);
            Session.GetParametersToNegotiate(parameters, KeyUsagePhase.OperationalNegotiation);

            byte[] paramBuffer = new byte[parameters.Size];
            parameters.WriteTo(paramBuffer, 0);

            LoginRequest req = new LoginRequest(this);
            byte[] packet = req.GetBytes(paramBuffer, 0, paramBuffer.Length, true);

            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            //
            // Read the response...
            //
            TextBuffer settings = new TextBuffer();

            ProtocolDataUnit pdu = ReadPdu();
            LoginResponse resp = ParseResponse<LoginResponse>(pdu);

            if (resp.StatusCode != LoginStatusCode.Success)
            {
                throw new LoginException("iSCSI Target indicated login failure: " + resp.StatusCode);
            }

            if (resp.Continue)
            {
                MemoryStream ms = new MemoryStream();
                ms.Write(resp.TextData, 0, resp.TextData.Length);

                while (resp.Continue)
                {
                    pdu = ReadPdu();
                    resp = ParseResponse<LoginResponse>(pdu);
                    ms.Write(resp.TextData, 0, resp.TextData.Length);
                }

                settings.ReadFrom(ms.ToArray(), 0, (int)ms.Length);
            }
            else if (resp.TextData != null)
            {
                settings.ReadFrom(resp.TextData, 0, resp.TextData.Length);
            }

            parameters = new TextBuffer();
            ConsumeParameters(settings, parameters);

            while (!resp.Transit || parameters.Count != 0)
            {
                paramBuffer = new byte[parameters.Size];
                parameters.WriteTo(paramBuffer, 0);

                req = new LoginRequest(this);
                packet = req.GetBytes(paramBuffer, 0, paramBuffer.Length, true);

                _stream.Write(packet, 0, packet.Length);
                _stream.Flush();

                //
                // Read the response...
                //
                settings = new TextBuffer();

                pdu = ReadPdu();
                resp = ParseResponse<LoginResponse>(pdu);

                if (resp.StatusCode != LoginStatusCode.Success)
                {
                    throw new LoginException("iSCSI Target indicated login failure: " + resp.StatusCode);
                }

                parameters = new TextBuffer();

                if (resp.TextData != null)
                {
                    if (resp.Continue)
                    {
                        MemoryStream ms = new MemoryStream();
                        ms.Write(resp.TextData, 0, resp.TextData.Length);

                        while (resp.Continue)
                        {
                            pdu = ReadPdu();
                            resp = ParseResponse<LoginResponse>(pdu);
                            ms.Write(resp.TextData, 0, resp.TextData.Length);
                        }

                        settings.ReadFrom(ms.ToArray(), 0, (int)ms.Length);
                    }
                    else
                    {
                        settings.ReadFrom(resp.TextData, 0, resp.TextData.Length);
                    }

                    ConsumeParameters(settings, parameters);
                }
            }

            if (resp.NextStage != NextLoginStage)
            {
                throw new LoginException("iSCSI Target wants to transition to a different login stage: " + resp.NextStage + " (expected: " + NextLoginStage + ")");
            }

            CurrentLoginStage = resp.NextStage;
        }

        private ProtocolDataUnit ReadPdu()
        {
            ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(_stream, HeaderDigest != Digest.None, DataDigest != Digest.None);

            if (pdu.OpCode == OpCode.Reject)
            {
                RejectPacket pkt = new RejectPacket();
                pkt.Parse(pdu);

                throw new IscsiException("Target sent reject packet, reason " + pkt.Reason);
            }

            return pdu;
        }

        private void GetParametersToNegotiate(TextBuffer parameters, KeyUsagePhase phase, SessionType sessionType)
        {
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (PropertyInfo propInfo in properties)
            {
                ProtocolKeyAttribute attr = (ProtocolKeyAttribute)ReflectionHelper.GetCustomAttribute(propInfo, typeof(ProtocolKeyAttribute));
                if (attr != null)
                {
                    object value = propInfo.GetGetMethod(true).Invoke(this, null);

                    if (attr.ShouldTransmit(value, propInfo.PropertyType, phase, sessionType == SessionType.Discovery))
                    {
                        parameters.Add(attr.Name, ProtocolKeyAttribute.GetValueAsString(value, propInfo.PropertyType));
                        _negotiatedParameters.Add(attr.Name, string.Empty);
                    }
                }
            }
        }

        private void ConsumeParameters(TextBuffer inParameters, TextBuffer outParameters)
        {
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (PropertyInfo propInfo in properties)
            {
                ProtocolKeyAttribute attr = (ProtocolKeyAttribute)ReflectionHelper.GetCustomAttribute(propInfo, typeof(ProtocolKeyAttribute));
                if (attr != null && (attr.Sender & KeySender.Target) != 0)
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

            Session.ConsumeParameters(inParameters, outParameters);

            foreach (KeyValuePair<string, string> param in inParameters.Lines)
            {
                outParameters.Add(param.Key, "NotUnderstood");
            }
        }

        private T ParseResponse<T>(ProtocolDataUnit pdu)
            where T : BaseResponse, new()
        {
            BaseResponse resp;

            switch (pdu.OpCode)
            {
                case OpCode.LoginResponse:
                    resp = new LoginResponse();
                    break;

                case OpCode.LogoutResponse:
                    resp = new LogoutResponse();
                    break;

                case OpCode.ReadyToTransfer:
                    resp = new ReadyToTransferPacket();
                    break;

                case OpCode.Reject:
                    resp = new RejectPacket();
                    break;

                case OpCode.ScsiDataIn:
                    resp = new DataInPacket();
                    break;

                case OpCode.ScsiResponse:
                    resp = new Response();
                    break;

                case OpCode.TextResponse:
                    resp = new TextResponse();
                    break;

                default:
                    throw new InvalidProtocolException("Unrecognized response opcode: " + pdu.OpCode);
            }

            resp.Parse(pdu);
            if (resp.StatusPresent)
            {
                SeenStatusSequenceNumber(resp.StatusSequenceNumber);
            }

            T result = resp as T;
            if (result == null)
            {
                throw new InvalidProtocolException("Unexpected response, expected " + typeof(T) + ", got " + result.GetType());
            }

            return result;
        }

        #region Parameters

        private const string InitiatorNameParameter = "InitiatorName";
        private const string SessionTypeParameter = "SessionType";
        private const string AuthMethodParameter = "AuthMethod";

        private const string HeaderDigestParameter = "HeaderDigest";
        private const string DataDigestParameter = "DataDigest";
        private const string MaxRecvDataSegmentLengthParameter = "MaxRecvDataSegmentLength";
        private const string DefaultTime2WaitParameter = "DefaultTime2Wait";
        private const string DefaultTime2RetainParameter = "DefaultTime2Retain";

        private const string SendTargetsParameter = "SendTargets";
        private const string TargetNameParameter = "TargetName";
        private const string TargetAddressParameter = "TargetAddress";

        private const string NoneValue = "None";
        private const string ChapValue = "CHAP";

        #endregion

        #region Protocol Features

        [ProtocolKey("HeaderDigest", "None", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, UsedForDiscovery = true)]
        public Digest HeaderDigest { get; set; }

        [ProtocolKey("DataDigest", "None", KeyUsagePhase.OperationalNegotiation, KeySender.Both, KeyType.Negotiated, UsedForDiscovery = true)]
        public Digest DataDigest { get; set; }

        [ProtocolKey("MaxRecvDataSegmentLength", "8192", KeyUsagePhase.OperationalNegotiation, KeySender.Initiator, KeyType.Declarative)]
        internal int MaxInitiatorTransmitDataSegmentLength { get; set; }

        [ProtocolKey("MaxRecvDataSegmentLength", "8192", KeyUsagePhase.OperationalNegotiation, KeySender.Target, KeyType.Declarative)]
        internal int MaxTargetReceiveDataSegmentLength { get; set; }

        #endregion
    }
}