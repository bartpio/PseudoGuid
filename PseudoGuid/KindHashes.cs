using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;

namespace PseudoGuid;

/// <summary>
/// Deals with all of the prefix hashes for a particular enum type.
/// Instances are immutable and safe to share across threads.
/// </summary>
/// <typeparam name="TKind">
/// An enum type. Each defined value must have a distinct underlying value (aliases are rejected),
/// since the prefix is derived from the value's name.
/// </typeparam>
public sealed class KindHashes<TKind> where TKind : struct, Enum
{
    /// <summary>
    /// The size of a <see cref="Guid"/>, in bytes.
    /// </summary>
    public const int TotalSize = 16;

    private readonly ReadOnlyDictionary<TKind, KindHash<TKind>> _map;

    /// <summary>
    /// Construct using MD5 as the hash algorithm (the conventional choice).
    /// MD5 is used only to derive prefixes, not for any security purpose.
    /// </summary>
    public KindHashes()
    {
#pragma warning disable CA5351 // MD5 derives stable prefixes; it is not used for security.
        using var md5 = MD5.Create();
#pragma warning restore CA5351
        _map = BuildMap(md5);
    }

    /// <summary>
    /// Construct using a specific hash algorithm.
    /// </summary>
    /// <param name="hashAlgorithm">
    /// Hash algorithm to use (generally MD5). It is only used during construction, and is not disposed;
    /// the caller retains ownership. It must produce at least 12 bytes to be usable with <see cref="int"/> sequences.
    /// </param>
    public KindHashes(HashAlgorithm hashAlgorithm)
    {
        ArgumentNullExceptionPolyfill.ThrowIfNull(hashAlgorithm);
        _map = BuildMap(hashAlgorithm);
    }

    private static ReadOnlyDictionary<TKind, KindHash<TKind>> BuildMap(HashAlgorithm hashAlgorithm)
    {
        var map = new Dictionary<TKind, KindHash<TKind>>();
        foreach (var kind in EnumPolyfill.GetValues<TKind>())
        {
            if (map.TryGetValue(kind, out var existing))
            {
                var alias = EnumPolyfill.GetNames<TKind>().Where(n => EnumPolyfill.Parse<TKind>(n).Equals(kind));
                throw new PseudoGuidException(
                    $"{typeof(TKind).Name} has multiple names ({string.Join(", ", alias)}) for the underlying value {existing.Kind:D}. " +
                    "Aliased enum values are not supported, because the prefix is derived from the value's name.");
            }

            map.Add(kind, new KindHash<TKind>(hashAlgorithm, kind));
        }

        return new ReadOnlyDictionary<TKind, KindHash<TKind>>(map);
    }

    /// <summary>
    /// The enum values covered by this instance.
    /// </summary>
    public IEnumerable<TKind> Kinds => _map.Keys;

    /// <summary>
    /// Get the hash for a particular enum value.
    /// </summary>
    /// <param name="kind">
    /// A particular enum value.
    /// </param>
    /// <param name="take">
    /// Length of hash desired, or <see langword="null"/> for the full hash.
    /// </param>
    /// <returns>
    /// Hash of specified enum value, trimmed to specified length.
    /// </returns>
    public ReadOnlyMemory<byte> GetHash(TKind kind, int? take)
    {
        if (_map.TryGetValue(kind, out var ekh))
        {
            return ekh.GetHash(take);
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, $"'{kind}' is not a defined value of {typeof(TKind).Name}.");
    }

    /// <summary>
    /// Get a map of enum value to hash, for every enum value.
    /// </summary>
    /// <param name="take">
    /// Length of hashes desired.
    /// </param>
    /// <param name="modder">
    /// Optional function to apply to each hash (such as to apply an RFC4122 based version mod).
    /// </param>
    /// <returns>
    /// Map of enum value to hash, for every enum value.
    /// </returns>
    /// <exception cref="PseudoGuidException">The resulting hashes are not unique.</exception>
    public ReadOnlyDictionary<TKind, ReadOnlyMemory<byte>> GetEncoderMap(int? take, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? modder)
    {
        var map = new Dictionary<TKind, ReadOnlyMemory<byte>>(_map.Count);
        var valset = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in _map.Keys)
        {
            var hash = GetHash(key, take);
            if (modder is not null)
            {
                hash = modder(hash);
            }

            if (!valset.Add(Convert.ToBase64String(hash.ToArray())))
            {
                throw new PseudoGuidException($"The hash values for {typeof(TKind).Name} don't form a unique set (prefix collision at '{key}').");
            }

            map.Add(key, hash);
        }

        return new ReadOnlyDictionary<TKind, ReadOnlyMemory<byte>>(map);
    }

    /// <summary>
    /// Get a map of hash-based guid prefix to enum value, for every enum value.
    /// </summary>
    /// <param name="take">
    /// Length of hashes desired.
    /// </param>
    /// <param name="modder">
    /// Optional function to apply to each hash (such as to apply an RFC4122 based version mod).
    /// </param>
    /// <returns>
    /// Map of hash-based guid prefix to enum value, for every enum value.
    /// The hash-based guid will be zero-padded on the trailing end, if <paramref name="take"/> of less than 16 is specified.
    /// </returns>
    /// <exception cref="PseudoGuidException">The resulting prefixes are not unique.</exception>
    public ReadOnlyDictionary<Guid, TKind> GetDecoderMap(int? take, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? modder)
    {
        var map = new Dictionary<Guid, TKind>(_map.Count);
        var buf = new byte[TotalSize];
        foreach (var key in _map.Keys)
        {
            var hash = GetHash(key, take);
            if (modder is not null)
            {
                hash = modder(hash);
            }

            Array.Clear(buf, 0, buf.Length);
            hash.Span.Slice(0, Math.Min(hash.Length, TotalSize)).CopyTo(buf);
            var prefix = new Guid(buf);
            if (map.ContainsKey(prefix))
            {
                throw new PseudoGuidException($"The hash values for {typeof(TKind).Name} don't form a unique set (prefix collision at '{key}').");
            }

            map.Add(prefix, key);
        }

        return new ReadOnlyDictionary<Guid, TKind>(map);
    }
}
