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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiscUtils.Internal;

namespace DiscUtils.Ewf.Section
{
    /// <summary>
    /// The Header2 section of the EWF file.
    /// </summary>
    public class Header2
    {
        /// <summary>
        /// The list of categories stored in the Header2 section.
        /// </summary>
        public List<Header2Category> Categories { get; set; }

        /// <summary>
        /// <para>Represents the Header2 section of the EWF file.</para>
        /// <para>Holds various meta-data about the acquisition.</para>
        /// </summary>
        /// <param name="bytes">The bytes that make up the Header2 section.</param>
        public Header2(byte[] bytes)
        {
            string header = null; // Will eventually hold the decompressed Header2 info
            #region Decompress zlib'd data
            {
                byte[] buff = new byte[1024];
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    using (DiscUtils.Compression.ZlibStream zs = new DiscUtils.Compression.ZlibStream(ms, System.IO.Compression.CompressionMode.Decompress, false))
                    {
                        using (MemoryStream decomp = new MemoryStream())
                        {
                            int n;
                            while ((n = zs.Read(buff, 0, buff.Length)) != 0)
                            {
                                decomp.Write(buff, 0, n);
                            }

                            decomp.Seek(0, SeekOrigin.Begin);
                            StreamReader sr = new StreamReader(decomp, Encoding.UTF8);
                            header = sr.ReadToEnd();
                        }
                    }
                }
            }
            #endregion

            string[] parts = header.Split(new char[] { (char)0x0A });

            int catsCount = int.Parse(parts[0]);
            Categories = new List<Header2Category>(catsCount);

            if (catsCount == 1) // EnCase 4
            {
                for (int i = 0; i < parts.Length; i++) // Header seems to have 0x0A on the end
                {
                    parts[i] = parts[i].TrimEnd('\r');
                }

                if (parts[1] != "main")
                {
                    throw new ArgumentException(string.Format("unexpected category: {0}", parts[1]));
                }

                Categories.Add(new Header2Category("main", parts[2], parts[3]));
            }
            else if (catsCount == 3) // EnCase 5-7
            {
                if (parts[1] != "main")
                {
                    throw new ArgumentException(string.Format("Unexpected category: {0}", parts[1]));
                }

                Categories.Add(new Header2Category("main", parts[2], parts[3]));

                if (parts[5] != "srce")
                {
                    throw new ArgumentException(string.Format("Unexpected category: {0}", parts[5]));
                }

                Categories.Add(new Header2Category("srce", parts[7], parts[9]));

                if (parts[11] != "sub")
                {
                    throw new ArgumentException(string.Format("Unexpected category: {0}", parts[13]));
                }

                Categories.Add(new Header2Category("sub", parts[13], parts[15]));
            }
            else
            {
                throw new ArgumentException(string.Format("Unknown category layout ({0} categories)", catsCount));
            }
        }

        /// <summary>
        /// Represents a Category within the Header2 section.
        /// </summary>
        public class Header2Category
        {
            /// <summary>
            /// The name of the Category.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// A Dictionary which maps a Category ID to its value.
            /// </summary>
            public Dictionary<string, string> Info { get; private set; }

            /// <summary>
            /// Creates an object to hold a particular category from within the Header2 section.
            /// </summary>
            /// <param name="name">The name of the category.</param>
            /// <param name="id">The tab-seperated list of IDs.</param>
            /// <param name="value">The tab seperated list of values.</param>
            public Header2Category(string name, string id, string value)
            {
                Name = name;
                string[] ids = id.Split('\t');
                string[] values = value.Split('\t');

                Info = new Dictionary<string, string>(ids.Length);
                for (int i = 0; i < ids.Length; i++)
                {
                    bool isDate;
                    string lookup = HeaderCodeLookup(ids[i], out isDate);
                    string val = values[i];

                    if (isDate)
                    {
                        try
                        {
                            if (val.Length == 10) // Unix timestamp
                            {
                                val = Utilities.DateTimeFromUnix(uint.Parse(val)).ToString();
                            }
                            else
                            {
                                string[] dateParts = val.Split(' ');
                                if (dateParts.Length == 6)
                                {
                                    int[] intParts = new int[6];
                                    for (int ip = 0; ip < 6; ip++)
                                    {
                                        intParts[ip] = int.Parse(dateParts[ip]);
                                    }

                                    val = new DateTime(intParts[0], intParts[1], intParts[2], intParts[3], intParts[4], intParts[5]).ToString();
                                }
                            }
                        }
                        catch (Exception)
                        { } // Do nothing, leave as txt.
                    }

                    Info.Add(lookup, val);
                }
            }

            private string HeaderCodeLookup(string code, out bool isDate)
            {
                isDate = false;
                string result = code;

                switch (code)
                {
                    case "a":
                        result = "Unique_Description";
                        break;

                    case "ah":
                        result = "Acquisition_Hash";
                        break;

                    case "aq": // Different from 'm'?
                        isDate = true;
                        result = "Acquisition_Date";
                        break;

                    case "av":
                        result = "Acquisition_Tool";
                        break;

                    case "c":
                        result = "Case_Number";
                        break;

                    case "co":
                        result = "Comment";
                        break;

                    case "e":
                        result = "Examiner_Name";
                        break;

                    case "ev": // Different from 'n'?
                        result = "Process_Extents";
                        break;

                    case "ext":
                        result = "Process_Extents";
                        break;

                    case "gu":
                        result = "GUID";
                        break;

                    case "id":
                        result = "Identifier";
                        break;

                    case "l": // L
                        result = "Device_Label";
                        break;

                    case "lo":
                        result = "Logical_Offset";
                        break;

                    case "m":
                        isDate = true;
                        result = "Acquisition_Date";
                        break;

                    case "md":
                        result = "Model";
                        break;

                    case "n":
                        result = "Evidence_Number";
                        break;

                    case "nu":
                        result = "Number";
                        break;

                    case "ov":
                        result = "Acquisition_OS";
                        break;

                    case "p":
                        result = "Password_Hash";
                        break;

                    case "pid":
                        result = "Process_Identifier";
                        break;

                    case "po":
                        result = "Physical_Offset";
                        break;

                    case "r":
                        result = "char";
                        break;

                    case "sn":
                        result = "Serial_Number";
                        break;

                    case "t":
                        result = "Notes";
                        break;

                    case "tb":
                        result = "Total_Bytes";
                        break;

                    case "u":
                        isDate = true;
                        result = "System_Date";
                        break;
                }

                return result;
            }
        }
    }
}
