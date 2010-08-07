//
// Copyright (c) 2008-2010, Kenneth Bell
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
using System.Globalization;
using System.Reflection;

namespace DiscUtils.Iscsi
{
    [Flags]
    internal enum KeyUsagePhase
    {
        SecurityNegotiation = 0x01,
        OperationalNegotiation = 0x02,
        FullyFeatured = 0x04,
        All = 0x07,
    };

    [Flags]
    internal enum KeySender
    {
        Initiator = 0x01,
        Target = 0x02,
        Both = 0x03
    }

    internal enum KeyType
    {
        Declarative,
        Negotiated
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple=false, Inherited=false)]
    internal sealed class ProtocolKeyValueAttribute : Attribute
    {
        private string _name;

        public ProtocolKeyValueAttribute(string name)
        {
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal sealed class ProtocolKeyAttribute : Attribute
    {
        private string _name;
        private string _default;
        private KeyUsagePhase _phase;
        private KeySender _sender;
        private KeyType _type;

        public ProtocolKeyAttribute(string name, string defaultValue, KeyUsagePhase phase, KeySender sender, KeyType type)
        {
            _name = name;
            _default = defaultValue;
            _phase = phase;
            _sender = sender;
            _type = type;
        }

        public string Name
        {
            get { return _name; }
        }

        public string DefaultValue
        {
            get { return _default; }
        }

        public KeyUsagePhase Phase
        {
            get { return _phase; }
        }

        public bool LeadingConnectionOnly { get; set; }

        public KeySender Sender
        {
            get { return _sender; }
        }

        public KeyType Type
        {
            get { return _type; }
        }

        public bool UsedForDiscovery { get; set; }

        internal static string GetValueAsString(object value, Type valueType)
        {
            if (valueType == typeof(bool))
            {
                return ((bool)value) ? "Yes" : "No";
            }
            else if (valueType == typeof(string))
            {
                return (string)value;
            }
            else if (valueType == typeof(int))
            {
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            else if (valueType.IsEnum)
            {
                FieldInfo[] infos = valueType.GetFields();
                foreach (var info in infos)
                {
                    if (info.IsLiteral)
                    {
                        object literalValue = info.GetValue(null);
                        if (literalValue.Equals(value))
                        {
                            Attribute attr = Attribute.GetCustomAttribute(info, typeof(ProtocolKeyValueAttribute));
                            return ((ProtocolKeyValueAttribute)attr).Name;
                        }
                    }
                }
                throw new NotImplementedException();
            }
            else
            {
                throw new NotSupportedException("Unknown property type: " + valueType);
            }
        }

        internal static object GetValueAsObject(string value, Type valueType)
        {
            if (valueType == typeof(bool))
            {
                return value == "Yes";
            }
            else if (valueType == typeof(string))
            {
                return value;
            }
            else if (valueType == typeof(int))
            {
                return int.Parse(value, CultureInfo.InvariantCulture);
            }
            else if (valueType.IsEnum)
            {
                FieldInfo[] infos = valueType.GetFields();
                foreach (var info in infos)
                {
                    if (info.IsLiteral)
                    {
                        Attribute attr = Attribute.GetCustomAttribute(info, typeof(ProtocolKeyValueAttribute));
                        if (attr != null && ((ProtocolKeyValueAttribute)attr).Name == value)
                        {
                            return info.GetValue(null);
                        }
                    }
                }
                throw new NotImplementedException();
            }
            else
            {
                throw new NotSupportedException("Unknown property type: " + valueType);
            }
        }

        internal bool ShouldTransmit(object currentValue, Type valueType, KeyUsagePhase phase, bool discoverySession)
        {
            return
                ((Phase & phase) != 0
                && (discoverySession ? (UsedForDiscovery == true) : true)
                && currentValue != null
                && GetValueAsString(currentValue, valueType) != DefaultValue
                && (Sender & KeySender.Initiator) != 0);
        }
    }
}
