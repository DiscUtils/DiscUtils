//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.IO;
using DiscUtils.Common;
using DiscUtils.Iso9660;

namespace ISOExtract
{
    class Program : ProgramBase
    {
        private CommandLineParameter _isoFileParam;
        private CommandLineParameter _targetFileParam;
        private CommandLineSwitch _destDirSwitch;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run(args);
        }

        protected override ProgramBase.StandardSwitches DefineCommandLine(CommandLineParser parser)
        {
            _isoFileParam = new CommandLineParameter("iso_file", "The ISO file to extract files from.", false);
            _targetFileParam = new CommandLineParameter("file", "The name of the file to extract.", false);
            _destDirSwitch = new CommandLineSwitch("d", "destdir", "dir", "The destination directory.  If not specified, the current directory is used.");

            parser.AddParameter(_isoFileParam);
            parser.AddParameter(_targetFileParam);
            parser.AddSwitch(_destDirSwitch);

            return StandardSwitches.Default;
        }

        protected override void DoRun()
        {
            string destDir = _destDirSwitch.IsPresent ? _destDirSwitch.Value : Environment.CurrentDirectory;

            using (FileStream isoStream = new FileStream(_isoFileParam.Value, FileMode.Open, FileAccess.Read))
            {
                CDReader reader = new CDReader(isoStream, true);
                if (_targetFileParam.IsPresent)
                {
                    string fileName = _targetFileParam.Value;
                    int sep = fileName.LastIndexOf('\\');
                    if (sep >= 0)
                    {
                        fileName = fileName.Substring(sep + 1);
                    }

                    using (FileStream outFile = new FileStream(destDir + "\\" + fileName, FileMode.Create, FileAccess.ReadWrite))
                    {
                        using (Stream inFile = reader.OpenFile(_targetFileParam.Value, FileMode.Open))
                        {
                            PumpStreams(inFile, outFile);
                        }
                    }
                }
            }
        }

        private static void PumpStreams(Stream inStream, Stream outStream)
        {
            byte[] buffer = new byte[4096];
            int bytesRead = inStream.Read(buffer, 0, 4096);
            while (bytesRead != 0)
            {
                outStream.Write(buffer, 0, bytesRead);
                bytesRead = inStream.Read(buffer, 0, 4096);
            }
        }
    }
}
