using System;
using System.Collections.Generic;
using System.Reflection;
using DiscUtils.CoreCompat;

namespace DiscUtils.Setup
{
    /// <summary>
    /// Helps setup new DiscUtils dependencies, when loaded into target programs
    /// </summary>
    public static class SetupHelper
    {
        private static readonly HashSet<string> _alreadyLoaded;

        static SetupHelper()
        {
            _alreadyLoaded = new HashSet<string>();

            // Register the core DiscUtils lib
            RegisterAssembly(ReflectionHelper.GetAssembly(typeof(SetupHelper)));
        }

        /// <summary>
        /// Registers the types provided by an assembly to all relevant DiscUtils managers
        /// </summary>
        /// <param name="assembly"></param>
        public static void RegisterAssembly(Assembly assembly)
        {
            lock (_alreadyLoaded)
            {
                if (!_alreadyLoaded.Add(assembly.FullName))
                    return;

                FileSystemManager.RegisterFileSystems(assembly);
                VirtualDiskManager.RegisterVirtualDiskTypes(assembly);
                VolumeManager.RegisterLogicalVolumeFactory(assembly);
            }
        }

        /// <summary>
        /// Allows intercepting any file open operation
        /// </summary>
        /// <remarks>
        /// Can be used to wrap the opened file for special use cases,
        /// modify the parameters for opening files, validate file names 
        /// and many more.
        /// </remarks>
        public static event EventHandler<FileOpenEventArgs> OpeningFile;

        internal static void OnOpeningFile(object sender, FileOpenEventArgs e)
        {
            OpeningFile?.Invoke(sender, e);
        }
    }
}