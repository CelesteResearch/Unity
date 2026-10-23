using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using Celeste.Core.Framework.Time;

namespace Celeste.Unity.Web
{
    internal sealed class UnityWebFrameworkTimeResources : ISourceFrameworkTimeResources
    {
        private readonly Dictionary<string, string> strings = new Dictionary<string, string>(StringComparer.Ordinal);

        public UnityWebFrameworkTimeResources(byte[] canonicalResourceBytes)
        {
            using var stream =
                new MemoryStream(
                    canonicalResourceBytes ?? throw new ArgumentNullException(nameof(canonicalResourceBytes)), false);
            using var reader = new ResourceReader(stream);
            foreach (string name in new[] { "Arg_CannotBeNaN", "Overflow_TimeSpanTooLong" })
            {
                reader.GetResourceData(name, out string type, out byte[] data);
                if (type != "ResourceTypeCode.String")
                    throw new InvalidDataException("Framework time resource is not a string: " + name);
                using var valueStream = new MemoryStream(data, false);
                using var valueReader = new BinaryReader(valueStream);
                strings.Add(name, valueReader.ReadString());
            }

            _ = GetResourceString("Arg_CannotBeNaN");
            _ = GetResourceString("Overflow_TimeSpanTooLong");
        }

        public string GetResourceString(string name) => strings.TryGetValue(name, out string value)
            ? value
            : throw new MissingManifestResourceException(name);
    }
}
