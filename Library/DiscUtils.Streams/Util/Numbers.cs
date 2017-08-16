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

namespace DiscUtils.Streams
{
    internal static class Numbers<T>
        where T : struct, IComparable<T>, IEquatable<T>
    {
        public delegate bool ComparisonFn(T a, T b);

        public delegate T ConvertIntFn(int a);

        public delegate T ConvertLongFn(long a);

        public delegate T DualParamFn(T a, T b);

        public delegate T NoParamFn();

        public static readonly T Zero = default(T);
        public static readonly T One = GetOne();
        public static readonly DualParamFn Add = GetAdd();
        public static readonly DualParamFn Subtract = GetSubtract();
        public static readonly DualParamFn Multiply = GetMultiply();
        public static readonly DualParamFn Divide = GetDivide();
        public static readonly DualParamFn RoundUp = GetRoundUp();
        public static readonly DualParamFn RoundDown = GetRoundDown();
        public static readonly DualParamFn Ceil = GetCeil();
        public static readonly ConvertLongFn ConvertLong = GetConvertLong();
        public static readonly ConvertIntFn ConvertInt = GetConvertInt();

        public static bool GreaterThan(T a, T b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool GreaterThanOrEqual(T a, T b)
        {
            return a.CompareTo(b) >= 0;
        }

        public static bool LessThan(T a, T b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool LessThanOrEqual(T a, T b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool Equal(T a, T b)
        {
            return a.CompareTo(b) == 0;
        }

        public static bool NotEqual(T a, T b)
        {
            return a.CompareTo(b) != 0;
        }

        private static T GetOne()
        {
            if (typeof(T) == typeof(long))
            {
                return ((NoParamFn)(object)new LongNoParamFn(() => { return 1; }))();
            }
            if (typeof(T) == typeof(int))
            {
                return ((NoParamFn)(object)new IntNoParamFn(() => { return 1; }))();
            }
            throw new NotSupportedException();
        }

        private static ConvertLongFn GetConvertLong()
        {
            if (typeof(T) == typeof(long))
            {
                return (ConvertLongFn)(object)new LongConvertLongFn(x => { return x; });
            }
            if (typeof(T) == typeof(int))
            {
                return (ConvertLongFn)(object)new IntConvertLongFn(x => { return (int)x; });
            }
            throw new NotSupportedException();
        }

        private static ConvertIntFn GetConvertInt()
        {
            if (typeof(T) == typeof(long))
            {
                return (ConvertIntFn)(object)new LongConvertIntFn(x => { return x; });
            }
            if (typeof(T) == typeof(int))
            {
                return (ConvertIntFn)(object)new IntConvertIntFn(x => { return x; });
            }
            throw new NotSupportedException();
        }

        private static DualParamFn GetAdd()
        {
            if (typeof(T) == typeof(long))
            {
                return (DualParamFn)(object)new LongDualParamFn((a, b) => { return a + b; });
            }
            if (typeof(T) == typeof(int))
            {
                return (DualParamFn)(object)new IntDualParamFn((a, b) => { return a + b; });
            }
            throw new NotSupportedException();
        }

        private static DualParamFn GetSubtract()
        {
            if (typeof(T) == typeof(long))
            {
                return (DualParamFn)(object)new LongDualParamFn((a, b) => { return a - b; });
            }
            if (typeof(T) == typeof(int))
            {
                return (DualParamFn)(object)new IntDualParamFn((a, b) => { return a - b; });
            }
            throw new NotSupportedException();
        }

        private static DualParamFn GetMultiply()
        {
            if (typeof(T) == typeof(long))
            {
                return (DualParamFn)(object)new LongDualParamFn((a, b) => { return a * b; });
            }
            if (typeof(T) == typeof(int))
            {
                return (DualParamFn)(object)new IntDualParamFn((a, b) => { return a * b; });
            }
            throw new NotSupportedException();
        }

        private static DualParamFn GetDivide()
        {
            if (typeof(T) == typeof(long))
            {
                return (DualParamFn)(object)new LongDualParamFn((a, b) => { return a / b; });
            }
            if (typeof(T) == typeof(int))
            {
                return (DualParamFn)(object)new IntDualParamFn((a, b) => { return a / b; });
            }
            throw new NotSupportedException();
        }

        private static DualParamFn GetRoundUp()
        {
            if (typeof(T) == typeof(long))
            {
                return (DualParamFn)(object)new LongDualParamFn((a, b) => { return (a + b - 1) / b * b; });
            }
            if (typeof(T) == typeof(int))
            {
                return (DualParamFn)(object)new IntDualParamFn((a, b) => { return (a + b - 1) / b * b; });
            }
            throw new NotSupportedException();
        }

        private static DualParamFn GetRoundDown()
        {
            if (typeof(T) == typeof(long))
            {
                return (DualParamFn)(object)new LongDualParamFn((a, b) => { return a / b * b; });
            }
            if (typeof(T) == typeof(int))
            {
                return (DualParamFn)(object)new IntDualParamFn((a, b) => { return a / b * b; });
            }
            throw new NotSupportedException();
        }

        private static DualParamFn GetCeil()
        {
            if (typeof(T) == typeof(long))
            {
                return (DualParamFn)(object)new LongDualParamFn((a, b) => { return (a + b - 1) / b; });
            }
            if (typeof(T) == typeof(int))
            {
                return (DualParamFn)(object)new IntDualParamFn((a, b) => { return (a + b - 1) / b; });
            }
            throw new NotSupportedException();
        }

        private delegate long LongNoParamFn();

        private delegate long LongDualParamFn(long a, long b);

        private delegate long LongConvertLongFn(long x);

        private delegate long LongConvertIntFn(int x);

        private delegate int IntNoParamFn();

        private delegate int IntDualParamFn(int a, int b);

        private delegate int IntConvertLongFn(long x);

        private delegate int IntConvertIntFn(int x);
    }
}