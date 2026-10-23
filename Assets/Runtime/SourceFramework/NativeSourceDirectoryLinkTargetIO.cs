using System;
using System.IO;
using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Celeste.Everest.Runtime.Startup;

namespace Celeste.Unity
{
    internal sealed class NativeSourceDirectoryLinkTargetIO : IEverestInitializationSaveLinkIO
    {
        private readonly Func<DirectoryInfo, string> managedLinkTarget;

        internal NativeSourceDirectoryLinkTargetIO()
        {
            var getter = typeof(DirectoryInfo).GetProperty("LinkTarget", BindingFlags.Instance | BindingFlags.Public)
                ?.GetGetMethod();
            if (getter != null)
                managedLinkTarget =
                    (Func<DirectoryInfo, string>)getter.CreateDelegate(typeof(Func<DirectoryInfo, string>));
        }

        public string GetDirectoryLinkTarget(DirectoryInfo directory)
        {
            if (directory == null) throw new ArgumentNullException(nameof(directory));
            if (managedLinkTarget != null) return managedLinkTarget(directory);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return ReadPosixLink(directory.FullName);

            // DirectoryInfo.LinkTarget uses the immediate substitute name, not the display name or final target.
            using (var handle = CreateFileW(ExtendedPath(directory.FullName), 0, 7, IntPtr.Zero, 3, 0x02200000,
                       IntPtr.Zero))
            {
                // The original .NET GetLinkTarget requests throwOnError=false for these physical operations.
                if (handle.IsInvalid) return null;
                var buffer = ArrayPool<byte>.Shared.Rent(16384);
                try
                {
                    if (!DeviceIoControl(handle, 0x000900A8, IntPtr.Zero, 0, buffer, 16384, out int returned,
                            IntPtr.Zero))
                        return null;
                    if (returned < 8) throw new IOException("The reparse-point device returned an incomplete header.");
                    uint tag = BitConverter.ToUInt32(buffer, 0);
                    int pathStart;
                    bool relative = false;
                    if (tag == 0xA000000C) // IO_REPARSE_TAG_SYMLINK
                    {
                        pathStart = 20;
                        if (returned < pathStart) throw new IOException("The symbolic-link buffer is incomplete.");
                        relative = (BitConverter.ToUInt32(buffer, 16) & 1) != 0;
                    }
                    else if (tag == 0xA0000003) // IO_REPARSE_TAG_MOUNT_POINT (junction / volume mount)
                    {
                        pathStart = 16;
                        if (returned < pathStart) throw new IOException("The mount-point buffer is incomplete.");
                    }
                    else return null; // Other reparse tags, including cloud placeholders, are not links.

                    int offset = pathStart + BitConverter.ToUInt16(buffer, 8);
                    int length = BitConverter.ToUInt16(buffer, 10);
                    if ((offset & 1) != 0 || (length & 1) != 0 || offset > returned || length > returned - offset)
                        throw new IOException("The reparse-point substitute name is outside the returned buffer.");
                    string target = MemoryMarshal.Cast<byte, char>(buffer.AsSpan(offset, length)).ToString();
                    if (relative) return target;
                    if (target.StartsWith(@"\??\UNC\", StringComparison.Ordinal)) return @"\\" + target.Substring(8);
                    if (!target.StartsWith(@"\??\", StringComparison.Ordinal))
                        throw new IOException("The absolute link target has no NT path prefix.");
                    return target.Substring(4);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        private static string ExtendedPath(string path)
        {
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return @"\\?\UNC\" + path.Substring(2);
            return @"\\?\" + path;
        }

        private static string ReadPosixLink(string path)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                while (true)
                {
                    long length = readlink(path, buffer, (UIntPtr)buffer.Length).ToInt64();
                    if (length < 0) return null;
                    if (length < buffer.Length)
                        return System.Text.Encoding.UTF8.GetString(buffer, 0, checked((int)length));
                    if (buffer.Length >= 1048576)
                        throw new IOException("The symbolic-link target exceeds the platform path limit.");
                    int size = checked(buffer.Length * 2);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = null;
                    buffer = ArrayPool<byte>.Shared.Rent(size);
                }
            }
            finally
            {
                if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern IntPtr readlink([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte[] buffer,
            UIntPtr length);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(SafeFileHandle handle, uint controlCode, IntPtr inputBuffer,
            int inputSize, [Out] byte[] outputBuffer, int outputSize, out int bytesReturned, IntPtr overlapped);
    }
}
