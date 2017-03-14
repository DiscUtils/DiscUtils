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
using System.Globalization;
using System.Reflection;
using DiscUtils.CoreCompat;

namespace DiscUtils.Iscsi
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class ProtocolKeyAttribute : Attribute
    {
        public ProtocolKeyAttribute(string name, string defaultValue, KeyUsagePhase phase, KeySender sender, KeyType type)
        {
            Name = name;
            DefaultValue = defaultValue;
            Phase = phase;
            Sender = sender;
            Type = type;
        }

        public string DefaultValue { get; }

        public bool LeadingConnectionOnly { get; set; }

        public string Name { get; }

        public KeyUsagePhase Phase { get; }

        public KeySender Sender { get; }

        public KeyType Type { get; }

        public bool UsedForDiscovery { get; set; }

        internal static string GetValueAsString(object value, Type valueType)
        {
            if (valueType == typeof(bool))
            {
                return (bool)value ? "Yes" : "No";
            }
            if (valueType == typeof(string))
            {
                return (string)value;
            }
            if (valueType == typeof(int))
            {
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            if (ReflectionHelper.IsEnum(valueType))
            {
                FieldInfo[] infos = valueType.GetFields();

                foreach (FieldInfo info in infos)
                {
                    if (info.IsLiteral)
                    {
                        object literalValue = info.GetValue(null);
                        if (literalValue.Equals(value))
                        {
                            Attribute attr = ReflectionHelper.GetCustomAttribute(info, typeof(ProtocolKeyValueAttribute));
                            return ((ProtocolKeyValueAttribute)attr).Name;
                        }
                    }
                }

                throw new NotImplementedException();
            }
            throw new NotSupportedException("Unknown property type: " + valueType);
        }

        internal static object GetValueAsObject(string value, Type valueType)
        {
            if (valueType == typeof(bool))
            {
                return value == "Yes";
            }
            if (valueType == typeof(string))
            {
                return value;
            }
            if (valueType == typeof(int))
            {
                return int.Parse(value, CultureInfo.InvariantCulture);
            }
            if (ReflectionHelper.IsEnum(valueType))
            {
                FieldInfo[] infos = valueType.GetFields();
                foreach (FieldInfo info in infos)
                {
                    if (info.IsLiteral)
                    {
                        Attribute attr = ReflectionHelper.GetCustomAttribute(info, typeof(ProtocolKeyValueAttribute));
                        if (attr != null && ((ProtocolKeyValueAttribute)attr).Name == value)
                        {
                            return info.GetValue(null);
                        }
                    }
                }

                throw new NotImplementedException();
            }
            throw new NotSupportedException("Unknown property type: " + valueType);
        }

        internal bool ShouldTransmit(object currentValue, Type valueType, KeyUsagePhase phase, bool discoverySession)
        {
            return
                (Phase & phase) != 0
                && (discoverySession ? UsedForDiscovery : true)
                && currentValue != null
                && GetValueAsString(currentValue, valueType) != DefaultValue
                && (Sender & KeySender.Initiator) != 0;
        }
    }
}