using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CustomAttribute = Mono.Cecil.CustomAttribute;
using TypeDefinition = Mono.Cecil.TypeDefinition;
using ModuleDefinition = Mono.Cecil.ModuleDefinition;

namespace Celeste.Tools.DependencyIsolation;

internal static class DependencyIsolation
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static void Run(string input, string output, string mapPath, string cecilKeyPath, string monoModKeyPath)
    {
        if (!Directory.Exists(input)) throw new DirectoryNotFoundException(input);
        if (output.Equals(input, StringComparison.OrdinalIgnoreCase)
            || output.StartsWith(input + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must be separate from input.");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("Output directory must be empty.");
        var map = JsonSerializer.Deserialize<IdentityMap>(File.ReadAllText(mapPath), Json)
                  ?? throw new InvalidDataException("Missing identity map.");
        var entries = ValidateMap(map);
        var cecilKey = new StrongNameKey(cecilKeyPath);
        var monoModKey = new StrongNameKey(monoModKeyPath);
        string cecilToken = Token(cecilKey.PublicKey);
        if (entries.Values.Any(e => e.SourcePublicKeyToken != cecilToken))
            throw new InvalidDataException("Cecil key does not match the reviewed public key token.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var manifest = new List<object>();
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(input);
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        var paths = Directory.GetFiles(input, "*.dll").Order(StringComparer.Ordinal).ToArray();
        if (paths.Length == 0) throw new InvalidDataException("No managed inputs.");
        foreach (string path in paths)
        {
            byte[] source = File.ReadAllBytes(path);
            var sourcePdb = Path.ChangeExtension(path, ".pdb");
            using var assembly =
                AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
            string sourceIdentity = assembly.Name.FullName;
            bool rename = entries.TryGetValue(assembly.Name.Name, out var definition);
            bool rewrite = rename || assembly.MainModule.AssemblyReferences.Any(r => entries.ContainsKey(r.Name));
            if (!rewrite)
            {
                Add(outputs, Path.GetFileName(path), source);
                if (File.Exists(sourcePdb)) Add(outputs, Path.GetFileName(sourcePdb), File.ReadAllBytes(sourcePdb));
                manifest.Add(new
                {
                    sourceFileName = Path.GetFileName(path), targetFileName = Path.GetFileName(path),
                    sourceIdentity, targetIdentity = sourceIdentity, rewritten = false,
                    sourceSha256 = Hash(source), targetSha256 = Hash(source),
                    sourcePdbSha256 = File.Exists(sourcePdb) ? Hash(File.ReadAllBytes(sourcePdb)) : null,
                    targetPdbSha256 = File.Exists(sourcePdb) ? Hash(File.ReadAllBytes(sourcePdb)) : null,
                    pdbFileName = File.Exists(sourcePdb) ? Path.GetFileName(sourcePdb) : null,
                    targetReferences = assembly.MainModule.AssemblyReferences.Select(r => new
                    {
                        name = r.Name, version = r.Version.ToString(),
                        publicKeyToken = Convert.ToHexString(r.PublicKeyToken).ToLowerInvariant()
                    }).ToArray()
                });
                continue;
            }

            if (assembly.Modules.Count != 1)
                throw new InvalidDataException("Multi-module rewriting is outside the reviewed scope.");
            using (var peStream = new MemoryStream(source, false))
            using (var pe = new PEReader(peStream))
            {
                if (pe.PEHeaders.PEHeader!.CertificateTableDirectory.Size != 0)
                    throw new InvalidDataException("No Authenticode signing key is available for " + sourceIdentity);
            }

            StrongNameKey? key = null;
            if (assembly.Name.HasPublicKey)
            {
                if (assembly.Name.PublicKey.SequenceEqual(cecilKey.PublicKey)) key = cecilKey;
                else if (assembly.Name.PublicKey.SequenceEqual(monoModKey.PublicKey)) key = monoModKey;
                else throw new InvalidDataException("No matching legitimate signing key for " + sourceIdentity);
            }
            else if ((assembly.MainModule.Attributes & ModuleAttributes.StrongNameSigned) != 0)
                throw new InvalidDataException("Signed module has no public key: " + sourceIdentity);

            if (rename && key != cecilKey) throw new InvalidDataException("Cecil definition signing key mismatch.");
            bool embedded;
            bool symbols = HasSymbols(path, out embedded);
            if (symbols) assembly.MainModule.ReadSymbols();
            int points = CountSequencePoints(assembly.MainModule);
            string[] types = Types(assembly.MainModule.Types).Select(t => t.FullName).ToArray();
            string sourceContract = Contract(assembly.MainModule);
            int references = 0;
            foreach (var reference in assembly.MainModule.AssemblyReferences)
            {
                if (!entries.TryGetValue(reference.Name, out var entry)) continue;
                if (!Convert.ToHexString(reference.PublicKeyToken).Equals(entry.SourcePublicKeyToken,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unqualified Cecil reference " + reference.FullName);
                reference.Name = entry.TargetName;
                reference.Version = Version.Parse(entry.TargetVersion);
                if (reference.HasPublicKey) reference.PublicKey = cecilKey.PublicKey;
                else reference.PublicKeyToken = Convert.FromHexString(cecilToken);
                references++;
            }

            int friends = RewriteFriends(assembly.CustomAttributes, entries, cecilKey.PublicKey);
            if (rename)
            {
                if (assembly.Name.Version != Version.Parse(definition!.TargetVersion))
                    throw new InvalidDataException("Only reviewed Cecil 0.11.6 definitions may be renamed.");
                assembly.Name.Name = definition.TargetName;
                assembly.MainModule.Name = definition.TargetName + ".dll";
            }

            string fileName = rename ? definition!.TargetName + ".dll" : Path.GetFileName(path);
            using var peOutput = new MemoryStream();
            using var pdbOutput = new MemoryStream();
            var writer = new WriterParameters { DeterministicMvid = true, WriteSymbols = symbols };
            if (key != null) writer.StrongNameKeyBlob = key.Blob;
            if (symbols)
            {
                writer.SymbolWriterProvider = embedded
                    ? new EmbeddedPortablePdbWriterProvider()
                    : new PortablePdbWriterProvider();
                if (!embedded) writer.SymbolStream = pdbOutput;
            }

            assembly.Write(peOutput, writer);
            byte[] target = peOutput.ToArray();
            byte[]? pdb = symbols && !embedded ? pdbOutput.ToArray() : null;
            ValidateOutput(target, pdb, embedded, assembly.Name.FullName, types, sourceContract, points, key != null,
                entries);
            Add(outputs, fileName, target);
            if (pdb != null) Add(outputs, Path.ChangeExtension(fileName, ".pdb"), pdb);
            manifest.Add(new
            {
                sourceFileName = Path.GetFileName(path), targetFileName = fileName, sourceIdentity,
                targetIdentity = assembly.Name.FullName, rewritten = true,
                sourceSha256 = Hash(source), targetSha256 = Hash(target),
                sourcePdbSha256 = File.Exists(sourcePdb) ? Hash(File.ReadAllBytes(sourcePdb)) : null,
                targetPdbSha256 = pdb == null ? null : Hash(pdb), symbols, embedded,
                sequencePoints = points, rewrittenReferences = references, rewrittenFriends = friends,
                signed = key != null, pdbFileName = pdb == null ? null : Path.ChangeExtension(fileName, ".pdb"),
                targetReferences = assembly.MainModule.AssemblyReferences.Select(r => new
                {
                    name = r.Name, version = r.Version.ToString(),
                    publicKeyToken = Convert.ToHexString(r.PublicKeyToken).ToLowerInvariant()
                }).ToArray()
            });
        }

        // All validation precedes publication. Caller owns staging promotion/cleanup.
        Directory.CreateDirectory(output);
        foreach (var item in outputs) File.WriteAllBytes(Path.Combine(output, item.Key), item.Value);
        File.WriteAllText(Path.Combine(output, "IsolationManifest.json"),
            JsonSerializer.Serialize(new
            {
                schema = 1, identityMapSha256 = Hash(File.ReadAllBytes(mapPath)),
                cecilKeySha256 = Hash(cecilKey.Blob), monoModKeySha256 = Hash(monoModKey.Blob), assemblies = manifest
            }, Json));
        Console.WriteLine($"Validated {manifest.Count} assemblies; staged {outputs.Count} files.");
    }

    private static Dictionary<string, IdentityMapEntry> ValidateMap(IdentityMap map)
    {
        string[] suffixes = ["", ".Pdb", ".Mdb", ".Rocks"];
        if (map.Schema != 1 || map.Assemblies.Length != 4)
            throw new InvalidDataException("Expected fixed four-family schema 1.");
        var entries = map.Assemblies.ToDictionary(e => e.SourceName, StringComparer.Ordinal);
        foreach (var suffix in suffixes)
        {
            if (!entries.TryGetValue("Mono.Cecil" + suffix, out var entry)
                || entry.TargetName != "Celeste.Reflection.Cecil" + suffix
                || entry.TargetVersion != "0.11.6.0" || entry.SourcePublicKeyToken != "50cebf1cceb9d05e")
                throw new InvalidDataException("Identity map is outside the reviewed scope.");
        }

        return entries;
    }

    private static int RewriteFriends(IEnumerable<CustomAttribute> attributes,
        Dictionary<string, IdentityMapEntry> entries, byte[] publicKey)
    {
        int count = 0;
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName !=
                "System.Runtime.CompilerServices.InternalsVisibleToAttribute") continue;
            var name = new AssemblyName((string)attribute.ConstructorArguments[0].Value);
            if (name.Name == null || !entries.TryGetValue(name.Name, out var entry)) continue;
            if (name.GetPublicKey() is not { Length: > 0 } key || !key.SequenceEqual(publicKey))
                throw new InvalidDataException("Unqualified Cecil friend assembly.");
            attribute.ConstructorArguments[0] = new CustomAttributeArgument(attribute.ConstructorArguments[0].Type,
                entry.TargetName + ", PublicKey=" + Convert.ToHexString(publicKey).ToLowerInvariant());
            count++;
        }

        return count;
    }

    private static bool HasSymbols(string path, out bool embedded)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var debug = pe.ReadDebugDirectory();
        embedded = debug.Any(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
        string pdb = Path.ChangeExtension(path, ".pdb");
        if (embedded && File.Exists(pdb)) throw new InvalidDataException("Ambiguous embedded and external symbols.");
        if (!embedded && debug.Any(e => e.Type == DebugDirectoryEntryType.CodeView) && !File.Exists(pdb))
            throw new InvalidDataException("CodeView has no supplied symbols: " + path);
        if (File.Exists(pdb))
        {
            using var symbols = File.OpenRead(pdb);
            using var reader = MetadataReaderProvider.FromPortablePdbStream(symbols);
            _ = reader.GetMetadataReader(); // Native/MDB symbols are outside this tool's reviewed scope.
        }

        return embedded || File.Exists(pdb);
    }

    private static void ValidateOutput(byte[] bytes, byte[]? symbols, bool embedded, string identity,
        string[] types, string sourceContract, int points, bool signed, Dictionary<string, IdentityMapEntry> entries)
    {
        using var stream = new MemoryStream(bytes, false);
        using var symbolStream = symbols == null ? null : new MemoryStream(symbols, false);
        var parameters = new ReaderParameters { ReadSymbols = embedded || symbols != null };
        if (symbolStream != null) parameters.SymbolStream = symbolStream;
        using var assembly = AssemblyDefinition.ReadAssembly(stream, parameters);
        if (assembly.Name.FullName != identity
            || !types.SequenceEqual(Types(assembly.MainModule.Types).Select(t => t.FullName))
            || Contract(assembly.MainModule) != sourceContract
            || CountSequencePoints(assembly.MainModule) != points
            || assembly.MainModule.AssemblyReferences.Any(r => entries.ContainsKey(r.Name))
            || ((assembly.MainModule.Attributes & ModuleAttributes.StrongNameSigned) != 0) != signed)
            throw new InvalidDataException("Rewritten PE/symbol contract mismatch: " + identity);
        using var peStream = new MemoryStream(bytes, false);
        using var pe = new PEReader(peStream);
        if (symbols != null)
        {
            using var pdbStream = new MemoryStream(symbols, false);
            using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            var id = provider.GetMetadataReader().DebugMetadataHeader!.Id;
            var codeViews = pe.ReadDebugDirectory().Where(e => e.Type == DebugDirectoryEntryType.CodeView).ToArray();
            if (codeViews.Length != 1 || !pe.ReadCodeViewDebugDirectoryData(codeViews[0]).Guid.ToByteArray()
                    .SequenceEqual(id.Take(16)) || pe.ReadCodeViewDebugDirectoryData(codeViews[0]).Age != 1)
                throw new InvalidDataException("PE CodeView does not match emitted portable PDB.");
        }

        if (signed)
        {
            var directory = pe.PEHeaders.CorHeader!.StrongNameSignatureDirectory;
            if (directory.Size == 0 || pe.GetSectionData(directory.RelativeVirtualAddress).GetContent(0, directory.Size)
                    .All(b => b == 0))
                throw new InvalidDataException("Missing strong-name signature.");
            VerifySignature(bytes, pe, assembly.Name.PublicKey);
        }
    }

    private static void VerifySignature(byte[] bytes, PEReader pe, byte[] publicKey)
    {
        var directory = pe.PEHeaders.CorHeader!.StrongNameSignatureDirectory;
        var section = pe.PEHeaders.SectionHeaders.Single(s => directory.RelativeVirtualAddress >= s.VirtualAddress
                                                              && directory.RelativeVirtualAddress <
                                                              s.VirtualAddress + s.SizeOfRawData);
        int signatureOffset = section.PointerToRawData + directory.RelativeVirtualAddress - section.VirtualAddress;
        int peOffset = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x3c));
        int headerEnd = peOffset + 4 + 20 + pe.PEHeaders.CoffHeader.SizeOfOptionalHeader
                        + pe.PEHeaders.CoffHeader.NumberOfSections * 40;
        int firstSection = pe.PEHeaders.SectionHeaders.Min(s => s.PointerToRawData);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(bytes, 0, headerEnd);
        hash.AppendData(bytes, firstSection, signatureOffset - firstSection);
        hash.AppendData(bytes, signatureOffset + directory.Size, bytes.Length - signatureOffset - directory.Size);
        byte[] digest = hash.GetHashAndReset();
        byte[] signature = bytes.AsSpan(signatureOffset, directory.Size).ToArray();
        Array.Reverse(signature);
        int size = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(publicKey.AsSpan(24))) /
                   8;
        byte[] modulus = publicKey.AsSpan(32, size).ToArray();
        Array.Reverse(modulus);
        byte[] exponent = publicKey.AsSpan(28, 4).ToArray();
        Array.Reverse(exponent);
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
            { Modulus = modulus, Exponent = exponent.SkipWhile(b => b == 0).ToArray() });
        if (!rsa.VerifyHash(digest, signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1))
            throw new InvalidDataException("Rewritten PE strong-name signature is invalid.");
    }

    private static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in Types(type.NestedTypes)) yield return nested;
        }
    }

    private static int CountSequencePoints(ModuleDefinition module)
        => Types(module.Types).Sum(t => t.Methods.Sum(m => m.DebugInformation.SequencePoints.Count));

    private static string Contract(ModuleDefinition module)
    {
        static string Operand(object? value) => value switch
        {
            Instruction target => "IL_" + target.Offset,
            Instruction[] targets => string.Join(",", targets.Select(t => t.Offset)),
            IFormattable formatted => formatted.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? ""
        };

        var contract = Types(module.Types).Select(type => new
        {
            name = type.FullName, attributes = type.Attributes.ToString(), baseType = type.BaseType?.FullName,
            interfaces = type.Interfaces.Select(i => i.InterfaceType.FullName).ToArray(),
            fields = type.Fields.Select(f => new
            {
                name = f.FullName, attributes = f.Attributes.ToString(),
                constant = f.HasConstant ? Operand(f.Constant) : null
            }).ToArray(),
            methods = type.Methods.Select(m => new
            {
                name = m.FullName, attributes = m.Attributes.ToString(), impl = m.ImplAttributes.ToString(),
                instructions = m.HasBody
                    ? m.Body.Instructions.Select(i => i.OpCode.Code + ":" + Operand(i.Operand)).ToArray()
                    : null,
                variables = m.HasBody ? m.Body.Variables.Select(v => v.VariableType.FullName).ToArray() : null,
                handlers = m.HasBody
                    ? m.Body.ExceptionHandlers.Select(h => new
                    {
                        kind = h.HandlerType.ToString(),
                        catchType = h.CatchType?.FullName, start = h.TryStart?.Offset, end = h.TryEnd?.Offset,
                        handlerStart = h.HandlerStart?.Offset, handlerEnd = h.HandlerEnd?.Offset,
                        filter = h.FilterStart?.Offset
                    }).ToArray()
                    : null
            }).ToArray()
        }).ToArray();
        return Hash(JsonSerializer.SerializeToUtf8Bytes(contract));
    }

    private static string Token(byte[] key) =>
        Convert.ToHexString(SHA1.HashData(key).TakeLast(8).Reverse().ToArray()).ToLowerInvariant();

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void Add(Dictionary<string, byte[]> outputs, string name, byte[] bytes)
    {
        if (!outputs.TryAdd(name, bytes)) throw new InvalidDataException("Conflicting output filename: " + name);
    }
}
