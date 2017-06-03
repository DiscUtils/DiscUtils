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
using System.Net;
using System.Security.Cryptography;
using System.Text;
using DiscUtils.Streams;
using Buffer=DiscUtils.Streams.Buffer;

namespace DiscUtils.OpticalDiscSharing
{
    internal sealed class DiscContentBuffer : Buffer
    {
        private string _authHeader;

        private readonly string _password;
        private readonly Uri _uri;
        private readonly string _userName;

        internal DiscContentBuffer(Uri uri, string userName, string password)
        {
            _uri = uri;
            _userName = userName;
            _password = password;

            HttpWebResponse response = SendRequest(() =>
            {
                HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(uri);
                wr.Method = "HEAD";
                return wr;
            });

            Capacity = response.ContentLength;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Capacity { get; }

        public override int Read(long pos, byte[] buffer, int offset, int count)
        {
            HttpWebResponse response = SendRequest(() =>
            {
                HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(_uri);
                wr.Method = "GET";
                wr.AddRange((int)pos, (int)(pos + count - 1));
                return wr;
            });

            using (Stream s = response.GetResponseStream())
            {
                int total = (int)response.ContentLength;
                int read = 0;
                while (read < Math.Min(total, count))
                {
                    read += s.Read(buffer, offset + read, count - read);
                }

                return read;
            }
        }

        public override void Write(long pos, byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Attempt to write to shared optical disc");
        }

        public override void SetCapacity(long value)
        {
            throw new InvalidOperationException("Attempt to change size of shared optical disc");
        }

        public override IEnumerable<StreamExtent> GetExtentsInRange(long start, long count)
        {
            return StreamExtent.Intersect(
                new[] { new StreamExtent(0, Capacity) },
                new[] { new StreamExtent(start, count) });
        }

        private static string ToHexString(byte[] p)
        {
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < p.Length; ++i)
            {
                int j = (p[i] >> 4) & 0xf;
                result.Append((char)(j <= 9 ? '0' + j : 'a' + (j - 10)));
                j = p[i] & 0xf;
                result.Append((char)(j <= 9 ? '0' + j : 'a' + (j - 10)));
            }

            return result.ToString();
        }

        private static Dictionary<string, string> ParseAuthenticationHeader(string header, out string authMethod)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            string[] elements = header.Split(' ');

            authMethod = elements[0];

            for (int i = 1; i < elements.Length; ++i)
            {
                string[] nvPair = elements[i].Split(new[] { '=' }, 2, StringSplitOptions.None);
                result.Add(nvPair[0], nvPair[1].Trim('\"'));
            }

            return result;
        }

        private HttpWebResponse SendRequest(WebRequestCreator wrc)
        {
            HttpWebRequest wr = wrc();
            if (_authHeader != null)
            {
                wr.Headers["Authorization"] = _authHeader;
            }

            try
            {
                return (HttpWebResponse)wr.GetResponse();
            }
            catch (WebException we)
            {
                HttpWebResponse wresp = (HttpWebResponse)we.Response;

                if (wresp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    string authMethod;
                    Dictionary<string, string> authParams = ParseAuthenticationHeader(wresp.Headers["WWW-Authenticate"], out authMethod);

                    if (authMethod != "Digest")
                    {
                        throw;
                    }

                    string resp = CalcDigestResponse(authParams["nonce"], wr.RequestUri.AbsolutePath, wr.Method, authParams["realm"]);

                    _authHeader = "Digest username=\"" + _userName + "\", realm=\"ODS\", nonce=\"" + authParams["nonce"] + "\", uri=\"" + wr.RequestUri.AbsolutePath + "\", response=\"" + resp + "\"";

                    (wresp as IDisposable).Dispose();

                    wr = wrc();
                    wr.Headers["Authorization"] = _authHeader;

                    return (HttpWebResponse)wr.GetResponse();
                }

                throw;
            }
        }

        private string CalcDigestResponse(string nonce, string uriPath, string method, string realm)
        {
            string a2 = method + ":" + uriPath;
            MD5 ha2hash = MD5.Create();
            string ha2 = ToHexString(ha2hash.ComputeHash(Encoding.ASCII.GetBytes(a2)));

            string a1 = _userName + ":" + realm + ":" + _password;
            MD5 ha1hash = MD5.Create();
            string ha1 = ToHexString(ha1hash.ComputeHash(Encoding.ASCII.GetBytes(a1)));

            string toHash = ha1 + ":" + nonce + ":" + ha2;
            MD5 respHas = MD5.Create();
            byte[] hash = respHas.ComputeHash(Encoding.ASCII.GetBytes(toHash));
            return ToHexString(hash);
        }

        internal delegate HttpWebRequest WebRequestCreator();
    }
}