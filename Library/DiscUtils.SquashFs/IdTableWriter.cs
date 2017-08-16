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
using DiscUtils.Streams;

namespace DiscUtils.SquashFs
{
    internal sealed class IdTableWriter
    {
        private readonly BuilderContext _context;

        private readonly List<int> _ids;

        public IdTableWriter(BuilderContext context)
        {
            _context = context;
            _ids = new List<int>();
        }

        public int IdCount
        {
            get { return _ids.Count; }
        }

        /// <summary>
        /// Allocates space for a User / Group id.
        /// </summary>
        /// <param name="id">The id to allocate.</param>
        /// <returns>The key of the id.</returns>
        public ushort AllocateId(int id)
        {
            for (int i = 0; i < _ids.Count; ++i)
            {
                if (_ids[i] == id)
                {
                    return (ushort)i;
                }
            }

            _ids.Add(id);
            return (ushort)(_ids.Count - 1);
        }

        internal long Persist()
        {
            if (_ids.Count <= 0)
            {
                return -1;
            }

            if (_ids.Count * 4 > _context.DataBlockSize)
            {
                throw new NotImplementedException("Large numbers of user / group id's");
            }

            for (int i = 0; i < _ids.Count; ++i)
            {
                EndianUtilities.WriteBytesLittleEndian(_ids[i], _context.IoBuffer, i * 4);
            }

            // Persist the actual Id's
            long blockPos = _context.RawStream.Position;
            MetablockWriter writer = new MetablockWriter();
            writer.Write(_context.IoBuffer, 0, _ids.Count * 4);
            writer.Persist(_context.RawStream);

            // Persist the table that references the block containing the id's
            long tablePos = _context.RawStream.Position;
            byte[] tableBuffer = new byte[8];
            EndianUtilities.WriteBytesLittleEndian(blockPos, tableBuffer, 0);
            _context.RawStream.Write(tableBuffer, 0, 8);

            return tablePos;
        }
    }
}