//
// Copyright (c) 2013, Adam Bridge
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

namespace DiscUtils.Ewf.Section
{
    /// <summary>
    /// The Digest section from the EWF file.
    /// </summary>
    public class Digest
    {
        /// <summary>
        /// The MD5 sum of the acquired data.
        /// </summary>
        public string MD5 { get; private set; }

        /// <summary>
        /// The SHA1 sum of the acquired data.
        /// </summary>
        public string SHA1 { get; private set; }

        /// <summary>
        /// Creates a new Digest object from bytes.
        /// </summary>
        /// <param name="bytes">The bytes which make up the Digest section.</param>
        public Digest(byte[] bytes)
        {
            MD5 = Utils.ByteArrayToByteString(bytes, 0, 16);
            SHA1 = Utils.ByteArrayToByteString(bytes, 16, 20);
        }
    }
}
