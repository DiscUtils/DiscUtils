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
using System.Collections.Generic;
using System.Text;

namespace DiscUtils.Common
{
    public abstract class ProgramBase
    {
        private CommandLineParser _parser;
        private CommandLineSwitch _outFormatSwitch;
        private CommandLineSwitch _userNameSwitch;
        private CommandLineSwitch _passwordSwitch;
        private CommandLineSwitch _partitionSwitch;
        private CommandLineSwitch _volumeIdSwitch;
        private CommandLineSwitch _diskSizeSwitch;
        private CommandLineSwitch _helpSwitch;
        private CommandLineSwitch _quietSwitch;
        private CommandLineSwitch _verboseSwitch;

        private string _userName;
        private string _password;
        private string _outputDiskType;
        private string _outputDiskVariant;
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

        protected abstract StandardSwitches DefineCommandLine(CommandLineParser parser);
        protected virtual string[] HelpRemarks { get { return new string[] { }; } }
        protected abstract void DoRun();

        protected void Run(string[] args)
        {
            _parser = new CommandLineParser(ExeName);

            StandardSwitches stdSwitches = DefineCommandLine(_parser);

            if ((stdSwitches & StandardSwitches.OutputFormat) != 0)
            {
                _outFormatSwitch = OutputFormatSwitch();
                _parser.AddSwitch(_outFormatSwitch);
            }

            if ((stdSwitches & StandardSwitches.DiskSize) != 0)
            {
                _diskSizeSwitch = new CommandLineSwitch("sz", "size", "size", "The size of the output disk.  Use B, KB, MB, GB to specify units (units default to bytes if not specified).");
                _parser.AddSwitch(_diskSizeSwitch);
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

            if ((stdSwitches & StandardSwitches.OutputFormat) != 0)
            {
                if (_outFormatSwitch.IsPresent)
                {
                    string[] typeAndVariant = _outFormatSwitch.Value.Split(new char[] { '-' }, 2);
                    _outputDiskType = typeAndVariant[0];
                    _outputDiskVariant = typeAndVariant[1];
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

            DoRun();
        }

        protected void DisplayHelp()
        {
            _parser.DisplayHelp(HelpRemarks);
        }

        private void DisplayHeader()
        {
            Console.WriteLine("{0} v{1}, available from http://discutils.codeplex.com", ExeName, Version);
            Console.WriteLine("Copyright (c) Kenneth Bell, 2008-2009");
            Console.WriteLine("Free software issued under the MIT License, see LICENSE.TXT for details.");
            Console.WriteLine();
        }


        protected CommandLineParameter FileOrUriParameter(string paramName, string intro, bool optional)
        {
            return new CommandLineParameter(
                paramName,
                intro + "  " +
                "This can be a file path, or an iSCSI or NFS URL.  " +
                "URLs for iSCSI LUNs are of the form: iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  " +
                "Use the iSCSIBrowse utility to discover iSCSI URLs.  " +
                "NFS URLs are of the form: nfs://host/a/path.vhd.",
                optional);
        }

        protected CommandLineMultiParameter FileOrUriMultiParameter(string paramName, string intro, bool optional)
        {
            return new CommandLineMultiParameter(
                paramName,
                intro + "  " +
                "Values can be a file path, or an iSCSI or NFS URL.  " +
                "URLs for iSCSI LUNs are of the form: iscsi://192.168.1.2/iqn.2002-2004.example.com:port1?LUN=2.  " +
                "Use the iSCSIBrowse utility to discover iSCSI URLs.  " +
                "NFS URLs are of the form: nfs://host/a/path.vhd.",
                optional);
        }

        private CommandLineSwitch OutputFormatSwitch()
        {
            List<string> outputTypes = new List<string>();
            foreach (var type in VirtualDisk.SupportedDiskTypes)
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
                "The type of disk to output, one of " + string.Join(", ", ots, 0, ots.Length - 1) + " or " + ots[ots.Length - 1] + ".");
        }

        private string ExeName
        {
            get { return GetType().Assembly.GetName().Name; }
        }

        private string Version
        {
            get { return GetType().Assembly.GetName().Version.ToString(3); }
        }

        [Flags]
        protected internal enum StandardSwitches
        {
            Default = 0,
            UserAndPassword = 1,
            OutputFormat = 2,
            Verbose = 4,
            PartitionOrVolume = 8,
            DiskSize = 16
        }
    }
}
