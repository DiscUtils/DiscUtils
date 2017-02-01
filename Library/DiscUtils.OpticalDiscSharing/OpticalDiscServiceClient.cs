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
using DiscUtils.Net.Dns;

namespace DiscUtils.OpticalDiscSharing
{
    /// <summary>
    /// Provides access to Optical Disc Sharing services.
    /// </summary>
    public sealed class OpticalDiscServiceClient : IDisposable
    {
        private ServiceDiscoveryClient _sdClient;

        /// <summary>
        /// Initializes a new instance of the OpticalDiscServiceClient class.
        /// </summary>
        public OpticalDiscServiceClient()
        {
            _sdClient = new ServiceDiscoveryClient();
        }

        /// <summary>
        /// Disposes of this instance.
        /// </summary>
        public void Dispose()
        {
            if (_sdClient != null)
            {
                _sdClient.Dispose();
                _sdClient = null;
            }
        }

        /// <summary>
        /// Looks up the ODS services advertised.
        /// </summary>
        /// <returns>A list of discovered ODS services.</returns>
        public OpticalDiscService[] LookupServices()
        {
            return LookupServices("local.");
        }

        /// <summary>
        /// Looks up the ODS services advertised in a domain.
        /// </summary>
        /// <param name="domain">The domain to look in.</param>
        /// <returns>A list of discovered ODS services.</returns>
        public OpticalDiscService[] LookupServices(string domain)
        {
            List<OpticalDiscService> services = new List<OpticalDiscService>();

            foreach (ServiceInstance instance in _sdClient.LookupInstances("_odisk._tcp", domain, ServiceInstanceFields.All))
            {
                services.Add(new OpticalDiscService(instance, _sdClient));
            }

            return services.ToArray();
        }
    }
}