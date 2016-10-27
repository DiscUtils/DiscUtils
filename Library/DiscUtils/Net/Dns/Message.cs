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

namespace DiscUtils.Net.Dns
{
    using System.Collections.Generic;

    internal class Message
    {
        private List<Question> _questions;
        private List<ResourceRecord> _answers;
        private List<ResourceRecord> _authorityRecords;
        private List<ResourceRecord> _additional;

        public Message()
        {
            _questions = new List<Question>();
            _answers = new List<ResourceRecord>();
            _authorityRecords = new List<ResourceRecord>();
            _additional = new List<ResourceRecord>();
        }

        public ushort TransactionId { get; set; }

        public MessageFlags Flags { get; set; }

        public List<Question> Questions
        {
            get { return _questions; }
        }

        public List<ResourceRecord> Answers
        {
            get { return _answers; }
        }

        public List<ResourceRecord> AuthorityRecords
        {
            get { return _authorityRecords; }
        }

        public List<ResourceRecord> AdditionalRecords
        {
            get { return _additional; }
        }

        public static Message Read(PacketReader reader)
        {
            Message result = new Message();

            result.TransactionId = reader.ReadUShort();
            result.Flags = new MessageFlags(reader.ReadUShort());

            ushort questions = reader.ReadUShort();
            ushort answers = reader.ReadUShort();
            ushort authorityRecords = reader.ReadUShort();
            ushort additionalRecords = reader.ReadUShort();

            for (int i = 0; i < questions; ++i)
            {
                result.Questions.Add(Question.ReadFrom(reader));
            }

            for (int i = 0; i < answers; ++i)
            {
                result.Answers.Add(ResourceRecord.ReadFrom(reader));
            }

            for (int i = 0; i < authorityRecords; ++i)
            {
                result.AuthorityRecords.Add(ResourceRecord.ReadFrom(reader));
            }

            for (int i = 0; i < additionalRecords; ++i)
            {
                result.AdditionalRecords.Add(ResourceRecord.ReadFrom(reader));
            }

            return result;
        }

        public void WriteTo(PacketWriter writer)
        {
            writer.Write(TransactionId);
            writer.Write(Flags.Value);
            writer.Write((ushort)_questions.Count);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            foreach (Question question in _questions)
            {
                question.WriteTo(writer);
            }
        }
    }
}
