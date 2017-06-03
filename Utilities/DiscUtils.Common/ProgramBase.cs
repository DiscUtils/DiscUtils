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
using System.Text;
using System.Diagnostics;
using System.Reflection;
using DiscUtils.Streams;

namespace DiscUtils.Common
{
    public abstract class ProgramBase
    {
        private CommandLineParser _parser;
        private CommandLineSwitch _outFormatSwitch;
        private CommandLineEnumSwitch<GenericDiskAdapterType> _adapterTypeSwitch;
        private CommandLineSwitch _userNameSwitch;
        private CommandLineSwitch _passwordSwitch;
        private CommandLineSwitch _partitionSwitch;
        private CommandLineSwitch _volumeIdSwitch;
        private CommandLineSwitch _diskSizeSwitch;
        private CommandLineSwitch _filenameEncodingSwitch;
        private CommandLineSwitch _helpSwitch;
        private CommandLineSwitch _quietSwitch;
        private CommandLineSwitch _verboseSwitch;
        private CommandLineSwitch _timeSwitch;

        private string _userName;
        private string _password;
        private string _outputDiskType;
        private string _outputDiskVariant;
        private GenericDiskAdapterType _adapterType;
        private int _partition = -1;
        private string _volumeId;
        private long _diskSize;

        protected ProgramBase()
        {
        }

        protected string UserName
        {
            get { return _userName; }
        }

        protected string Password
        {
            get { return _password; }
        }

        protected string OutputDiskType
        {
            get { return _outputDiskType; }
        }

        protected string OutputDiskVariant
        {
            get { return _outputDiskVariant; }
        }

        protected GenericDiskAdapterType AdapterType
        {
            get { return _adapterType; }
        }

        protected bool Quiet
        {
            get { return _quietSwitch.IsPresent; }
        }

        protected bool Verbose
        {
            get { return _verboseSwitch.IsPresent; }
        }

        protected int Partition
        {
            get { return _partition; }
        }

        protected string VolumeId
        {
            get { return _volumeId; }
        }

        protected long DiskSize
        {
            get { return _diskSize; }
        }

        protected VirtualDiskParameters DiskParameters
        {
            get
            {
                return new VirtualDiskParameters()
                {
                    AdapterType = AdapterType,
                    Capacity = DiskSize,
                };
            }
        }

        protected FileSystemParameters FileSystemParameters
        {
            get
            {
                return new FileSystemParameters()
                {
                    FileNameEncoding = (_filenameEncodingSwitch != null && _filenameEncodingSwitch.IsPresent) ? Encoding.GetEncoding(_filenameEncodingSwitch.Value) : null,
                };
            }
        }

        protected abstract StandardSwitches DefineCommandLine(CommandLineParser parser);
        protected virtual string[] HelpRemarks { get { return new string[] { }; } }
        protected abstract void DoRun();

