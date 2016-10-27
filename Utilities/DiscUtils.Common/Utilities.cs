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

namespace DiscUtils.Common
{
    public static class Utilities
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

        public static string ApproximateDiskSize(long size)
        {
            if (size > 10 * (1024 * 1024L * 1024))
            {
                return (size / (1024 * 1024 * 1024)) + " GiB";
            }
            else if (size > 10 * (1024 * 1024L))
            {
                return (size / (1024 * 1024)) + " MiB";
            }
            else if (size > 10 * 1024)
            {
                return (size / 1024) + " KiB";
            }
            else
            {
                return size + " B";
            }
        }

        public static bool TryParseDiskSize(string size, out long value)
        {
            char lastChar = size[size.Length - 1];
            if (Char.IsDigit(lastChar))
            {
                return long.TryParse(size, out value);
            }
            else if (lastChar == 'B' && size.Length >= 2)
            {
                char unitChar = size[size.Length - 2];

                // suffix is 'B', indicating bytes
                if (Char.IsDigit(unitChar))
                {
                    return long.TryParse(size.Substring(0, size.Length - 1), out value);
                }

                // suffix is KB, MB or GB
                long quantity;
                if (!long.TryParse(size.Substring(0, size.Length - 2), out quantity))
                {
                    value = 0;
                    return false;
                }

                switch (unitChar)
                {
                    case 'K':
                        value = quantity * 1024L;
                        return true;
                    case 'M':
                        value = quantity * 1024L * 1024L;
                        return true;
                    case 'G':
                        value = quantity * 1024L * 1024L * 1024L;
                        return true;
                    default:
                        value = 0;
                        return false;
                }
            }
            else
            {
                value = 0;
                return false;
            }
        }
    }
}
