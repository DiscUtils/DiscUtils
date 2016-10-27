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
using System.Security.AccessControl;
using NUnit.Framework;

namespace DiscUtils.Registry
{
    [TestFixture]
    public class RegistryKeyTest
    {
        private RegistryHive hive;

        [SetUp]
        public void Setup()
        {
            hive = RegistryHive.Create(new MemoryStream());
        }

        [Test]
        public void SetDefaultValue()
        {
            hive.Root.SetValue("", "A default value");
            Assert.AreEqual("A default value", (string)hive.Root.GetValue(""));

            hive.Root.SetValue(null, "Foobar");
            Assert.AreEqual("Foobar", (string)hive.Root.GetValue(null));

            hive.Root.SetValue(null, "asdf");
            Assert.AreEqual("asdf", (string)hive.Root.GetValue(""));
        }

        [Test]
        public void ValueNameCaseSensitivity()
        {
            hive.Root.SetValue("nAmE", "value");
            Assert.AreEqual("value", (string)hive.Root.GetValue("NaMe"));

            hive.Root.SetValue("moreThanFourCharName", "foo");
            Assert.AreEqual("foo", (string)hive.Root.GetValue("moretHANfOURcHARnAME"));

            Assert.AreEqual(2, hive.Root.ValueCount);
            hive.Root.SetValue("NaMe", "newvalue");
            Assert.AreEqual(2, hive.Root.ValueCount);
            Assert.AreEqual("newvalue", (string)hive.Root.GetValue("NaMe"));
        }

        [Test]
        public void SetLargeValue()
        {
            byte[] buffer = new byte[64 * 1024];
            buffer[5232] = 0xAD;
            hive.Root.SetValue("bigvalue", buffer);

            byte[] readVal = (byte[])hive.Root.GetValue("bigvalue");
            Assert.AreEqual(buffer.Length, readVal.Length);
            Assert.AreEqual(0xAD, readVal[5232]);
        }

        [Test]
        public void SetStringValue()
        {
            hive.Root.SetValue("value", "string");
            Assert.AreEqual(RegistryValueType.String, hive.Root.GetValueType("value"));
            Assert.AreEqual("string", (string)hive.Root.GetValue("value"));

            hive.Root.SetValue("emptyvalue", "");
            Assert.AreEqual(RegistryValueType.String, hive.Root.GetValueType("emptyvalue"));
            Assert.AreEqual("", (string)hive.Root.GetValue("emptyvalue"));
        }

        [Test]
        public void SetIntegerValue()
        {
            hive.Root.SetValue("value", 0x7342BEEF);
            Assert.AreEqual(RegistryValueType.Dword, hive.Root.GetValueType("value"));
            Assert.AreEqual(0x7342BEEF, (int)hive.Root.GetValue("value"));
        }

        [Test]
        public void SetByteArrayValue()
        {
            hive.Root.SetValue("value", new byte[] { 1, 2, 3, 4 });
            Assert.AreEqual(RegistryValueType.Binary, hive.Root.GetValueType("value"));
            byte[] readVal = (byte[])hive.Root.GetValue("value");
            Assert.AreEqual(4, readVal.Length);
            Assert.AreEqual(3, readVal[2]);
        }

        [Test]
        public void SetStringArrayValue()
        {
            hive.Root.SetValue("value", new string[] {"A", "B", "C"});
            Assert.AreEqual(RegistryValueType.MultiString, hive.Root.GetValueType("value"));
            string[] readVal = (string[])hive.Root.GetValue("value");
            Assert.AreEqual(3, readVal.Length);
            Assert.AreEqual("C", readVal[2]);
        }

