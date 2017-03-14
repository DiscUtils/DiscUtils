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
using System.IO;
using DiscUtils.Registry;
using Xunit;

namespace LibraryTests.Registry
{
    public class RegistryKeyTest
    {
        private RegistryHive hive;

        public RegistryKeyTest()
        {
            hive = RegistryHive.Create(new MemoryStream());
        }

        [Fact]
        public void SetDefaultValue()
        {
            hive.Root.SetValue("", "A default value");
            Assert.Equal("A default value", (string)hive.Root.GetValue(""));

            hive.Root.SetValue(null, "Foobar");
            Assert.Equal("Foobar", (string)hive.Root.GetValue(null));

            hive.Root.SetValue(null, "asdf");
            Assert.Equal("asdf", (string)hive.Root.GetValue(""));
        }

        [Fact]
        public void ValueNameCaseSensitivity()
        {
            hive.Root.SetValue("nAmE", "value");
            Assert.Equal("value", (string)hive.Root.GetValue("NaMe"));

            hive.Root.SetValue("moreThanFourCharName", "foo");
            Assert.Equal("foo", (string)hive.Root.GetValue("moretHANfOURcHARnAME"));

            Assert.Equal(2, hive.Root.ValueCount);
            hive.Root.SetValue("NaMe", "newvalue");
            Assert.Equal(2, hive.Root.ValueCount);
            Assert.Equal("newvalue", (string)hive.Root.GetValue("NaMe"));
        }

        [Fact]
        public void SetLargeValue()
        {
            byte[] buffer = new byte[64 * 1024];
            buffer[5232] = 0xAD;
            hive.Root.SetValue("bigvalue", buffer);

            byte[] readVal = (byte[])hive.Root.GetValue("bigvalue");
            Assert.Equal(buffer.Length, readVal.Length);
            Assert.Equal(0xAD, readVal[5232]);
        }

        [Fact]
        public void SetStringValue()
        {
            hive.Root.SetValue("value", "string");
            Assert.Equal(RegistryValueType.String, hive.Root.GetValueType("value"));
            Assert.Equal("string", (string)hive.Root.GetValue("value"));

            hive.Root.SetValue("emptyvalue", "");
            Assert.Equal(RegistryValueType.String, hive.Root.GetValueType("emptyvalue"));
            Assert.Equal("", (string)hive.Root.GetValue("emptyvalue"));
        }

        [Fact]
        public void SetIntegerValue()
        {
            hive.Root.SetValue("value", 0x7342BEEF);
            Assert.Equal(RegistryValueType.Dword, hive.Root.GetValueType("value"));
            Assert.Equal(0x7342BEEF, (int)hive.Root.GetValue("value"));
        }

        [Fact]
        public void SetByteArrayValue()
        {
            hive.Root.SetValue("value", new byte[] { 1, 2, 3, 4 });
            Assert.Equal(RegistryValueType.Binary, hive.Root.GetValueType("value"));
            byte[] readVal = (byte[])hive.Root.GetValue("value");
            Assert.Equal(4, readVal.Length);
            Assert.Equal(3, readVal[2]);
        }

        [Fact]
        public void SetStringArrayValue()
        {
            hive.Root.SetValue("value", new string[] { "A", "B", "C" });
            Assert.Equal(RegistryValueType.MultiString, hive.Root.GetValueType("value"));
            string[] readVal = (string[])hive.Root.GetValue("value");
            Assert.Equal(3, readVal.Length);
            Assert.Equal("C", readVal[2]);
        }

        [Fact]
        public void SetEnvStringValue()
        {
            hive.Root.SetValue("value", "string", RegistryValueType.ExpandString);
            Assert.Equal(RegistryValueType.ExpandString, hive.Root.GetValueType("value"));
            Assert.Equal("string", (string)hive.Root.GetValue("value"));

            hive.Root.SetValue("value", "str%windir%ing", RegistryValueType.ExpandString);
            Assert.Equal(RegistryValueType.ExpandString, hive.Root.GetValueType("value"));
            Assert.Equal("str" + Environment.GetEnvironmentVariable("windir") + "ing", (string)hive.Root.GetValue("value"));

            hive.Root.SetValue("emptyvalue", "", RegistryValueType.ExpandString);
            Assert.Equal(RegistryValueType.ExpandString, hive.Root.GetValueType("emptyvalue"));
            Assert.Equal("", (string)hive.Root.GetValue("emptyvalue"));
        }

        [Fact]
        public void DeleteValue()
        {
            hive.Root.SetValue("aValue", "value");
            hive.Root.SetValue("nAmE", "value");
            hive.Root.SetValue("otherValue", "value");
            Assert.Equal(3, hive.Root.ValueCount);

            hive.Root.DeleteValue("NaMe");
            Assert.Equal(2, hive.Root.ValueCount);
        }

        [Fact]
        public void DeleteOnlyValue()
        {
            hive.Root.SetValue("nAmE", "value");
            Assert.Equal(1, hive.Root.ValueCount);

            hive.Root.DeleteValue("NaMe");
            Assert.Equal(0, hive.Root.ValueCount);
        }

        [Fact]
        public void DeleteDefaultValue()
        {
            hive.Root.SetValue("", "value");
            Assert.Equal(1, hive.Root.ValueCount);

            hive.Root.DeleteValue(null);
            Assert.Equal(0, hive.Root.ValueCount);
        }

        [Fact]
        public void EnumerateValues()
        {
            hive.Root.SetValue(@"C", "");
            hive.Root.SetValue(@"A", "");
            hive.Root.SetValue(@"B", "");

            string[] names = hive.Root.GetValueNames();
            Assert.Equal(3, names.Length);
            Assert.Equal("A", names[0]);
            Assert.Equal("B", names[1]);
            Assert.Equal("C", names[2]);
        }

        [Fact]
        public void CreateKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child\Grandchild");
            Assert.NotNull(newKey);
            Assert.Equal(1, hive.Root.SubKeyCount);
            Assert.Equal(1, hive.Root.OpenSubKey("cHiLd").SubKeyCount);
        }

        [Fact]
        public void CreateExistingKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child");
            Assert.NotNull(newKey);
            Assert.Equal(1, hive.Root.SubKeyCount);

            newKey = hive.Root.CreateSubKey(@"cHILD");
            Assert.NotNull(newKey);
            Assert.Equal(1, hive.Root.SubKeyCount);
        }

        [Fact]
        public void DeleteKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child");
            hive.Root.OpenSubKey(@"Child").SetValue("value", "a value");
            Assert.Equal(1, hive.Root.SubKeyCount);
            hive.Root.DeleteSubKey("cHiLd");
            Assert.Equal(0, hive.Root.SubKeyCount);
        }

        [Fact]
        public void DeleteNonEmptyKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child\Grandchild");
            Assert.Throws<InvalidOperationException>(() => hive.Root.DeleteSubKey("Child"));
        }

        [Fact]
        public void DeleteKeyTree()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child\Grandchild");
            Assert.Equal(1, hive.Root.SubKeyCount);
            hive.Root.DeleteSubKeyTree("cHiLd");
            Assert.Equal(0, hive.Root.SubKeyCount);
        }

        [Fact]
        public void EnumerateSubKeys()
        {
            hive.Root.CreateSubKey(@"C");
            hive.Root.CreateSubKey(@"A");
            hive.Root.CreateSubKey(@"B");

            string[] names = hive.Root.GetSubKeyNames();
            Assert.Equal(3, names.Length);
            Assert.Equal("A", names[0]);
            Assert.Equal("B", names[1]);
            Assert.Equal("C", names[2]);
        }
    }
}
