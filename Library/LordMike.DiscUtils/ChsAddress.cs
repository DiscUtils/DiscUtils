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

namespace DiscUtils
{
    /// <summary>
    /// Class whose instances represent a CHS (Cylinder, Head, Sector) address on a disk.
    /// </summary>
    /// <remarks>Instances of this class are immutable.</remarks>
    public sealed class ChsAddress
    {
        /// <summary>
        /// The address of the first sector on any disk.
        /// </summary>
        public static readonly ChsAddress First = new ChsAddress(0, 0, 1);

        /// <summary>
        /// Initializes a new instance of the ChsAddress class.
        /// </summary>
        /// <param name="cylinder">The number of cylinders of the disk.</param>
        /// <param name="head">The number of heads (aka platters) of the disk.</param>
        /// <param name="sector">The number of sectors per track/cylinder of the disk.</param>
        public ChsAddress(int cylinder, int head, int sector)
        {
            Cylinder = cylinder;
            Head = head;
            Sector = sector;
        }

        /// <summary>
        /// Gets the cylinder number (zero-based).
        /// </summary>
        public int Cylinder { get; }

        /// <summary>
        /// Gets the head (zero-based).
        /// </summary>
        public int Head { get; }

        /// <summary>
        /// Gets the sector number (one-based).
        /// </summary>
        public int Sector { get; }

        /// <summary>
        /// Determines if this object is equivalent to another.
        /// </summary>
        /// <param name="obj">The object to test against.</param>
        /// <returns><c>true</c> if the <paramref name="obj"/> is equivalent, else <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            ChsAddress other = (ChsAddress)obj;

            return Cylinder == other.Cylinder && Head == other.Head && Sector == other.Sector;
        }

        /// <summary>
        /// Calculates the hash code for this object.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            return Cylinder.GetHashCode() ^ Head.GetHashCode() ^ Sector.GetHashCode();
        }

        /// <summary>
        /// Gets a string representation of this object, in the form (C/H/S).
        /// </summary>
        /// <returns>The string representation.</returns>
        public override string ToString()
        {
            return "(" + Cylinder + "/" + Head + "/" + Sector + ")";
        }
    }
}