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

//
// Symbolic names of BCD Objects taken from Geoff Chappell's website:
//  http://www.geoffchappell.com/viewer.htm?doc=notes/windows/boot/bcd/objects.htm
//
//

using System;
using System.Collections.Generic;

namespace DiscUtils.BootConfig
{
    /// <summary>
    /// Represents a Boot Configuration Database object (application, device or inherited settings).
    /// </summary>
    public class BcdObject
    {
        /// <summary>
        /// Well-known object for Emergency Management Services settings.
        /// </summary>
        public const string EmsSettingsGroupId = "{0CE4991B-E6B3-4B16-B23C-5E0D9250E5D9}";

        /// <summary>
        /// Well-known object for the Resume boot loader.
        /// </summary>
        public const string ResumeLoaderSettingsGroupId = "{1AFA9C49-16AB-4A5C-4A90-212802DA9460}";

        /// <summary>
        /// Alias for the Default boot entry.
        /// </summary>
        public const string DefaultBootEntryId = "{1CAE1EB7-A0DF-4D4D-9851-4860E34EF535}";

        /// <summary>
        /// Well-known object for Emergency Management Services settings.
        /// </summary>
        public const string DebuggerSettingsGroupId = "{4636856E-540F-4170-A130-A84776F4C654}";

        /// <summary>
        /// Well-known object for NTLDR application.
        /// </summary>
        public const string WindowsLegacyNtldrId = "{466F5A88-0AF2-4F76-9038-095B170DC21C}";

        /// <summary>
        /// Well-known object for bad memory settings.
        /// </summary>
        public const string BadMemoryGroupId = "{5189B25C-5558-4BF2-BCA4-289B11BD29E2}";

        /// <summary>
        /// Well-known object for Boot Loader settings.
        /// </summary>
        public const string BootLoaderSettingsGroupId = "{6EFB52BF-1766-41DB-A6B3-0EE5EFF72BD7}";

        /// <summary>
        /// Well-known object for EFI setup.
        /// </summary>
        public const string WindowsSetupEfiId = "{7254A080-1510-4E85-AC0F-E7FB3D444736}";

        /// <summary>
        /// Well-known object for Global settings.
        /// </summary>
        public const string GlobalSettingsGroupId = "{7EA2E1AC-2E61-4728-AAA3-896D9D0A9F0E}";

        /// <summary>
        /// Well-known object for Windows Boot Manager.
        /// </summary>
        public const string WindowsBootManagerId = "{9DEA862C-5CDD-4E70-ACC1-F32B344D4795}";

        /// <summary>
        /// Well-known object for PCAT Template.
        /// </summary>
        public const string WindowsOsTargetTemplatePcatId = "{A1943BBC-EA85-487C-97C7-C9EDE908A38A}";

        /// <summary>
        /// Well-known object for Firmware Boot Manager.
        /// </summary>
        public const string FirmwareBootManagerId = "{A5A30FA2-3D06-4E9F-B5F4-A01DF9D1FCBA}";

        /// <summary>
        /// Well-known object for Windows Setup RAMDISK options.
        /// </summary>
        public const string WindowsSetupRamdiskOptionsId = "{AE5534E0-A924-466C-B836-758539A3EE3A}";

        /// <summary>
        /// Well-known object for EFI template.
        /// </summary>
        public const string WindowsOsTargetTemplateEfiId = "{B012B84D-C47C-4ED5-B722-C0C42163E569}";

        /// <summary>
        /// Well-known object for Windows memory tester application.
        /// </summary>
        public const string WindowsMemoryTesterId = "{B2721D73-1DB4-4C62-BF78-C548A880142D}";

        /// <summary>
        /// Well-known object for Windows PCAT setup.
        /// </summary>
        public const string WindowsSetupPcatId = "{CBD971BF-B7B8-4885-951A-FA03044F5D71}";

        /// <summary>
        /// Alias for the current boot entry.
        /// </summary>
        public const string CurrentBootEntryId = "{FA926493-6F1C-4193-A414-58F0B2456D1E}";

        private static readonly Dictionary<string, Guid> _nameToGuid;
        private static readonly Dictionary<Guid, string> _guidToName;
        private Guid _id;

        private readonly BaseStorage _storage;
        private readonly int _type;

        static BcdObject()
        {
            _nameToGuid = new Dictionary<string, Guid>();
            _guidToName = new Dictionary<Guid, string>();

            AddMapping("{emssettings}", EmsSettingsGroupId);
            AddMapping("{resumeloadersettings}", ResumeLoaderSettingsGroupId);
            AddMapping("{default}", DefaultBootEntryId);
            AddMapping("{dbgsettings}", DebuggerSettingsGroupId);
            AddMapping("{legacy}", WindowsLegacyNtldrId);
            AddMapping("{ntldr}", WindowsLegacyNtldrId);
            AddMapping("{badmemory}", BadMemoryGroupId);
            AddMapping("{bootloadersettings}", BootLoaderSettingsGroupId);
            AddMapping("{globalsettings}", GlobalSettingsGroupId);
            AddMapping("{bootmgr}", WindowsBootManagerId);
            AddMapping("{fwbootmgr}", FirmwareBootManagerId);
            AddMapping("{ramdiskoptions}", WindowsSetupRamdiskOptionsId);
            AddMapping("{memdiag}", WindowsMemoryTesterId);
            AddMapping("{current}", CurrentBootEntryId);
        }

        internal BcdObject(BaseStorage store, Guid id)
        {
            _storage = store;
            _id = id;
            _type = _storage.GetObjectType(id);
        }

