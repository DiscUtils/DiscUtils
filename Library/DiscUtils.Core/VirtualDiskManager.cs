using System;
using System.Collections.Generic;
using System.Reflection;
using DiscUtils.CoreCompat;
using DiscUtils.Internal;

namespace DiscUtils
{
    /// <summary>
    /// Helps discover and use VirtualDiskFactory's
    /// </summary>
    public static class VirtualDiskManager
    {
        static VirtualDiskManager()
        {
            ExtensionMap = new Dictionary<string, VirtualDiskFactory>();
            TypeMap = new Dictionary<string, VirtualDiskFactory>();
            DiskTransports = new Dictionary<string, Type>();
        }

        internal static Dictionary<string, Type> DiskTransports { get; }
        internal static Dictionary<string, VirtualDiskFactory> ExtensionMap { get; }

        /// <summary>
        /// Gets the set of disk formats supported as an array of file extensions.
        /// </summary>
        public static ICollection<string> SupportedDiskFormats
        {
            get { return ExtensionMap.Keys; }
        }

        /// <summary>
        /// Gets the set of disk types supported, as an array of identifiers.
        /// </summary>
        public static ICollection<string> SupportedDiskTypes
        {
            get { return TypeMap.Keys; }
        }

        internal static Dictionary<string, VirtualDiskFactory> TypeMap { get; }

        /// <summary>
        /// Locates VirtualDiskFactory factories attributed with VirtualDiskFactoryAttribute, and types marked with VirtualDiskTransportAttribute, that are able to work with Virtual Disk types.
        /// </summary>
        /// <param name="assembly">An assembly to scan</param>
        public static void RegisterVirtualDiskTypes(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                VirtualDiskFactoryAttribute diskFactoryAttribute = (VirtualDiskFactoryAttribute)ReflectionHelper.GetCustomAttribute(type, typeof(VirtualDiskFactoryAttribute), false);
                if (diskFactoryAttribute != null)
                {
                    VirtualDiskFactory factory = (VirtualDiskFactory)Activator.CreateInstance(type);
                    TypeMap.Add(diskFactoryAttribute.Type, factory);

                    foreach (string extension in diskFactoryAttribute.FileExtensions)
                    {
                        ExtensionMap.Add(extension.ToUpperInvariant(), factory);
                    }
                }

                VirtualDiskTransportAttribute diskTransportAttribute = ReflectionHelper.GetCustomAttribute(type, typeof(VirtualDiskTransportAttribute), false) as VirtualDiskTransportAttribute;
                if (diskTransportAttribute != null)
                {
                    DiskTransports.Add(diskTransportAttribute.Scheme.ToUpperInvariant(), type);
                }
            }
        }
    }
}