        protected void Run(string[] args)
        {
            _parser = new CommandLineParser(ExeName);

            StandardSwitches stdSwitches = DefineCommandLine(_parser);

            if ((stdSwitches & StandardSwitches.OutputFormatAndAdapterType) != 0)
            {
                _outFormatSwitch = OutputFormatSwitch();
                _adapterTypeSwitch = new CommandLineEnumSwitch<GenericDiskAdapterType>("a", "adaptortype", "type", GenericDiskAdapterType.Ide, "Some disk formats encode the disk type (IDE or SCSI) into the disk image, this parameter specifies the type of adaptor to encode.");

                _parser.AddSwitch(_outFormatSwitch);
                _parser.AddSwitch(_adapterTypeSwitch);
            }

            if ((stdSwitches & StandardSwitches.DiskSize) != 0)
            {
                _diskSizeSwitch = new CommandLineSwitch("sz", "size", "size", "The size of the output disk.  Use B, KB, MB, GB to specify units (units default to bytes if not specified).");
                _parser.AddSwitch(_diskSizeSwitch);
            }

            if ((stdSwitches & StandardSwitches.FileNameEncoding) != 0)
            {
                _filenameEncodingSwitch = new CommandLineSwitch(new string[]{"ne"}, "nameencoding", "encoding", "The encoding used for filenames in the file system (aka the codepage), e.g. UTF-8 or IBM437.  This is ignored for file systems have fixed/defined encodings.");
                _parser.AddSwitch(_filenameEncodingSwitch);
            }

            if ((stdSwitches & StandardSwitches.PartitionOrVolume) != 0)
            {
                _partitionSwitch = new CommandLineSwitch("p", "partition", "num", "The number of the partition to inspect, in the range 0-n.  If not specified, 0 (the first partition) is the default.");
                _volumeIdSwitch = new CommandLineSwitch("v", "volume", "id", "The volume id of the volume to access, use the VolInfo tool to discover this id.  If specified, the partition parameter is ignored.");

                _parser.AddSwitch(_partitionSwitch);
                _parser.AddSwitch(_volumeIdSwitch);
            }

            if ((stdSwitches & StandardSwitches.UserAndPassword) != 0)
            {
                _userNameSwitch = new CommandLineSwitch("u", "user", "user_name", "If using an iSCSI source or target, optionally use this parameter to specify the user name to authenticate with.  If this parameter is specified without a password, you will be prompted to supply the password.");
                _parser.AddSwitch(_userNameSwitch);
                _passwordSwitch = new CommandLineSwitch("pw", "password", "secret", "If using an iSCSI source or target, optionally use this parameter to specify the password to authenticate with.");
                _parser.AddSwitch(_passwordSwitch);
            }

            if ((stdSwitches & StandardSwitches.Verbose) != 0)
            {
                _verboseSwitch = new CommandLineSwitch("v", "verbose", null, "Show detailed information.");
                _parser.AddSwitch(_verboseSwitch);
            }

            _helpSwitch = new CommandLineSwitch(new string[] { "h", "?" }, "help", null, "Show this help.");
            _parser.AddSwitch(_helpSwitch);
            _quietSwitch = new CommandLineSwitch("q", "quiet", null, "Run quietly.");
            _parser.AddSwitch(_quietSwitch);
            _timeSwitch = new CommandLineSwitch("time", null, "Times how long this program takes to execute.");
            _parser.AddSwitch(_timeSwitch);


            bool parseResult = _parser.Parse(args);

            if (!_quietSwitch.IsPresent)
            {
                DisplayHeader();
            }

            if (_helpSwitch.IsPresent || !parseResult)
            {
                DisplayHelp();
                return;
            }

            if ((stdSwitches & StandardSwitches.OutputFormatAndAdapterType) != 0)
            {
                if (_outFormatSwitch.IsPresent)
                {
                    string[] typeAndVariant = _outFormatSwitch.Value.Split(new char[] { '-' }, 2);
                    _outputDiskType = typeAndVariant[0];
                    _outputDiskVariant = (typeAndVariant.Length > 1) ? typeAndVariant[1] : "";
                }
                else
                {
                    DisplayHelp();
                    return;
                }

                if (_adapterTypeSwitch.IsPresent)
                {
                    _adapterType = _adapterTypeSwitch.EnumValue;
                }
                else
                {
                    _adapterType = GenericDiskAdapterType.Ide;
                }
            }

            if ((stdSwitches & StandardSwitches.DiskSize) != 0)
            {
                if (_diskSizeSwitch.IsPresent && !Utilities.TryParseDiskSize(_diskSizeSwitch.Value, out _diskSize))
                {
                    DisplayHelp();
                    return;
                }
            }

            if ((stdSwitches & StandardSwitches.PartitionOrVolume) != 0)
            {
                _partition = -1;
                if (_partitionSwitch.IsPresent && !int.TryParse(_partitionSwitch.Value, out _partition))
                {
                    DisplayHelp();
                    return;
                }

                _volumeId = _volumeIdSwitch.IsPresent ? _volumeIdSwitch.Value : null;
            }

            if ((stdSwitches & StandardSwitches.UserAndPassword) != 0)
            {
                _userName = null;

                if (_userNameSwitch.IsPresent)
                {
                    _userName = _userNameSwitch.Value;

                    if (_passwordSwitch.IsPresent)
                    {
                        _password = _passwordSwitch.Value;
                    }
                    else
                    {
                        _password = Utilities.PromptForPassword();
                    }
                }
            }

            if (_timeSwitch.IsPresent)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                DoRun();
                stopWatch.Stop();

                Console.WriteLine();
                Console.WriteLine("Time taken: {0}", stopWatch.Elapsed);
            }
            else
            {
                DoRun();
            }
        }