        /// <summary>
        /// Gets the image type for this application.
        /// </summary>
        public ApplicationImageType ApplicationImageType
        {
            get { return IsApplication ? (ApplicationImageType)((_type & 0x00F00000) >> 20) : 0; }
        }

        /// <summary>
        /// Gets the application type for this application.
        /// </summary>
        public ApplicationType ApplicationType
        {
            get { return IsApplication ? (ApplicationType)(_type & 0xFFFFF) : 0; }
        }

        /// <summary>
        /// Gets the elements in this object.
        /// </summary>
        public IEnumerable<Element> Elements
        {
            get
            {
                foreach (int el in _storage.EnumerateElements(_id))
                {
                    yield return new Element(_storage, _id, ApplicationType, el);
                }
            }
        }

        /// <summary>
        /// Gets the friendly name for this object, if known.
        /// </summary>
        public string FriendlyName
        {
            get
            {
                string name;
                if (_guidToName.TryGetValue(_id, out name))
                {
                    return name;
                }

                return _id.ToString("B");
            }
        }

        /// <summary>
        /// Gets the identity of this object.
        /// </summary>
        public Guid Identity
        {
            get { return _id; }
        }

        private bool IsApplication
        {
            get { return ObjectType == ObjectType.Application; }
        }

        /// <summary>
        /// Gets the object type for this object.
        /// </summary>
        public ObjectType ObjectType
        {
            get { return (ObjectType)((_type >> 28) & 0xF); }
        }

        /// <summary>
        /// Indicates if the settings in this object are inheritable by another object.
        /// </summary>
        /// <param name="type">The type of the object to test for inheritability.</param>
        /// <returns><c>true</c> if the settings can be inherited, else <c>false</c>.</returns>
        public bool IsInheritableBy(ObjectType type)
        {
            if (type == ObjectType.Inherit)
            {
                throw new ArgumentException("Can not test inheritability by inherit objects", nameof(type));
            }

            if (ObjectType != ObjectType.Inherit)
            {
                return false;
            }

            InheritType setting = (InheritType)((_type & 0x00F00000) >> 20);

            return setting == InheritType.AnyObject
                   || (setting == InheritType.ApplicationObjects && type == ObjectType.Application)
                   || (setting == InheritType.DeviceObjects && type == ObjectType.Device);
        }

        /// <summary>
        /// Indicates if this object has a specific element.
        /// </summary>
        /// <param name="id">The identity of the element to look for.</param>
        /// <returns><c>true</c> if present, else <c>false</c>.</returns>
        public bool HasElement(int id)
        {
            return _storage.HasValue(_id, id);
        }

        /// <summary>
        /// Indicates if this object has a specific element.
        /// </summary>
        /// <param name="id">The identity of the element to look for.</param>
        /// <returns><c>true</c> if present, else <c>false</c>.</returns>
        public bool HasElement(WellKnownElement id)
        {
            return HasElement((int)id);
        }

        /// <summary>
        /// Gets a specific element in this object.
        /// </summary>
        /// <param name="id">The identity of the element to look for.</param>
        /// <returns>The element object.</returns>
        public Element GetElement(int id)
        {
            if (HasElement(id))
            {
                return new Element(_storage, _id, ApplicationType, id);
            }

            return null;
        }

        /// <summary>
        /// Gets a specific element in this object.
        /// </summary>
        /// <param name="id">The identity of the element to look for.</param>
        /// <returns>The element object.</returns>
        public Element GetElement(WellKnownElement id)
        {
            return GetElement((int)id);
        }

        /// <summary>
        /// Adds an element in this object.
        /// </summary>
        /// <param name="id">The identity of the element to add.</param>
        /// <param name="initialValue">The initial value of the element.</param>
        /// <returns>The element object.</returns>
        public Element AddElement(int id, ElementValue initialValue)
        {
            _storage.CreateElement(_id, id);
            Element el = new Element(_storage, _id, ApplicationType, id);
            el.Value = initialValue;
            return el;
        }

        /// <summary>
        /// Adds an element in this object.
        /// </summary>
        /// <param name="id">The identity of the element to add.</param>
        /// <param name="initialValue">The initial value of the element.</param>
        /// <returns>The element object.</returns>
        public Element AddElement(WellKnownElement id, ElementValue initialValue)
        {
            return AddElement((int)id, initialValue);
        }

        /// <summary>
        /// Removes a specific element.
        /// </summary>
        /// <param name="id">The element to remove.</param>
        public void RemoveElement(int id)
        {
            _storage.DeleteElement(_id, id);
        }

        /// <summary>
        /// Removes a specific element.
        /// </summary>
        /// <param name="id">The element to remove.</param>
        public void RemoveElement(WellKnownElement id)
        {
            RemoveElement((int)id);
        }

        /// <summary>
        /// Returns the object identity as a GUID string.
        /// </summary>
        /// <returns>A string representation, with surrounding curly braces.</returns>
        public override string ToString()
        {
            return _id.ToString("B");
        }

        internal static int MakeApplicationType(ApplicationImageType imageType, ApplicationType appType)
        {
            return 0x10000000 | (((int)imageType << 20) & 0x00F00000) | ((int)appType & 0x0000FFFF);
        }

        internal static int MakeInheritType(InheritType inheritType)
        {
            return 0x20000000 | (((int)inheritType << 20) & 0x00F00000);
        }

        private static void AddMapping(string name, string id)
        {
            Guid guid = new Guid(id);
            _nameToGuid[name] = guid;
            _guidToName[guid] = name;
        }
    }
}