using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Celeste.Unity.Editor
{
    public static class WebDataArchive
    {
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("UnityWebData1.0\0");
        private static readonly UTF8Encoding PathEncoding = new UTF8Encoding(false, true);

        private sealed class Entry
        {
            internal readonly string Path;
            internal byte[] Bytes;

            internal Entry(string path, byte[] bytes)
            {
                Path = path;
                Bytes = bytes;
            }
        }

        public static void IncludeGeneratedResources(string archivePath, string resourceDirectory)
        {
            if (!Directory.Exists(resourceDirectory))
                throw new DirectoryNotFoundException(resourceDirectory);
            string[] resourceFiles = Directory.GetFiles(resourceDirectory, "*-resources.dat");
            if (resourceFiles.Length == 0)
                throw new InvalidDataException("The generated IL2CPP resource directory is empty.");
            Array.Sort(resourceFiles, StringComparer.Ordinal);
            List<Entry> entries = ReadArchive(archivePath);
            var byPath = entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
            foreach (string file in resourceFiles)
            {
                string path = "Il2CppData/Resources/" + System.IO.Path.GetFileName(file);
                byte[] bytes = File.ReadAllBytes(file);
                if (byPath.TryGetValue(path, out var existing))
                {
                    if (!existing.Bytes.SequenceEqual(bytes)) existing.Bytes = bytes;
                }
                else
                {
                    var entry = new Entry(path, bytes);
                    entries.Add(entry);
                    byPath.Add(path, entry);
                }
            }

            WriteArchive(archivePath, entries);
        }

        private static List<Entry> ReadArchive(string archivePath)
        {
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var contents = new MemoryStream();
            gzip.CopyTo(contents);
            byte[] data = contents.ToArray();
            using var input = new MemoryStream(data, false);
            using var reader = new BinaryReader(input, PathEncoding);
            if (!reader.ReadBytes(Signature.Length).SequenceEqual(Signature))
                throw new InvalidDataException("The Web data archive has no UnityWebData1.0 signature.");
            int headerSize = reader.ReadInt32();
            if (headerSize < Signature.Length + sizeof(int) || headerSize > data.Length)
                throw new InvalidDataException("The Web data archive has an invalid header size.");
            var entries = new List<Entry>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            while (input.Position < headerSize)
            {
                if (headerSize - input.Position < 3 * sizeof(int))
                    throw new InvalidDataException("The Web data archive has a truncated entry header.");
                int offset = reader.ReadInt32();
                int size = reader.ReadInt32();
                int pathLength = reader.ReadInt32();
                if (pathLength <= 0 || pathLength > headerSize - input.Position || offset < headerSize ||
                    size < 0 || (long)offset + size > data.Length)
                    throw new InvalidDataException("The Web data archive has an invalid entry range.");
                string path = PathEncoding.GetString(reader.ReadBytes(pathLength));
                if (!paths.Add(path)) throw new InvalidDataException("The Web data archive has a duplicate path.");
                byte[] bytes = new byte[size];
                Buffer.BlockCopy(data, offset, bytes, 0, size);
                entries.Add(new Entry(path, bytes));
            }

            return entries;
        }

        private static void WriteArchive(string archivePath, List<Entry> entries)
        {
            int headerSize = Signature.Length + sizeof(int);
            foreach (var entry in entries)
                headerSize = checked(headerSize + 3 * sizeof(int) + PathEncoding.GetByteCount(entry.Path));
            using var contents = new MemoryStream();
            using (var writer = new BinaryWriter(contents, PathEncoding, true))
            {
                writer.Write(Signature);
                writer.Write(headerSize);
                int offset = headerSize;
                foreach (var entry in entries)
                {
                    byte[] name = PathEncoding.GetBytes(entry.Path);
                    writer.Write(offset);
                    writer.Write(entry.Bytes.Length);
                    writer.Write(name.Length);
                    writer.Write(name);
                    offset = checked(offset + entry.Bytes.Length);
                }

                foreach (var entry in entries) writer.Write(entry.Bytes);
            }

            string temporary = archivePath + ".resources.tmp";
            bool createdTemporary = false;
            try
            {
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                {
                    createdTemporary = true;
                    using var gzip = new GZipStream(file, CompressionLevel.Optimal);
                    contents.Position = 0;
                    contents.CopyTo(gzip);
                }

                File.Replace(temporary, archivePath, null);
            }
            finally
            {
                if (createdTemporary && File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