        protected void DisplayHelp()
        {
            _parser.DisplayHelp(HelpRemarks);
        }

        protected virtual void DisplayHeader()
        {
            Console.WriteLine("{0} v{1}, available from http://discutils.codeplex.com", ExeName, Version);
            Console.WriteLine("Copyright (c) Kenneth Bell, 2008-2013");
            Console.WriteLine("Free software issued under the MIT License, see LICENSE.TXT for details.");
            Console.WriteLine();
        }


        protected CommandLineParameter FileOrUriParameter(string paramName, string intro, bool optional)
        {
            return new CommandLineParameter(
                paramName,
                intro + "  " +
                "This can be a file path or an iSCSI, NFS or ODS URL.  " +
                "URLs for iSCSI LUNs are of the form: iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  " +
                "Use the iSCSIBrowse utility to discover iSCSI URLs.  " +
                "NFS URLs are of the form: nfs://host/a/path.vhd.  " +
                "ODS URLs are of the form: ods://domain/host/volumename.",
                optional);
        }

        protected CommandLineMultiParameter FileOrUriMultiParameter(string paramName, string intro, bool optional)
        {
            return new CommandLineMultiParameter(
                paramName,
                intro + "  " +
                "This can be a file path or an iSCSI, NFS or ODS URL.  " +
                "URLs for iSCSI LUNs are of the form: iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  " +
                "Use the iSCSIBrowse utility to discover iSCSI URLs.  " +
                "NFS URLs are of the form: nfs://host/a/path.vhd.  " +
                "ODS URLs are of the form: ods://domain/host/volumename.",
                optional);
        }

        protected static void ShowProgress(string label, long totalBytes, DateTime startTime, object sourceObject, PumpProgressEventArgs e)
        {
            int progressLen = 55 - label.Length;

            int numProgressChars = (int)((e.BytesRead * progressLen) / totalBytes);
            string progressBar = new string('=', numProgressChars) + new string(' ', progressLen - numProgressChars);

            DateTime now = DateTime.Now;
            TimeSpan timeSoFar = now - startTime;

            TimeSpan remaining = TimeSpan.FromMilliseconds((timeSoFar.TotalMilliseconds / (double)e.BytesRead) * (totalBytes - e.BytesRead));

            Console.Write("\r{0} ({1,3}%)  |{2}| {3:hh\\:mm\\:ss\\.f}", label, (e.BytesRead * 100) / totalBytes, progressBar, remaining, remaining.TotalHours, remaining.Minutes, remaining.Seconds, remaining.Milliseconds);
        }

        private CommandLineSwitch OutputFormatSwitch()
        {
            List<string> outputTypes = new List<string>();
            foreach (var type in VirtualDiskManager.SupportedDiskTypes)
            {
                List<string> variants = new List<string>(VirtualDisk.GetSupportedDiskVariants(type));
                if (variants.Count == 0)
                {
                    outputTypes.Add(type.ToUpperInvariant());
                }
                else
                {
                    foreach (var variant in variants)
                    {
                        outputTypes.Add(type.ToUpperInvariant() + "-" + variant.ToLowerInvariant());
                    }
                }
            }

            string[] ots = outputTypes.ToArray();
            Array.Sort(ots);

            return new CommandLineSwitch(
                "of",
                "outputFormat",
                "format",
                "Mandatory - the type of disk to output, one of " + string.Join(", ", ots, 0, ots.Length - 1) + " or " + ots[ots.Length - 1] + ".");
        }

        private string ExeName
        {
            get
            {
#if NET40
                return GetType().Assembly.GetName().Name;
#else
                return GetType().GetTypeInfo().Assembly.GetName().Name;
#endif
            }
        }

        private string Version
        {
            get
            {
#if NET40
                return GetType().Assembly.GetName().Version.ToString(3);
#else
                return GetType().GetTypeInfo().Assembly.GetName().Version.ToString(3);
#endif
            }
        }

        [Flags]
        protected internal enum StandardSwitches
        {
            Default = 0,
            UserAndPassword = 1,
            OutputFormatAndAdapterType = 2,
            Verbose = 4,
            PartitionOrVolume = 8,
            DiskSize = 16,
            FileNameEncoding = 32
        }
    }
}
