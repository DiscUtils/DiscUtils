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
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace DiscUtils
{
    internal static class Plist
    {
        internal static Dictionary<string, object> Parse(Stream stream)
        {
            XmlDocument xmlDoc = new XmlDocument();
#if !NETCORE
            xmlDoc.XmlResolver = null;
#endif

            XmlReaderSettings settings = new XmlReaderSettings();
#if !NET20
            // DTD processing is disabled on anything but .NET 2.0, so this must be set to
            // Ignore.
            // See https://msdn.microsoft.com/en-us/magazine/ee335713.aspx for additional information.
            settings.DtdProcessing = DtdProcessing.Ignore;
#endif

            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                xmlDoc.Load(reader);
            }

            XmlElement root = xmlDoc.DocumentElement;
            if (root.Name != "plist")
            {
                throw new InvalidDataException("XML document is not a plist");
            }

            return ParseDictionary(root.FirstChild);
        }

        internal static void Write(Stream stream, Dictionary<string, object> plist)
        {
            XmlDocument xmlDoc = new XmlDocument();
#if !NETCORE
            xmlDoc.XmlResolver = null;
#endif

            XmlDeclaration xmlDecl = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDoc.AppendChild(xmlDecl);

#if !NETCORE
            XmlDocumentType xmlDocType = xmlDoc.CreateDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);
            xmlDoc.AppendChild(xmlDocType);
#endif

            XmlElement rootElement = xmlDoc.CreateElement("plist");
            rootElement.SetAttribute("Version", "1.0");
            xmlDoc.AppendChild(rootElement);

            xmlDoc.DocumentElement.SetAttribute("Version", "1.0");

            rootElement.AppendChild(CreateNode(xmlDoc, plist));

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = Encoding.UTF8;

            using (XmlWriter xw = XmlWriter.Create(stream, settings))
            {
                xmlDoc.Save(xw);
            }
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
                case "integer":
                    return ParseInteger(xmlNode);
                case "true":
                    return true;
                case "false":
                    return false;
                default:
                    throw new NotImplementedException();
            }
        }

        private static XmlNode CreateNode(XmlDocument xmlDoc, object obj)
        {
            if (obj is Dictionary<string, object>)
            {
                return CreateDictionary(xmlDoc, (Dictionary<string, object>)obj);
            }
            if (obj is string)
            {
                XmlText text = xmlDoc.CreateTextNode((string)obj);
                XmlElement node = xmlDoc.CreateElement("string");
                node.AppendChild(text);
                return node;
            }
            throw new NotImplementedException();
        }

        private static XmlNode CreateDictionary(XmlDocument xmlDoc, Dictionary<string, object> dict)
        {
            XmlElement dictNode = xmlDoc.CreateElement("dict");

            foreach (KeyValuePair<string, object> entry in dict)
            {
                XmlText text = xmlDoc.CreateTextNode(entry.Key);
                XmlElement keyNode = xmlDoc.CreateElement("key");
                keyNode.AppendChild(text);

                dictNode.AppendChild(keyNode);

                XmlNode valueNode = CreateNode(xmlDoc, entry.Value);
                dictNode.AppendChild(valueNode);
            }

            return dictNode;
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

        private static object ParseInteger(XmlNode xmlNode)
        {
            return int.Parse(xmlNode.InnerText, CultureInfo.InvariantCulture);
        }
    }
}