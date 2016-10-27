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
using DiscUtils.Partitions;
using DiscUtils.Registry;
using NUnit.Framework;

namespace DiscUtils.BootConfig
{
    [TestFixture]
    public class ElementValueTest
    {
        [Test]
        public void StringValue()
        {
            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.LibraryApplicationPath, ElementValue.ForString(@"a\path\to\nowhere"));

            el = obj.GetElement(WellKnownElement.LibraryApplicationPath);

            Assert.AreEqual(@"a\path\to\nowhere", el.Value.ToString());
        }

        [Test]
        public void BooleanValue()
        {
            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.LibraryAutoRecoveryEnabled, ElementValue.ForBoolean(true));

            el = obj.GetElement(WellKnownElement.LibraryAutoRecoveryEnabled);

            Assert.AreEqual(true.ToString(), el.Value.ToString());
        }

        [Test]
        public void DeviceValue_Gpt()
        {
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(80 * 1024 * 1024);
            GuidPartitionTable gpt = GuidPartitionTable.Initialize(ms, Geometry.FromCapacity(ms.Length));
            gpt.Create(WellKnownPartitionType.WindowsNtfs, true);
            VolumeManager volMgr = new VolumeManager(ms);

            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.LibraryApplicationDevice, ElementValue.ForDevice(Guid.Empty, volMgr.GetPhysicalVolumes()[0]));

            el = obj.GetElement(WellKnownElement.LibraryApplicationDevice);

            Assert.IsNotNullOrEmpty(el.Value.ToString());
        }

        [Test]
        public void DeviceValue_Mbr()
        {
            SparseMemoryStream ms = new SparseMemoryStream();
            ms.SetLength(80 * 1024 * 1024);
            BiosPartitionTable pt = BiosPartitionTable.Initialize(ms, Geometry.FromCapacity(ms.Length));
            pt.Create(WellKnownPartitionType.WindowsNtfs, true);
            VolumeManager volMgr = new VolumeManager(ms);

            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.LibraryApplicationDevice, ElementValue.ForDevice(Guid.Empty, volMgr.GetPhysicalVolumes()[0]));

            el = obj.GetElement(WellKnownElement.LibraryApplicationDevice);

            Assert.IsNotNullOrEmpty(el.Value.ToString());
        }

        [Test]
        public void DeviceValue_BootDevice()
        {
            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.LibraryApplicationDevice, ElementValue.ForBootDevice());

            el = obj.GetElement(WellKnownElement.LibraryApplicationDevice);

            Assert.IsNotNullOrEmpty(el.Value.ToString());
        }

        [Test]
        public void GuidValue()
        {
            Guid testGuid = Guid.NewGuid();

            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.BootMgrDefaultObject, ElementValue.ForGuid(testGuid));

            el = obj.GetElement(WellKnownElement.BootMgrDefaultObject);

            Assert.AreEqual(testGuid.ToString("B"), el.Value.ToString());
        }

        [Test]
        public void GuidListValue()
        {
            Guid testGuid1 = Guid.NewGuid();
            Guid testGuid2 = Guid.NewGuid();

            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.BootMgrDisplayOrder, ElementValue.ForGuidList(new Guid[] {testGuid1, testGuid2}));

            el = obj.GetElement(WellKnownElement.BootMgrDisplayOrder);

            Assert.AreEqual(testGuid1.ToString("B") + "," + testGuid2.ToString("B"), el.Value.ToString());
        }

        [Test]
        public void IntegerValue()
        {
            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.LibraryTruncatePhysicalMemory, ElementValue.ForInteger(1234));

            el = obj.GetElement(WellKnownElement.LibraryTruncatePhysicalMemory);

            Assert.AreEqual("1234", el.Value.ToString());
        }

        [Test]
        public void IntegerListValue()
        {
            RegistryHive hive = RegistryHive.Create(new MemoryStream());
            Store s = Store.Initialize(hive.Root);
            BcdObject obj = s.CreateInherit(InheritType.AnyObject);

            Element el = obj.AddElement(WellKnownElement.LibraryBadMemoryList, ElementValue.ForIntegerList(new long[] { 1234, 4132 }));

            el = obj.GetElement(WellKnownElement.LibraryBadMemoryList);

            Assert.IsNotNullOrEmpty(el.Value.ToString());
        }
    }
}
