using System;
using System.Security.Cryptography;
using System.Text;

namespace PseudoGuid;

internal sealed class KindHash<TKind> where TKind : struct, Enum
{
    private readonly byte[] _hash;

    public KindHash(HashAlgorithm hashAlgorithm, TKind kind)
    {
        ArgumentNullExceptionPolyfill.ThrowIfNull(hashAlgorithm);

        if (!EnumPolyfill.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, $"'{kind}' is not a defined value of {typeof(TKind).Name}.");
        }

        Kind = kind;
        KindString = kind.ToString();

        // ASCII is intentional: changing the encoding would change every generated identifier.
        _hash = hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(KindString));
    }

    public TKind Kind { get; }

    public string KindString { get; }

    public ReadOnlyMemory<byte> GetHash(int? take)
    {
        if (take is null)
        {
            return _hash;
        }

        if (take.Value < 0 || take.Value > _hash.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take.Value, $"The hash algorithm produced {_hash.Length} bytes, but {take.Value} were requested.");
        }

        return _hash.AsMemory(0, take.Value);
    }
}