        [Test]
        public void SetEnvStringValue()
        {
            hive.Root.SetValue("value", "string", RegistryValueType.ExpandString);
            Assert.AreEqual(RegistryValueType.ExpandString, hive.Root.GetValueType("value"));
            Assert.AreEqual("string", (string)hive.Root.GetValue("value"));

            hive.Root.SetValue("value", "str%windir%ing", RegistryValueType.ExpandString);
            Assert.AreEqual(RegistryValueType.ExpandString, hive.Root.GetValueType("value"));
            Assert.AreEqual("str" + Environment.GetEnvironmentVariable("windir") + "ing", (string)hive.Root.GetValue("value"));

            hive.Root.SetValue("emptyvalue", "", RegistryValueType.ExpandString);
            Assert.AreEqual(RegistryValueType.ExpandString, hive.Root.GetValueType("emptyvalue"));
            Assert.AreEqual("", (string)hive.Root.GetValue("emptyvalue"));
        }

        [Test]
        public void DeleteValue()
        {
            hive.Root.SetValue("aValue", "value");
            hive.Root.SetValue("nAmE", "value");
            hive.Root.SetValue("otherValue", "value");
            Assert.AreEqual(3, hive.Root.ValueCount);

            hive.Root.DeleteValue("NaMe");
            Assert.AreEqual(2, hive.Root.ValueCount);
        }

        [Test]
        public void DeleteOnlyValue()
        {
            hive.Root.SetValue("nAmE", "value");
            Assert.AreEqual(1, hive.Root.ValueCount);

            hive.Root.DeleteValue("NaMe");
            Assert.AreEqual(0, hive.Root.ValueCount);
        }

        [Test]
        public void DeleteDefaultValue()
        {
            hive.Root.SetValue("", "value");
            Assert.AreEqual(1, hive.Root.ValueCount);

            hive.Root.DeleteValue(null);
            Assert.AreEqual(0, hive.Root.ValueCount);
        }

        [Test]
        public void EnumerateValues()
        {
            hive.Root.SetValue(@"C", "");
            hive.Root.SetValue(@"A", "");
            hive.Root.SetValue(@"B", "");

            string[] names = hive.Root.GetValueNames();
            Assert.AreEqual(3, names.Length);
            Assert.AreEqual("A", names[0]);
            Assert.AreEqual("B", names[1]);
            Assert.AreEqual("C", names[2]);
        }

        [Test]
        public void CreateKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child\Grandchild");
            Assert.NotNull(newKey);
            Assert.AreEqual(1, hive.Root.SubKeyCount);
            Assert.AreEqual(1, hive.Root.OpenSubKey("cHiLd").SubKeyCount);
        }

        [Test]
        public void CreateExistingKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child");
            Assert.NotNull(newKey);
            Assert.AreEqual(1, hive.Root.SubKeyCount);

            newKey = hive.Root.CreateSubKey(@"cHILD");
            Assert.NotNull(newKey);
            Assert.AreEqual(1, hive.Root.SubKeyCount);
        }

        [Test]
        public void DeleteKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child");
            hive.Root.OpenSubKey(@"Child").SetValue("value", "a value");
            Assert.AreEqual(1, hive.Root.SubKeyCount);
            hive.Root.DeleteSubKey("cHiLd");
            Assert.AreEqual(0, hive.Root.SubKeyCount);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void DeleteNonEmptyKey()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child\Grandchild");
            hive.Root.DeleteSubKey("Child");
        }

        [Test]
        public void DeleteKeyTree()
        {
            RegistryKey newKey = hive.Root.CreateSubKey(@"Child\Grandchild");
            Assert.AreEqual(1, hive.Root.SubKeyCount);
            hive.Root.DeleteSubKeyTree("cHiLd");
            Assert.AreEqual(0, hive.Root.SubKeyCount);
        }

        [Test]
        public void EnumerateSubKeys()
        {
            hive.Root.CreateSubKey(@"C");
            hive.Root.CreateSubKey(@"A");
            hive.Root.CreateSubKey(@"B");

            string[] names = hive.Root.GetSubKeyNames();
            Assert.AreEqual(3, names.Length);
            Assert.AreEqual("A", names[0]);
            Assert.AreEqual("B", names[1]);
            Assert.AreEqual("C", names[2]);
        }
    }
}
