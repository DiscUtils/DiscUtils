//
// Copyright (c) 2008, Kenneth Bell
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

namespace DiscUtils.Fat
{
    internal class FatUtilities
    {
        public static string NormalizeFileName(string name)
        {
            string[] parts = name.Split('.');

            if (parts.Length < 1 || parts.Length > 2)
            {
                throw new ArgumentException("Invalid file name", "name");
            }

            string namePart = parts[0];
            string extPart = (parts.Length == 2 ? parts[1] : "");

            if (namePart.Length > 8 || extPart.Length > 3)
            {
                throw new ArgumentException("Invalid file name", "name");
            }

            return String.Format("{0,-8}{1,-3}", namePart.ToUpperInvariant(), extPart.ToUpperInvariant());
        }
    }
}
