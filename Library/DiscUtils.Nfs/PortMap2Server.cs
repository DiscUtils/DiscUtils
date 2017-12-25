//
// Copyright (c) 2017, Quamotion
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

#if !NET20
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DiscUtils.Nfs
{
    // http://pubs.opengroup.org/onlinepubs/9629799/chap6.htm
    public class PortMap2Server : IRpcProgram
    {
        public const int Port = 111;

        private readonly Collection<IRpcProgram> _programs;

        public PortMap2Server(Collection<IRpcProgram> programs)
        {
            _programs = programs;
        }

        public IEnumerable<int> Procedures
        { get; } = new int[] { (int)PortMapProc2.GetPort };

        public int ProgramIdentifier => 100000;
        public int ProgramVersion => 2;

        public IRpcObject Invoke(RpcCallHeader header, XdrDataReader reader)
        {
            switch ((PortMapProc2)header.Proc)
            {
                case PortMapProc2.GetPort:
                    uint port = 0;

                    if (_programs.Any(
                        p => p.ProgramIdentifier == header.Program
                        && p.ProgramVersion == header.Version))
                    {
                        port = PortMap2Server.Port;
                    }

                    return new PortMap2Port()
                    {
                        Port = port
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(header));
            }
        }
    }
}
#endif