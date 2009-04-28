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
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace DiscUtils.Iscsi
{
    internal sealed class Connection : IDisposable
    {
        private Session _session;

        private Stream _stream;
        private Authenticator[] _authenticators;

        private ushort _id;
        private uint _expectedStatusSequenceNumber = 1;
        private LoginStages _loginStage = LoginStages.SecurityNegotiation;

        public Connection(Session session, TargetAddress address, Authenticator[] authenticators)
        {
            _session = session;
            _authenticators = authenticators;

            TcpClient client = new TcpClient(address.NetworkAddress, address.NetworkPort);
            client.NoDelay = true;
            _stream = client.GetStream();

            _id = session.NextConnectionId();

            NegotiateSecurity();
            NegotiateFeatures();
        }

        public void Dispose()
        {
            Close(LogoutReason.CloseConnection);
        }

        internal ushort Id
        {
            get { return _id; }
        }

        internal Session Session
        {
            get { return _session; }
        }

        internal LoginStages CurrentLoginStage
        {
            get { return _loginStage; }
        }

        public void Close(LogoutReason reason)
        {
            LogoutRequest req = new LogoutRequest(this);
            byte[] packet = req.GetBytes(reason);
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
            LogoutResponse resp = ParseResponse<LogoutResponse>(pdu);

            if (resp.Response != LogoutResponseCode.ClosedSuccessfully)
            {
                throw new InvalidProtocolException("Target indicated failure during logout: " + resp.Response);
            }

            _stream.Close();
        }

        public T Send<T>(ScsiCommand cmd)
            where T : ScsiResponse, new()
        {
            ScsiCommandRequest req = new ScsiCommandRequest(this, cmd.TargetLun);
            byte[] packet = req.GetBytes(cmd, null, 0, 0, true, true, false);
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);

            ScsiDataIn resp = ParseResponse<ScsiDataIn>(pdu);

            T result = new T();
            result.ReadFrom(resp.ReadData, 0, resp.ReadData.Length);
            return result;
        }

        public int Read(ScsiReadCommand readCmd, byte[] buffer, int offset)
        {
            ScsiCommandRequest req = new ScsiCommandRequest(this, readCmd.TargetLun);
            byte[] packet = req.GetBytes(readCmd, null, 0, 0, true, true, false);
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();

            bool isFinal = false;
            int numRead = 0;
            while (!isFinal)
            {
                ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);

                ScsiDataIn resp = ParseResponse<ScsiDataIn>(pdu);

                if (resp.StatusPresent && resp.Status != ScsiStatus.Good)
                {
                    throw new InvalidProtocolException("Target indicated failure during read: " + resp.Status);
                }

                Array.Copy(resp.ReadData, 0, buffer, offset + resp.BufferOffset, resp.ReadData.Length);
                numRead += resp.ReadData.Length;

                isFinal = resp.Header.FinalPdu;
            }

            return numRead;
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

            ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
            TextResponse resp = ParseResponse<TextResponse>(pdu);

            TextBuffer buffer = new TextBuffer();
            buffer.ReadFrom(resp.TextData, 0, resp.TextData.Length);

            List<TargetInfo> targets = new List<TargetInfo>();

            string currentTarget = null;
            List<TargetAddress> currentAddresses = null;
            foreach (var line in buffer.Lines)
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

        internal LoginStages NextLoginStage
        {
            get
            {
                switch(_loginStage)
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

        internal uint ExpectedStatusSequenceNumber
        {
            get { return _expectedStatusSequenceNumber; }
        }

        internal void SeenStatusSequenceNumber(uint number)
        {
            if (number != 0 && number != _expectedStatusSequenceNumber)
            {
                throw new InvalidProtocolException("Unexpected status sequence number " + number + ", expected " + _expectedStatusSequenceNumber);
            }
            _expectedStatusSequenceNumber = number + 1;
        }

        private void NegotiateSecurity()
        {
            _loginStage = LoginStages.SecurityNegotiation;

            //
            // Send the request...
            //
            TextBuffer parameters = new TextBuffer();
            parameters.Add(InitiatorNameParameter, "iqn.2009-04.discutils.codeplex.com");
            if (_session.Type == SessionType.Discovery)
            {
                parameters.Add(SessionTypeParameter, "Discovery");
            }
            else
            {
                parameters.Add(SessionTypeParameter, "Normal");
                parameters.Add(TargetNameParameter, _session.TargetName);
            }


            string authParam = _authenticators[0].Identifier;
            for (int i = 1; i < _authenticators.Length; ++i)
            {
                authParam += "," + _authenticators[i].Identifier;
            }
            parameters.Add(AuthMethodParameter, authParam);


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

            ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
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
                    pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
                    resp = ParseResponse<LoginResponse>(pdu);
                    ms.Write(resp.TextData, 0, resp.TextData.Length);
                }

                settings.ReadFrom(ms.GetBuffer(), 0, (int)ms.Length);
            }
            else
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

            if (authenticator == null)
            {
                throw new LoginException("iSCSI Target specified an unsupported authentication method: " + settings[AuthMethodParameter]);
            }

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

                pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
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
                            pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
                            resp = ParseResponse<LoginResponse>(pdu);
                            ms.Write(resp.TextData, 0, resp.TextData.Length);
                        }

                        settings.ReadFrom(ms.GetBuffer(), 0, (int)ms.Length);
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

            _loginStage = resp.NextStage;
        }

        private void NegotiateFeatures()
        {
            //
            // Send the request...
            //
            TextBuffer parameters = new TextBuffer();
            parameters.Add(HeaderDigestParameter, NoneValue);
            parameters.Add(DataDigestParameter, NoneValue);
            parameters.Add(MaxRecvDataSegmentLengthParameter, "65536");
            parameters.Add(DefaultTime2WaitParameter, "0");
            parameters.Add(DefaultTime2RetainParameter, "60");

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

            ProtocolDataUnit pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
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
                    pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
                    resp = ParseResponse<LoginResponse>(pdu);
                    ms.Write(resp.TextData, 0, resp.TextData.Length);
                }

                settings.ReadFrom(ms.GetBuffer(), 0, (int)ms.Length);
            }
            else
            {
                settings.ReadFrom(resp.TextData, 0, resp.TextData.Length);
            }

            while (!resp.Transit)
            {
                // Any settings in the response that weren't in the request are considered
                // a new negotiation.  For now, just pretend we don't understand...
                TextBuffer oldParameters = parameters;
                parameters = new TextBuffer();
                foreach (var line in settings.Lines)
                {
                    if (oldParameters[line.Key] == null)
                    {
                        parameters.Add(line.Key, "NotUnderstood");
                    }
                }


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

                pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
                resp = ParseResponse<LoginResponse>(pdu);

                if (resp.StatusCode != LoginStatusCode.Success)
                {
                    throw new LoginException("iSCSI Target indicated login failure: " + resp.StatusCode);
                }

                if (resp.TextData != null)
                {
                    if (resp.Continue)
                    {
                        MemoryStream ms = new MemoryStream();
                        ms.Write(resp.TextData, 0, resp.TextData.Length);

                        while (resp.Continue)
                        {
                            pdu = ProtocolDataUnit.ReadFrom(_stream, false, false);
                            resp = ParseResponse<LoginResponse>(pdu);
                            ms.Write(resp.TextData, 0, resp.TextData.Length);
                        }

                        settings.ReadFrom(ms.GetBuffer(), 0, (int)ms.Length);
                    }
                    else
                    {
                        settings.ReadFrom(resp.TextData, 0, resp.TextData.Length);
                    }
                }
            }

            if (resp.NextStage != NextLoginStage)
            {
                throw new LoginException("iSCSI Target wants to transition to a different login stage: " + resp.NextStage + " (expected: " + NextLoginStage + ")");
            }

            _loginStage = resp.NextStage;
        }

        private T ParseResponse<T>(ProtocolDataUnit pdu)
            where T : Response, new()
        {
            T result = new T();
            result.Parse(pdu);

            if (result.StatusPresent)
            {
                SeenStatusSequenceNumber(result.StatusSequenceNumber);
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

        private string CombineValues(params string[] values)
        {
            string result = values[0];
            for (int i = 1; i < values.Length; ++i)
            {
                result += "," + values[i];
            }
            return result;
        }

        #endregion
    }
}
