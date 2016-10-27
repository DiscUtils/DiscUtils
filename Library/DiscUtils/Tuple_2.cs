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
    using System;

    internal class Tuple<A, B> : Tuple
    {
        private A _a;
        private B _b;

        public Tuple(A a, B b)
        {
            _a = a;
            _b = b;
        }

        public A First
        {
            get { return _a; }
        }

        public B Second
        {
            get { return _b; }
        }

        public override object this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return _a;
                    case 1: return _b;
                    default: throw new ArgumentOutOfRangeException("i", i, "Invalid index");
                }
            }
        }

        public override bool Equals(object obj)
        {
            Tuple<A, B> asType = obj as Tuple<A, B>;
            if (asType == null)
            {
                return false;
            }

            return Equals(_a, asType._a) && Equals(_b, asType._b);
        }

        public override int GetHashCode()
        {
            return ((_a == null) ? 0x14AB32BC : _a.GetHashCode()) ^ ((_b == null) ? 0x65BC32DE : _b.GetHashCode());
        }
    }
}
