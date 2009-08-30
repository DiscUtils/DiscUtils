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
using System.IO;
using System.Globalization;

namespace DiscUtils.Common
{
    public class Utilities
    {
        public static string[] WordWrap(string text, int width)
        {
            List<string> lines = new List<string>();
            int pos = 0;

            while (pos < text.Length - width)
            {
                int start = Math.Min(pos + width, text.Length - 1);
                int count = start - pos;

                int breakPos = text.LastIndexOf(' ', start, count);

                lines.Add(text.Substring(pos, breakPos - pos).TrimEnd(' '));

                while (breakPos < text.Length && text[breakPos] == ' ')
                {
                    breakPos++;
                }
                pos = breakPos;
            }

            lines.Add(text.Substring(pos));

            return lines.ToArray();
        }

        public static string PromptForPassword()
        {
            Console.WriteLine();
            Console.Write("Password: ");

            ConsoleColor restoreColor = Console.ForegroundColor;
            Console.ForegroundColor = Console.BackgroundColor;
            try
            {
                return Console.ReadLine();
            }
            finally
            {
                Console.ForegroundColor = restoreColor;
            }
        }

        public static VirtualDisk OpenDisk(string path, FileAccess access)
        {
            VirtualDisk result = VirtualDisk.OpenDisk(path, access);
            if (result == null)
            {
                throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "{0} is not a recognised virtual disk type", path));
            }

            return result;
        }

        public static SparseStream OpenVolume(VirtualDisk disk, int partition)
        {
            if (disk.IsPartitioned)
            {
                return disk.Partitions[partition].Open();
            }
            else
            {
                if (partition != 0)
                {
                    throw new ArgumentException("Attempt to open partition on unpartitioned disk");
                }

                return new SnapshotStream(disk.Content, Ownership.None);
            }
        }

        public static void ShowHeader(Type program)
        {
            Console.WriteLine("{0} v{1}, available from http://discutils.codeplex.com", GetExeName(program), GetVersion(program));
            Console.WriteLine("Copyright (c) Kenneth Bell, 2008-2009");
            Console.WriteLine("Free software issued under the MIT License, see LICENSE.TXT for details.");
            Console.WriteLine();
        }

        public static string GetExeName(Type program)
        {
            return program.Assembly.GetName().Name;
        }

        public static string GetVersion(Type program)
        {
            return program.Assembly.GetName().Version.ToString(3);
        }
    }
}
