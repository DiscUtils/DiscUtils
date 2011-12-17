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

namespace DiscUtils.Dmg
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;

    internal static class Plist
    {
        internal static Dictionary<string, object> Parse(Stream stream)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.XmlResolver = null;
            xmlDoc.Load(stream);

            var root = xmlDoc.DocumentElement;
            if (root.Name != "plist")
            {
                throw new InvalidDataException("XML document is not a plist");
            }

            return ParseDictionary(root.FirstChild);
        }

        private static object ParseNode(XmlNode xmlNode)
        {
            switch (xmlNode.Name)
            {
                case "dict":
                    return ParseDictionary(xmlNode);
                case "array":
                    return ParseArray(xmlNode);
                case "string":
                    return ParseString(xmlNode);
                case "data":
                    return ParseData(xmlNode);
                default:
                    throw new NotImplementedException();
            }
        }

        private static Dictionary<string, object> ParseDictionary(XmlNode xmlNode)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            XmlNode focusNode = xmlNode.FirstChild;
            while (focusNode != null)
            {
                if (focusNode.Name != "key")
                {
                    throw new InvalidDataException("Invalid plist, expected dictionary key");
                }

                string key = focusNode.InnerText;

                focusNode = focusNode.NextSibling;

                result.Add(key, ParseNode(focusNode));

                focusNode = focusNode.NextSibling;
            }

            return result;
        }

        private static object ParseArray(XmlNode xmlNode)
        {
            List<object> result = new List<object>();

            XmlNode focusNode = xmlNode.FirstChild;
            while (focusNode != null)
            {
                result.Add(ParseNode(focusNode));
                focusNode = focusNode.NextSibling;
            }

            return result;
        }

        private static object ParseString(XmlNode xmlNode)
        {
            return xmlNode.InnerText;
        }

        private static object ParseData(XmlNode xmlNode)
        {
            string base64 = xmlNode.InnerText;
            return Convert.FromBase64String(base64);
        }
    }
}
