using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Celeste.Tools.DependencyIsolation;

internal sealed class StrongNameKey
{
    public byte[] Blob { get; }
    public byte[] PublicKey { get; }

    public StrongNameKey(string path)
    {
        Blob = File.ReadAllBytes(path);
        if (Blob.Length < 20 || Blob[0] != 7 || Blob[1] != 2
            || BinaryPrimitives.ReadUInt32LittleEndian(Blob.AsSpan(8)) != 0x32415352)
            throw new InvalidDataException("Expected RSA PRIVATEKEYBLOB: " + path);
        int bits = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(Blob.AsSpan(12)));
        int modulusBytes = bits / 8;
        if (bits < 512 || bits > 16384 || bits % 16 != 0
            || Blob.Length != 20 + modulusBytes * 9 / 2)
            throw new InvalidDataException("Invalid RSA key size: " + path);
        PublicKey = new byte[12 + 20 + modulusBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(PublicKey, 0x2400);
        BinaryPrimitives.WriteUInt32LittleEndian(PublicKey.AsSpan(4), 0x8004);
        BinaryPrimitives.WriteUInt32LittleEndian(PublicKey.AsSpan(8), (uint)(20 + modulusBytes));
        Blob.AsSpan(0, 20 + modulusBytes).CopyTo(PublicKey.AsSpan(12));
        PublicKey[12] = 6;
        BinaryPrimitives.WriteUInt32LittleEndian(PublicKey.AsSpan(20), 0x31415352);

        // Verify that this is a complete private key, not a matching public blob.
        byte[] Reverse(int offset, int size)
        {
            var bytes = Blob.AsSpan(offset, size).ToArray();
            Array.Reverse(bytes);
            return bytes;
        }

        var exponent = Reverse(16, 4).SkipWhile(b => b == 0).ToArray();
        int half = modulusBytes / 2;
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Reverse(20, modulusBytes), Exponent = exponent,
            P = Reverse(20 + modulusBytes, half), Q = Reverse(20 + modulusBytes + half, half),
            DP = Reverse(20 + modulusBytes + half * 2, half),
            DQ = Reverse(20 + modulusBytes + half * 3, half),
            InverseQ = Reverse(20 + modulusBytes + half * 4, half),
            D = Reverse(20 + modulusBytes + half * 5, modulusBytes)
        });
        byte[] proof = [1, 2, 3];
        if (!rsa.VerifyData(proof, rsa.SignData(proof, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new InvalidDataException("Private key proof failed.");
    }
}
