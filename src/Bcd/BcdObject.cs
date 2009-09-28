//
// Copyright (c) 2008-2009, Kenneth Bell
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
using System.Linq;

namespace DiscUtils.Bcd
{
    public class BcdObject
    {
        private BaseStorage _storage;
        private Guid _id;
        private int _type;

        public const string EmsSettingsGroupId = "{0CE4991B-E6B3-4B16-B23C-5E0D9250E5D9}";
        public const string ResumeLoaderSettingsGroupId = "{1AFA9C49-16AB-4A5C-4A90-212802DA9460}";
        public const string DefaultBootEntryId = "{1CAE1EB7-A0DF-4D4D-9851-4860E34EF535}";
        public const string DebuggerSettingsGroupId = "{4636856E-540F-4170-A130-A84776F4C654}";
        public const string WindowsLegacyNtldrId = "{466F5A88-0AF2-4F76-9038-095B170DC21C}";
        public const string BadMemoryGroupId = "{5189B25C-5558-4BF2-BCA4-289B11BD29E2}";
        public const string BootLoaderSettingsGroupId = "{6EFB52BF-1766-41DB-A6B3-0EE5EFF72BD7}";
        public const string WindowsSetupEfiId = "{7254A080-1510-4E85-AC0F-E7FB3D444736}";
        public const string GlobalSettingsGroupId = "{7EA2E1AC-2E61-4728-AAA3-896D9D0A9F0E}";
        public const string WindowsBootManagerId = "{9DEA862C-5CDD-4E70-ACC1-F32B344D4795}";
        public const string WindowsOsTargetTemplatePcatId = "{A1943BBC-EA85-487C-97C7-C9EDE908A38A}";
        public const string FirmwareBootManagerId = "{A5A30FA2-3D06-4E9F-B5F4-A01DF9D1FCBA}";
        public const string WindowsSetupRamdiskOptionsId = "{AE5534E0-A924-466C-B836-758539A3EE3A}";
        public const string WindowsOsTargetTemplateEfiId = "{B012B84D-C47C-4ED5-B722-C0C42163E569}";
        public const string WindowsMemoryTesterId = "{B2721D73-1DB4-4C62-BF78-C548A880142D}";
        public const string WindowsSetupPcatId = "{CBD971BF-B7B8-4885-951A-FA03044F5D71}";
        public const string CurrentBootEntryId = "{FA926493-6F1C-4193-A414-58F0B2456D1E}";

        private static Dictionary<string, Guid> s_NameToGuid;
        private static Dictionary<Guid, string> s_GuidToName;

        static BcdObject()
        {
            s_NameToGuid = new Dictionary<string, Guid>();
            s_GuidToName = new Dictionary<Guid, string>();

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

        public string FriendlyName
        {
            get
            {
                string name;
                if (s_GuidToName.TryGetValue(_id, out name))
                {
                    return name;
                }
                return _id.ToString("B");
            }
        }

        public ObjectType ObjectType
        {
            get { return (ObjectType)((_type >> 28) & 0xF); }
        }

        public ApplicationImageType ApplicationImageType
        {
            get { return IsApplication ? (ApplicationImageType)((_type & 0x00F00000) >> 20) : 0; }
        }

        public ApplicationType ApplicationType
        {
            get { return IsApplication ? (ApplicationType)(_type & 0xFFFFF) : 0; }
        }

        public bool IsInheritableBy(ObjectType type)
        {
            if(type == ObjectType.Inherit)
            {
                throw new ArgumentException("Can not test inheritability by inherit objects", "type");
            }

            if (ObjectType != ObjectType.Inherit)
            {
                return false;
            }

            int setting = ((_type & 0x00F00000) >> 20);

            return setting == 1
                || (setting == 2 && ObjectType == ObjectType.Application)
                || (setting == 3 && ObjectType == ObjectType.Device);
        }

        public IEnumerable<Element> Elements
        {
            get
            {
                return
                    from el in _storage.EnumerateElements(_id)
                    select new Element(_storage, _id, ApplicationType, el);
            }
        }

        public bool HasElement(int id)
        {
            return _storage.HasValue(_id, id);
        }

        public Element GetElement(int id)
        {
            return new Element(_storage, _id, ApplicationType, id);
        }

        public override string ToString()
        {
            return _id.ToString("B");
        }

        private bool IsApplication
        {
            get { return ObjectType == ObjectType.Application; }
        }

        private static void AddMapping(string name, string id)
        {
            Guid guid = new Guid(id);
            s_NameToGuid[name] = guid;
            s_GuidToName[guid] = name;
        }
    }
}
