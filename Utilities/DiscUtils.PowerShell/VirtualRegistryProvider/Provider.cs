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
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using DiscUtils.Registry;

namespace DiscUtils.PowerShell.VirtualRegistryProvider
{
    [CmdletProvider("VirtualRegistry", ProviderCapabilities.None)]
    public sealed class Provider : NavigationCmdletProvider, IDynamicPropertyCmdletProvider
    {
        private static readonly string DefaultValueName = "(default)";

        #region Drive manipulation
        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            NewDriveParameters dynParams = DynamicParameters as NewDriveParameters;

            if (drive == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentNullException(nameof(drive)),
                    "NullDrive",
                    ErrorCategory.InvalidArgument,
                    null));
                return null;
            }

            if (string.IsNullOrEmpty(drive.Root))
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("drive"),
                    "NoRoot",
                    ErrorCategory.InvalidArgument,
                    drive));
                return null;
            }

            string[] mountPaths = drive.Root.Split('!');
            if (mountPaths.Length < 1 || mountPaths.Length > 2)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("drive"),
                    "InvalidRoot",
                    ErrorCategory.InvalidArgument,
                    drive));
                return null;
            }
            string filePath = mountPaths[0];
            string relPath = mountPaths.Length > 1 ? mountPaths[1] : "";

            Stream hiveStream = null;

            FileAccess access = dynParams.ReadWrite.IsPresent ? FileAccess.ReadWrite : FileAccess.Read;
            FileShare share = access == FileAccess.Read ? FileShare.Read : FileShare.None;

            filePath = Utilities.ResolvePsPath(SessionState, filePath);
            hiveStream = Utilities.OpenPsPath(SessionState, filePath, access, share);


            if (hiveStream == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("drive"),
                    "InvalidRoot",
                    ErrorCategory.InvalidArgument,
                    drive));
                return null;
            }
            else
            {
                return new VirtualRegistryPSDriveInfo(drive, MakePath(Utilities.NormalizePath(filePath + "!"), Utilities.NormalizePath(relPath)), hiveStream);
            }
        }

        protected override object NewDriveDynamicParameters()
        {
            return new NewDriveParameters();
        }

        protected override PSDriveInfo RemoveDrive(PSDriveInfo drive)
        {
            if (drive == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentNullException(nameof(drive)),
                    "NullDrive",
                    ErrorCategory.InvalidArgument,
                    null));
                return null;
            }

            VirtualRegistryPSDriveInfo vrDrive = drive as VirtualRegistryPSDriveInfo;
            if (vrDrive == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("invalid type of drive"),
                    "BadDrive",
                    ErrorCategory.InvalidArgument,
                    null));
                return null;
            }

            vrDrive.Close();

            return vrDrive;
        }
        #endregion

        #region Item methods
        protected override void GetItem(string path)
        {
            RegistryKey key = FindItemByPath(path);
            WriteKey(path, key);
        }

        protected override object GetItemDynamicParameters(string path)
        {
            return null;
        }

        protected override void SetItem(string path, object value)
        {
            throw new NotImplementedException();
        }

        protected override bool ItemExists(string path)
        {
            return FindItemByPath(path) != null;
        }

        protected override bool IsValidPath(string path)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Container methods
        protected override void GetChildItems(string path, bool recurse)
        {
            RegistryKey key = FindItemByPath(path);
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                WriteKey(MakePath(path, subKeyName), key.OpenSubKey(subKeyName));
            }
        }

        protected override void GetChildNames(string path, ReturnContainers returnContainers)
        {
            RegistryKey key = FindItemByPath(path);
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                WriteItemObject(subKeyName, MakePath(path, subKeyName), true);
            }
        }

        protected override bool HasChildItems(string path)
        {
            RegistryKey key = FindItemByPath(path);
            return key.SubKeyCount != 0;
        }

        protected override void RemoveItem(string path, bool recurse)
        {
            string parentPath = GetParentPath(path, null);

            RegistryKey parentKey = FindItemByPath(parentPath);
            if (recurse)
            {
                parentKey.DeleteSubKeyTree(GetChildName(path));
            }
            else
            {
                parentKey.DeleteSubKey(GetChildName(path));
            }
        }

        protected override void NewItem(string path, string itemTypeName, object newItemValue)
        {
            string parentPath = GetParentPath(path, null);

            RegistryKey parentKey = FindItemByPath(parentPath);
            WriteItemObject(parentKey.CreateSubKey(GetChildName(path)), path, true);
        }

        protected override void RenameItem(string path, string newName)
        {
            throw new NotImplementedException();
        }

        protected override void CopyItem(string path, string copyPath, bool recurse)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Navigation methods
        protected override bool IsItemContainer(string path)
        {
            return true;
        }

        protected override string MakePath(string parent, string child)
        {
            return Utilities.NormalizePath(base.MakePath(Utilities.DenormalizePath(parent), Utilities.DenormalizePath(child)));
        }

        #endregion

        #region IPropertyCmdletProvider Members

        public void ClearProperty(string path, Collection<string> propertyToClear)
        {
            PSObject propVal = new PSObject();

            bool foundProp = false;
            RegistryKey key = FindItemByPath(path);
            foreach (var valueName in key.GetValueNames())
            {
                string propName = valueName;
                if (string.IsNullOrEmpty(valueName))
                {
                    propName = DefaultValueName;
                }

                if (IsMatch(propName, propertyToClear))
                {
                    RegistryValueType type = key.GetValueType(valueName);
                    object newVal = DefaultRegistryTypeValue(type);

                    key.SetValue(valueName, newVal);
                    propVal.Properties.Add(new PSNoteProperty(propName, newVal));
                    foundProp = true;
                }
            }

            if (foundProp)
            {
                WritePropertyObject(propVal, path);
            }
        }

        public object ClearPropertyDynamicParameters(string path, Collection<string> propertyToClear)
        {
            return null;
        }

        public void GetProperty(string path, Collection<string> providerSpecificPickList)
        {
            PSObject propVal = new PSObject();

            bool foundProp = false;
            RegistryKey key = FindItemByPath(path);
            foreach(var valueName in key.GetValueNames())
            {
                string propName = valueName;
                if (string.IsNullOrEmpty(valueName))
                {
                    propName = DefaultValueName;
                }

                if (IsMatch(propName, providerSpecificPickList))
                {
                    propVal.Properties.Add(new PSNoteProperty(propName, key.GetValue(valueName)));
                    foundProp = true;
                }
            }

            if (foundProp)
            {
                WritePropertyObject(propVal, path);
            }
        }

        public object GetPropertyDynamicParameters(string path, Collection<string> providerSpecificPickList)
        {
            return null;
        }

        public void SetProperty(string path, PSObject propertyValue)
        {
            PSObject propVal = new PSObject();

            RegistryKey key = FindItemByPath(path);
            if (key == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("path"),
                    "NoSuchRegistryKey",
                    ErrorCategory.ObjectNotFound,
                    path));
            }

            foreach (var prop in propertyValue.Properties)
            {
                key.SetValue(prop.Name, prop.Value);
            }
        }

        public object SetPropertyDynamicParameters(string path, PSObject propertyValue)
        {
            return null;
        }

        #endregion

        #region IDynamicPropertyCmdletProvider Members

        public void CopyProperty(string sourcePath, string sourceProperty, string destinationPath, string destinationProperty)
        {
            throw new NotImplementedException();
        }

        public object CopyPropertyDynamicParameters(string sourcePath, string sourceProperty, string destinationPath, string destinationProperty)
        {
            return null;
        }

        public void MoveProperty(string sourcePath, string sourceProperty, string destinationPath, string destinationProperty)
        {
            throw new NotImplementedException();
        }

        public object MovePropertyDynamicParameters(string sourcePath, string sourceProperty, string destinationPath, string destinationProperty)
        {
            return null;
        }

        public void NewProperty(string path, string propertyName, string propertyTypeName, object value)
        {
            RegistryKey key = FindItemByPath(path);
            if (key == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("path"),
                    "NoSuchRegistryKey",
                    ErrorCategory.ObjectNotFound,
                    path));
            }

            RegistryValueType type;
            type = RegistryValueType.None;
            if (!string.IsNullOrEmpty(propertyTypeName))
            {
                try
                {
                    type = (RegistryValueType)Enum.Parse(typeof(RegistryValueType), propertyTypeName, true);
                }
                catch(ArgumentException)
                {
                }
            }

            if(string.Compare(propertyName, DefaultValueName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                propertyName = "";
            }

            key.SetValue(propertyName, value ?? DefaultRegistryTypeValue(type), type);
        }

        public object NewPropertyDynamicParameters(string path, string propertyName, string propertyTypeName, object value)
        {
            return null;
        }

        public void RemoveProperty(string path, string propertyName)
        {
            RegistryKey key = FindItemByPath(path);
            if (key == null)
            {
                WriteError(new ErrorRecord(
                    new ArgumentException("path"),
                    "NoSuchRegistryKey",
                    ErrorCategory.ObjectNotFound,
                    path));
            }

            if (string.Compare(propertyName, DefaultValueName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                propertyName = "";
            }

            key.DeleteValue(propertyName);
        }

        public object RemovePropertyDynamicParameters(string path, string propertyName)
        {
            return null;
        }

        public void RenameProperty(string path, string sourceProperty, string destinationProperty)
        {
            throw new NotImplementedException();
        }

        public object RenamePropertyDynamicParameters(string path, string sourceProperty, string destinationProperty)
        {
            return null;
        }

        #endregion

        private VirtualRegistryPSDriveInfo DriveInfo
        {
            get { return PSDriveInfo as VirtualRegistryPSDriveInfo; }
        }

        private RegistryHive Hive
        {
            get
            {
                VirtualRegistryPSDriveInfo driveInfo = DriveInfo;
                return (driveInfo != null) ? driveInfo.Hive : null;
            }
        }

        private RegistryKey FindItemByPath(string path)
        {
            string filePath;
            string relPath;

            int mountSepIdx = path.IndexOf('!');
            if (mountSepIdx < 0)
            {
                filePath = path;
                relPath = "";
            }
            else
            {
                filePath = path.Substring(0, mountSepIdx);
                relPath = path.Substring(mountSepIdx + 1);

                if (relPath.Length > 0 && relPath[0] == '\\')
                {
                    relPath = relPath.Substring(1);
                }
            }

            RegistryHive hive = Hive;
            if (hive == null)
            {
                throw new NotImplementedException("Accessing registry hives outside of a mounted drive");
            }

            return hive.Root.OpenSubKey(relPath);
        }

        private void WriteKey(string path, RegistryKey key)
        {
            if (key == null)
            {
                return;
            }

            PSObject psObj = PSObject.AsPSObject(key);

            string[] valueNames = key.GetValueNames();
            for (int i = 0; i < valueNames.Length; ++i)
            {
                if (string.IsNullOrEmpty(valueNames[i]))
                {
                    valueNames[i] = DefaultValueName;
                }
            }

            psObj.Properties.Add(new PSNoteProperty("Property", valueNames));
            WriteItemObject(psObj, path.Trim('\\'), true);
        }

        private bool IsMatch(string valueName, Collection<string> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return true;
            }

            foreach (var filter in filters)
            {
                if (WildcardPattern.ContainsWildcardCharacters(filter))
                {
                    if (new WildcardPattern(filter, WildcardOptions.IgnoreCase).IsMatch(valueName))
                    {
                        return true;
                    }
                }
                else if (string.Compare(filter, valueName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static object DefaultRegistryTypeValue(RegistryValueType type)
        {
            switch (type)
            {
                case RegistryValueType.Binary:
                case RegistryValueType.None:
                    return new byte[] { };
                case RegistryValueType.Dword:
                case RegistryValueType.DwordBigEndian:
                    return 0;
                case RegistryValueType.QWord:
                    return 0L;
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                    return "";
                case RegistryValueType.MultiString:
                    return new string[] { };
            }

            return null;
        }

    }
}
