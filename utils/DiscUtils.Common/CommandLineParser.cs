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

namespace DiscUtils.Common
{
    public class CommandLineParser
    {
        private string _utilityName;
        private List<CommandLineSwitch> _switches;
        private List<CommandLineParameter> _params;

        private bool _parseFailed;

        public CommandLineParser(string utilityName)
        {
            _utilityName = utilityName;
            _switches = new List<CommandLineSwitch>();
            _params = new List<CommandLineParameter>();
            _parseFailed = false;
        }

        public void AddParameter(CommandLineParameter parameter)
        {
            _params.Add(parameter);
        }

        public void AddSwitch(CommandLineSwitch clSwitch)
        {
            _switches.Add(clSwitch);
        }

        public void DisplayHelp()
        {
            Console.Write("{0}", _utilityName);
            if (_switches.Count > 0)
            {
                Console.Write(" <switches>");
            }
            foreach (CommandLineParameter el in _params)
            {
                Console.Write(" " + el.CommandLineText);
            }
            Console.WriteLine();
            Console.WriteLine();


            Console.WriteLine("Parameters:");

            int maxNameLen = 0;
            foreach (CommandLineParameter p in _params)
            {
                maxNameLen = Math.Max(maxNameLen, p.NameDisplayLength);
            }

            foreach (CommandLineParameter p in _params)
            {
                p.WriteDescription(Console.Out, "  {0,-" + maxNameLen + "}  {1}", 70 - maxNameLen);
                Console.WriteLine();
            }


            Console.WriteLine("Switches:");

            int maxSwitchLen = 0;
            foreach (CommandLineSwitch s in _switches)
            {
                maxSwitchLen = Math.Max(maxSwitchLen, s.SwitchDisplayLength);
            }

            foreach (CommandLineSwitch s in _switches)
            {
                s.WriteDescription(Console.Out, "  {0,-" + maxSwitchLen + "}  {1}", 70 - maxSwitchLen);
                Console.WriteLine();
            }
        }

        public bool Parse(string[] args)
        {
            _parseFailed = true;

            int i = 0;
            int paramIdx = 0;

            while (i < args.Length)
            {
                if (args[i].StartsWith("-") || args[i].StartsWith("/"))
                {
                    string switchName = args[i].Substring(1);
                    bool foundMatch = false;

                    foreach (CommandLineSwitch s in _switches)
                    {
                        if (s.Matches(switchName))
                        {
                            i = s.Process(args, i + 1);
                            foundMatch = true;
                            break;
                        }
                    }

                    if (!foundMatch)
                    {
                        throw new Exception(string.Format("Unrecognized command-line switch '{0}'", args[i]));
                    }

                }
                else
                {
                    if (paramIdx < _params.Count && (!_params[paramIdx].IsOptional || _params[paramIdx].Matches(args[i])))
                    {
                        i = _params[paramIdx].Process(args, i);
                        ++paramIdx;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // Check mandatory params present & all params valid.
            for (int j = 0; j < _params.Count; ++j)
            {
                if (!_params[j].IsValid)
                {
                    return false;
                }

                if (!_params[j].IsOptional && !_params[j].IsPresent)
                {
                    return false;
                }
            }

            _parseFailed = false;
            return true;
        }

        public bool ParseSucceeded
        {
            get { return !_parseFailed; }
        }
    }
}
