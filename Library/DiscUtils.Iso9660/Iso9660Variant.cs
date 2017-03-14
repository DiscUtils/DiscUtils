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

namespace DiscUtils.Iso9660
{
    /// <summary>
    /// Enumeration of known file system variants.
    /// </summary>
    /// <remarks>
    /// <para>ISO9660 has a number of significant limitations, and over time
    /// multiple schemes have been devised for extending the standard
    /// to support the richer file system semantics typical of most modern
    /// operating systems.  These variants differ functionally and (in the
    /// case of RockRidge) may represent a logically different directory
    /// hierarchy to that encoded in the vanilla iso9660 standard.</para>
    /// <para>Use this enum to control which variants to honour / prefer
    /// when accessing an ISO image.</para>
    /// </remarks>
    public enum Iso9660Variant
    {
        /// <summary>
        /// No known variant.
        /// </summary>
        None,

        /// <summary>
        /// Vanilla ISO9660.
        /// </summary>
        Iso9660,

        /// <summary>
        /// Joliet file system (Windows).
        /// </summary>
        Joliet,

        /// <summary>
        /// Rock Ridge (Unix).
        /// </summary>
        RockRidge
    }
}