using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;

namespace Celeste.Unity
{
    // XML is resource metadata only. SpriteBank construction remains at the
    // original dialogue call site, with that scene's current random source.
    public static class RoomDialogueMetadata
    {
        public sealed class Prepared
        {
            internal readonly string Directory;
            internal readonly XmlDocument Portraits;

            internal Prepared(string directory, XmlDocument portraits)
            {
                Directory = directory;
                Portraits = portraits;
            }
        }

        private static readonly object gate = new object();

        private static readonly Dictionary<string, Prepared> sources =
            new Dictionary<string, Prepared>(StringComparer.OrdinalIgnoreCase);

        private static string Key(string content) => Path.GetFullPath(content)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Worker-safe: neither publishes incomplete work nor constructs sprites.
        public static Prepared Prepare(string content, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            string directory = Key(content);
            lock (gate)
                if (sources.TryGetValue(directory, out var source))
                    return source;
            var xml = new XmlDocument();
            xml.Load(Path.Combine(directory, "Graphics/Portraits.xml"));
            token.ThrowIfCancellationRequested();
            return new Prepared(directory, xml);
        }

        public static void Publish(Prepared prepared)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            lock (gate)
                if (!sources.ContainsKey(prepared.Directory))
                    sources.Add(prepared.Directory, prepared);
        }

        // Call only after all preparation workers and scene views have retired.
        public static void Invalidate(string content)
        {
            lock (gate) sources.Remove(Key(content));
        }

        public static XmlDocument ReadView(string content)
        {
            var prepared = Prepare(content);
            Publish(prepared);
            return (XmlDocument)prepared.Portraits.CloneNode(true);
        }
    }
}
