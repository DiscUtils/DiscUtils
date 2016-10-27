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
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DiskClone
{
    static internal class Win32Wrapper
    {
        public static SafeFileHandle OpenFileHandle(string path)
        {
            SafeFileHandle handle = NativeMethods.CreateFileW(path, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw Win32Wrapper.GetIOExceptionForLastError();
            }
            return handle;
        }

        public static NativeMethods.DiskGeometry GetDiskGeometry(SafeFileHandle handle)
        {
            NativeMethods.DiskGeometry diskGeometry = new NativeMethods.DiskGeometry();
            int bytesRet = Marshal.SizeOf(diskGeometry);
            if (!NativeMethods.DeviceIoControl(handle, NativeMethods.EIOControlCode.DiskGetDriveGeometry, null, 0, diskGeometry, bytesRet, ref bytesRet, IntPtr.Zero))
            {
                throw Win32Wrapper.GetIOExceptionForLastError();
            }
            return diskGeometry;
        }

        public static NativeMethods.NtfsVolumeData GetNtfsVolumeData(SafeFileHandle volumeHandle)
        {
            NativeMethods.NtfsVolumeData volumeData = new NativeMethods.NtfsVolumeData();
            int bytesRet = Marshal.SizeOf(volumeData);
            if (!NativeMethods.DeviceIoControl(volumeHandle, NativeMethods.EIOControlCode.FsctlGetNtfsVolumeData, null, 0, volumeData, bytesRet, ref bytesRet, IntPtr.Zero))
            {
                throw Win32Wrapper.GetIOExceptionForLastError();
            }
            return volumeData;
        }

        public static long GetDiskCapacity(SafeFileHandle diskHandle)
        {
            byte[] sizeBytes = new byte[8];
            int bytesRet = sizeBytes.Length;
            if (!NativeMethods.DeviceIoControl(diskHandle, NativeMethods.EIOControlCode.DiskGetLengthInfo, null, 0, sizeBytes, bytesRet, ref bytesRet, IntPtr.Zero))
            {
                throw Win32Wrapper.GetIOExceptionForLastError();
            }

            return BitConverter.ToInt64(sizeBytes, 0);
        }

        public static string GetMessageForError(int code)
        {
            IntPtr buffer = new IntPtr();

            try
            {
                NativeMethods.FormatMessageW(
                    NativeMethods.FORMAT_MESSAGE_ALLOCATE_BUFFER | NativeMethods.FORMAT_MESSAGE_FROM_SYSTEM | NativeMethods.FORMAT_MESSAGE_IGNORE_INSERTS,
                    IntPtr.Zero,
                    code,
                    0,
                    ref buffer,
                    0,
                    IntPtr.Zero
                    );

                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    NativeMethods.LocalFree(buffer);
                }
            }
        }

        internal static Exception GetIOExceptionForLastError()
        {
            int lastError = Marshal.GetLastWin32Error();
            int lastErrorHr = Marshal.GetHRForLastWin32Error();
            return new IOException(GetMessageForError(lastError), lastErrorHr);
        }

        internal static T[] ByteArrayToStructureArray<T>(byte[] data, int offset, int count)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                int elemSize = Marshal.SizeOf(typeof(T));
                if (count * elemSize + offset > data.Length)
                {
                    throw new ArgumentException("Attempting to read too many elements from byte array");
                }

                IntPtr basePtr = handle.AddrOfPinnedObject();

                T[] result = new T[count];
                for (int i = 0; i < count; ++i)
                {
                    IntPtr elemPtr = new IntPtr(basePtr.ToInt64() + (elemSize * i) + offset);
                    result[i] = (T)Marshal.PtrToStructure(elemPtr, typeof(T));
                }

                return result;
            }
            finally
            {
                handle.Free();
            }
        } 

    }
}
