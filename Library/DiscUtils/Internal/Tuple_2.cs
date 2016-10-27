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
    internal class Tuple<A, B> : Tuple
    {
        public A Item1 { get; }
        public B Item2 { get; }

        public Tuple(A item1, B item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
        
        public override bool Equals(object obj)
        {
            Tuple<A, B> asType = obj as Tuple<A, B>;
            if (asType == null)
            {
                return false;
            }

            return Equals(Item1, asType.Item1) && Equals(Item2, asType.Item2);
        }

        public override int GetHashCode()
        {
            return ((Item1 == null) ? 0x14AB32BC : Item1.GetHashCode()) ^ ((Item2 == null) ? 0x65BC32DE : Item2.GetHashCode());
        }
    }
}