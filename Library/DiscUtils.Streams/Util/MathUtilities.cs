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
    public static class MathUtilities
    {
        /// <summary>
        /// Round up a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round up.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-up value.</returns>
        public static long RoundUp(long value, long unit)
        {
            return (value + (unit - 1)) / unit * unit;
        }

        /// <summary>
        /// Round up a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round up.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-up value.</returns>
        public static int RoundUp(int value, int unit)
        {
            return (value + (unit - 1)) / unit * unit;
        }

        /// <summary>
        /// Round down a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round down.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-down value.</returns>
        public static long RoundDown(long value, long unit)
        {
            return value / unit * unit;
        }

        /// <summary>
        /// Calculates the CEIL function.
        /// </summary>
        /// <param name="numerator">The value to divide.</param>
        /// <param name="denominator">The value to divide by.</param>
        /// <returns>The value of CEIL(numerator/denominator).</returns>
        public static int Ceil(int numerator, int denominator)
        {
            return (numerator + (denominator - 1)) / denominator;
        }

        /// <summary>
        /// Calculates the CEIL function.
        /// </summary>
        /// <param name="numerator">The value to divide.</param>
        /// <param name="denominator">The value to divide by.</param>
        /// <returns>The value of CEIL(numerator/denominator).</returns>
        public static uint Ceil(uint numerator, uint denominator)
        {
            return (numerator + (denominator - 1)) / denominator;
        }

        /// <summary>
        /// Calculates the CEIL function.
        /// </summary>
        /// <param name="numerator">The value to divide.</param>
        /// <param name="denominator">The value to divide by.</param>
        /// <returns>The value of CEIL(numerator/denominator).</returns>
        public static long Ceil(long numerator, long denominator)
        {
            return (numerator + (denominator - 1)) / denominator;
        }

        public static int Log2(uint val)
        {
            if (val == 0)
            {
                throw new ArgumentException("Cannot calculate log of Zero", nameof(val));
            }

            int result = 0;
            while ((val & 1) != 1)
            {
                val >>= 1;
                ++result;
            }

            if (val == 1)
            {
                return result;
            }
            throw new ArgumentException("Input is not a power of Two", nameof(val));
        }

        public static int Log2(int val)
        {
            if (val == 0)
            {
                throw new ArgumentException("Cannot calculate log of Zero", nameof(val));
            }

            int result = 0;
            while ((val & 1) != 1)
            {
                val >>= 1;
                ++result;
            }

            if (val == 1)
            {
                return result;
            }
            throw new ArgumentException("Input is not a power of Two", nameof(val));
        }
    }
}
