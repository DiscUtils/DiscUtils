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

namespace DiscUtils.Compression
{
    internal sealed class InverseBurrowsWheeler : DataBlockTransform
    {
        private readonly int[] _nextPos;
        private readonly int[] _pointers;

        public InverseBurrowsWheeler(int bufferSize)
        {
            _pointers = new int[bufferSize];
            _nextPos = new int[256];
        }

        protected override bool BuffersMustNotOverlap
        {
            get { return true; }
        }

        public int OriginalIndex { get; set; }

        protected override int DoProcess(byte[] input, int inputOffset, int inputCount, byte[] output, int outputOffset)
        {
            int outputCount = inputCount;

            // First find the frequency of each value
            Array.Clear(_nextPos, 0, _nextPos.Length);
            for (int i = inputOffset; i < inputOffset + inputCount; ++i)
            {
                _nextPos[input[i]]++;
            }

            // We know they're 'sorted' in the first column, so now can figure
            // out the position of the first instance of each.
            int sum = 0;
            for (int i = 0; i < 256; ++i)
            {
                int tempSum = sum;
                sum += _nextPos[i];
                _nextPos[i] = tempSum;
            }

            // For each value in the final column, put a pointer to to the
            // 'next' character in the first (sorted) column.
            for (int i = 0; i < inputCount; ++i)
            {
                _pointers[_nextPos[input[inputOffset + i]]++] = i;
            }

            // The 'next' character after the end of the original string is the
            // first character of the original string.
            int focus = _pointers[OriginalIndex];

            // We can now just walk the pointers to reconstruct the original string
            for (int i = 0; i < outputCount; ++i)
            {
                output[outputOffset + i] = input[inputOffset + focus];
                focus = _pointers[focus];
            }

            return outputCount;
        }

        protected override int MaxOutputCount(int inputCount)
        {
            return inputCount;
        }

        protected override int MinOutputCount(int inputCount)
        {
            return inputCount;
        }
    }
}