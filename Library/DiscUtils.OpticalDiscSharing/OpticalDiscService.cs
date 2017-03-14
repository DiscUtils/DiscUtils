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
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using DiscUtils.Net.Dns;

namespace DiscUtils.OpticalDiscSharing
{
    /// <summary>
    /// Represents a particular Optical Disc Sharing service (typically a Mac or PC).
    /// </summary>
    public sealed class OpticalDiscService
    {
        private string _askToken;
        private ServiceInstance _instance;
        private readonly ServiceDiscoveryClient _sdClient;
        private string _userName;

        internal OpticalDiscService(ServiceInstance instance, ServiceDiscoveryClient sdClient)
        {
            _sdClient = sdClient;
            _instance = instance;
        }

        /// <summary>
        /// Gets information about the optical discs advertised by this service.
        /// </summary>
        public IEnumerable<DiscInfo> AdvertisedDiscs
        {
            get
            {
                foreach (KeyValuePair<string, byte[]> sdParam in _instance.Parameters)
                {
                    if (sdParam.Key.StartsWith("disk"))
                    {
                        Dictionary<string, string> diskParams = GetParams(sdParam.Key);
                        string infoVal;

                        DiscInfo info = new DiscInfo { Name = sdParam.Key };

                        if (diskParams.TryGetValue("adVN", out infoVal))
                        {
                            info.VolumeLabel = infoVal;
                        }

                        if (diskParams.TryGetValue("adVT", out infoVal))
                        {
                            info.VolumeType = infoVal;
                        }

                        yield return info;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the display name of this service.
        /// </summary>
        public string DisplayName
        {
            get { return _instance.DisplayName; }
        }

        /// <summary>
        /// Connects to the service.
        /// </summary>
        /// <param name="userName">The username to use, if the owner of the Mac / PC is prompted.</param>
        /// <param name="computerName">The computer name to use, if the owner of the Mac / PC is prompted.</param>
        /// <param name="maxWaitSeconds">The maximum number of seconds to wait to be granted access.</param>
        public void Connect(string userName, string computerName, int maxWaitSeconds)
        {
            Dictionary<string, string> sysParams = GetParams("sys");

            int volFlags = 0;
            string volFlagsStr;
            if (sysParams.TryGetValue("adVF", out volFlagsStr))
            {
                volFlags = ParseInt(volFlagsStr);
            }

            if ((volFlags & 0x200) != 0)
            {
                _userName = userName;
                AskForAccess(userName, computerName, maxWaitSeconds);

                // Flush any stale mDNS data - the server advertises extra info (such as the discs available)
                // after a client is granted permission to access a disc.
                _sdClient.FlushCache();

                _instance = _sdClient.LookupInstance(_instance.Name, ServiceInstanceFields.All);
            }
        }

        /// <summary>
        /// Opens a shared optical disc as a virtual disk.
        /// </summary>
        /// <param name="name">The name of the disc, from the Name field of DiscInfo.</param>
        /// <returns>The virtual disk.</returns>
        public VirtualDisk OpenDisc(string name)
        {
            ServiceInstanceEndPoint siep = _instance.EndPoints[0];
            List<IPEndPoint> ipAddrs = new List<IPEndPoint>(siep.IPEndPoints);

            UriBuilder builder = new UriBuilder();
            builder.Scheme = "http";
            builder.Host = ipAddrs[0].Address.ToString();
            builder.Port = ipAddrs[0].Port;
            builder.Path = "/" + name + ".dmg";

            return new Disc(builder.Uri, _userName, _askToken);
        }

        private static string GetAskToken(string askId, UriBuilder uriBuilder, int maxWaitSecs)
        {
            uriBuilder.Path = "/ods-ask-status";
            uriBuilder.Query = "askID=" + askId;

            bool askBusy = true;
            string askStatus = "unknown";
            string askToken = null;

            DateTime start = DateTime.UtcNow;
            TimeSpan maxWait = TimeSpan.FromSeconds(maxWaitSecs);

            while (askStatus == "unknown" && DateTime.Now - start < maxWait)
            {
                Thread.Sleep(1000);

                WebRequest wreq = WebRequest.Create(uriBuilder.Uri);
                wreq.Method = "GET";

                WebResponse wrsp = wreq.GetResponse();
                using (Stream inStream = wrsp.GetResponseStream())
                {
                    Dictionary<string, object> plist = Plist.Parse(inStream);

                    askBusy = (bool)plist["askBusy"];
                    askStatus = plist["askStatus"] as string;

                    if (askStatus == "accepted")
                    {
                        askToken = plist["askToken"] as string;
                    }
                }
            }

            if (askToken == null)
            {
                throw new UnauthorizedAccessException("Access not granted");
            }

            return askToken;
        }

        private static string InitiateAsk(string userName, string computerName, UriBuilder uriBuilder)
        {
            uriBuilder.Path = "/ods-ask";

            HttpWebRequest wreq = (HttpWebRequest)WebRequest.Create(uriBuilder.Uri);
            wreq.Method = "POST";

            Dictionary<string, object> req = new Dictionary<string, object>();
            req["askDevice"] = string.Empty;
            req["computer"] = computerName;
            req["user"] = userName;

            using (Stream outStream = wreq.GetRequestStream())
            {
                Plist.Write(outStream, req);
            }

            string askId;
            WebResponse wrsp = wreq.GetResponse();
            using (Stream inStream = wrsp.GetResponseStream())
            {
                Dictionary<string, object> plist = Plist.Parse(inStream);
                askId = ((int)plist["askID"]).ToString(CultureInfo.InvariantCulture);
            }

            return askId;
        }

        private static int ParseInt(string volFlagsStr)
        {
            if (volFlagsStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(volFlagsStr.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return int.Parse(volFlagsStr, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private void AskForAccess(string userName, string computerName, int maxWaitSecs)
        {
            ServiceInstanceEndPoint siep = _instance.EndPoints[0];
            List<IPEndPoint> ipAddrs = new List<IPEndPoint>(siep.IPEndPoints);

            UriBuilder uriBuilder = new UriBuilder();
            uriBuilder.Scheme = "http";
            uriBuilder.Host = ipAddrs[0].Address.ToString();
            uriBuilder.Port = ipAddrs[0].Port;

            string askId = InitiateAsk(userName, computerName, uriBuilder);

            _askToken = GetAskToken(askId, uriBuilder, maxWaitSecs);
        }

        private Dictionary<string, string> GetParams(string section)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            byte[] data;
            if (_instance.Parameters.TryGetValue(section, out data))
            {
                string asString = Encoding.ASCII.GetString(data);
                string[] nvPairs = asString.Split(',');

                foreach (string nvPair in nvPairs)
                {
                    string[] parts = nvPair.Split('=');
                    result[parts[0]] = parts[1];
                }
            }

            return result;
        }
    }
}