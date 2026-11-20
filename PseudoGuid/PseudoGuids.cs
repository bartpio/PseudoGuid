using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using PseudoGuid.Rfc;

namespace PseudoGuid;

/// <summary>
/// Provider of pseudo-guids. Instances are immutable and safe to share across threads;
/// construct once (e.g. as a singleton) and reuse.
/// </summary>
/// <typeparam name="TKind">
/// Enumeration type to base the pseudo-guid prefixes on.
/// </typeparam>
/// <typeparam name="TSeq">
/// Type of sequence value that will be used for the trailing end of the pseudo-guid.
/// Typically <see cref="int"/> or <see cref="long"/>, though any unmanaged type 4 or 8 bytes in size
/// (such as <see cref="uint"/> or <see cref="ulong"/>) is accepted. The sequence is stored little-endian.
/// </typeparam>
public class PseudoGuids<TKind, TSeq> where TKind : struct, Enum where TSeq : unmanaged
{
    private const int TotalSize = KindHashes<TKind>.TotalSize;

    private readonly int _seqsiz;
    private readonly int _pfxsiz;
    private readonly ReadOnlyDictionary<TKind, ReadOnlyMemory<byte>> _encodermap;
    private readonly ReadOnlyDictionary<Guid, TKind> _decodermap;

    /// <summary>
    /// Construct a provider of pseudo-guids, using MD5-based prefixes.
    /// </summary>
    /// <exception cref="PseudoGuidException">The enum values of <typeparamref name="TKind"/> produce colliding prefixes.</exception>
    public PseudoGuids()
        : this(new KindHashes<TKind>())
    {
    }

    /// <summary>
    /// Construct a provider of pseudo-guids.
    /// </summary>
    /// <param name="khashes">
    /// Prefix hashes for every value of <typeparamref name="TKind"/>.
    /// </param>
    /// <exception cref="InvalidOperationException"><typeparamref name="TSeq"/> is not 4 or 8 bytes in size.</exception>
    /// <exception cref="PseudoGuidException">The enum values of <typeparamref name="TKind"/> produce colliding prefixes.</exception>
    public PseudoGuids(KindHashes<TKind> khashes)
    {
        ArgumentNullExceptionPolyfill.ThrowIfNull(khashes);

        unsafe
        {
            _seqsiz = sizeof(TSeq);
        }

        RfcVersion = GetRfcVersion(_seqsiz);
        _pfxsiz = TotalSize - _seqsiz;

        _encodermap = khashes.GetEncoderMap(_pfxsiz, ApplyRfcVersion);
        _decodermap = khashes.GetDecoderMap(_pfxsiz, ApplyRfcVersion);
    }

    /// <summary>
    /// The nonstandard version nibble stamped on every pseudo-guid produced by this instance
    /// (0xA for 4 byte sequences, 0xB for 8 byte sequences).
    /// </summary>
    public int RfcVersion { get; }

    /// <summary>
    /// Get an rfc version for a given sequence size, that is not actually a version listed in the rfc.
    /// RFC4122 defines versions 1 through 5 (and RFC9562 adds 6 through 8);
    /// here, we'll return nonstandard version 10 or 11 (for 4 and 8 byte sequences, respectively).
    /// </summary>
    /// <param name="seqsiz">
    /// Sequence size (4 for int, 8 for long).
    /// </param>
    /// <returns>
    /// Nonstandard version 10 or 11 (for sequence type int and long, respectively).
    /// </returns>
    /// <exception cref="InvalidOperationException"><paramref name="seqsiz"/> is not 4 or 8.</exception>
#pragma warning disable CA1000 // Existing public API.
    public static int GetRfcVersion(int seqsiz) => seqsiz switch
    {
        // the RFC ( https://datatracker.ietf.org/doc/html/rfc4122#section-4.1.3 ) defines versions 1 to 5
        // since we're using nonstandard uids, let's intentionally use a version OUT of this range!
        sizeof(int) => 0xA,
        sizeof(long) => 0xB,
        _ => throw new InvalidOperationException($"{typeof(TSeq).Name} must be sized 4 bytes or 8 bytes (not {seqsiz})."),
    };
#pragma warning restore CA1000

    private ReadOnlyMemory<byte> ApplyRfcVersion(ReadOnlyMemory<byte> buf)
    {
        var newbuf = new byte[TotalSize];
        buf.Span.CopyTo(newbuf);
        VersionModder.ApplyRfcVersion(newbuf, RfcVersion);
        return newbuf;
    }

    /// <summary>
    /// Encode a particular enumeration value and sequence to a pseudo-guid.
    /// </summary>
    /// <param name="kind">
    /// A particular enumeration value.
    /// </param>
    /// <param name="seq">
    /// Sequence value.
    /// </param>
    /// <returns>
    /// Pseudo-guid with prefix based on the enumeration value, and suffix based on the sequence.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined value of <typeparamref name="TKind"/>.</exception>
    public Guid Encode(TKind kind, TSeq seq)
    {
        if (!_encodermap.TryGetValue(kind, out var prefix))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, $"'{kind}' is not a defined value of {typeof(TKind).Name}.");
        }

#if NET
        Span<byte> buf = stackalloc byte[TotalSize];
#else
        var arr = new byte[TotalSize];
        Span<byte> buf = arr;
#endif
        prefix.Span.Slice(0, _pfxsiz).CopyTo(buf);
        WriteSequence(buf.Slice(_pfxsiz), seq);

#if NET
        return new Guid(buf);
#else
        return new Guid(arr);
#endif
    }

    /// <summary>
    /// Try to decode a candidate pseudo-guid, recovering the original enumeration value and sequence value.
    /// </summary>
    /// <param name="encoded">
    /// Candidate pseudo-guid to decode (should have been encoded using <see cref="Encode(TKind, TSeq)"/>,
    /// using the same enumeration type <typeparamref name="TKind"/> and sequence type <typeparamref name="TSeq"/>).
    /// </param>
    /// <param name="kind">
    /// Outputs the particular enumeration value, whose hash is the prefix of the encoded pseudo-guid.
    /// </param>
    /// <param name="seq">
    /// Outputs the sequence value, that is present as the suffix of the encoded pseudo-guid.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the provided <see cref="Guid"/> was in fact a pseudo-guid encoded for the
    /// <typeparamref name="TKind"/> enumeration type; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryDecode(Guid encoded, out TKind kind, out TSeq seq)
    {
#if NET
        Span<byte> buf = stackalloc byte[TotalSize];
        encoded.TryWriteBytes(buf);
#else
        var arr = encoded.ToByteArray();
        Span<byte> buf = arr;
#endif
        var suffix = buf.Slice(_pfxsiz);
        var candidate = ReadSequence(suffix);
        suffix.Clear();

#if NET
        var pfxguid = new Guid(buf);
#else
        var pfxguid = new Guid(arr);
#endif

        if (_decodermap.TryGetValue(pfxguid, out kind))
        {
            seq = candidate;
            return true;
        }

        kind = default;
        seq = default;
        return false;
    }

    /// <summary>
    /// Try to decode a candidate pseudo-guid that is expected to be of a particular kind.
    /// </summary>
    /// <param name="encoded">Candidate pseudo-guid to decode.</param>
    /// <param name="expectedKind">The enumeration value the pseudo-guid must have been encoded with.</param>
    /// <param name="seq">Outputs the sequence value, if decoding succeeded.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="encoded"/> is a pseudo-guid of <paramref name="expectedKind"/>;
    /// otherwise (including when it is a pseudo-guid of a different kind), <see langword="false"/>.
    /// </returns>
    public bool TryDecode(Guid encoded, TKind expectedKind, out TSeq seq)
    {
        if (TryDecode(encoded, out var kind, out seq) && kind.Equals(expectedKind))
        {
            return true;
        }

        seq = default;
        return false;
    }

    /// <summary>
    /// Decode a pseudo-guid, recovering the original enumeration value and sequence value.
    /// </summary>
    /// <param name="encoded">Pseudo-guid to decode.</param>
    /// <returns>The enumeration value and sequence value the pseudo-guid was encoded from.</returns>
    /// <exception cref="ArgumentException"><paramref name="encoded"/> is not a pseudo-guid for <typeparamref name="TKind"/>.</exception>
    public (TKind Kind, TSeq Sequence) Decode(Guid encoded)
    {
        if (TryDecode(encoded, out var kind, out var seq))
        {
            return (kind, seq);
        }

        throw new ArgumentException($"'{encoded}' is not a pseudo-guid for {typeof(TKind).Name} with a {typeof(TSeq).Name} sequence.", nameof(encoded));
    }

    /// <summary>
    /// Decode a pseudo-guid that is expected to be of a particular kind, recovering the sequence value.
    /// </summary>
    /// <param name="encoded">Pseudo-guid to decode.</param>
    /// <param name="expectedKind">The enumeration value the pseudo-guid must have been encoded with.</param>
    /// <returns>The sequence value the pseudo-guid was encoded from.</returns>
    /// <exception cref="ArgumentException"><paramref name="encoded"/> is not a pseudo-guid of <paramref name="expectedKind"/>.</exception>
    public TSeq Decode(Guid encoded, TKind expectedKind)
    {
        if (TryDecode(encoded, expectedKind, out var seq))
        {
            return seq;
        }

        throw new ArgumentException($"'{encoded}' is not a pseudo-guid for {typeof(TKind).Name}.{expectedKind} with a {typeof(TSeq).Name} sequence.", nameof(encoded));
    }

    /// <summary>
    /// Determine whether a <see cref="Guid"/> is a pseudo-guid produced for <typeparamref name="TKind"/> and <typeparamref name="TSeq"/>.
    /// </summary>
    /// <param name="candidate">The <see cref="Guid"/> to test.</param>
    /// <returns><see langword="true"/> if <paramref name="candidate"/> can be decoded by this instance.</returns>
    public bool IsPseudoGuid(Guid candidate) => TryDecode(candidate, out _, out _);

    private static void WriteSequence(Span<byte> dest, TSeq seq)
    {
#if NET
        MemoryMarshal.Write(dest, in seq);
#else
        MemoryMarshal.Write(dest, ref seq);
#endif
        if (!BitConverter.IsLittleEndian)
        {
            dest.Reverse();
        }
    }

    private static TSeq ReadSequence(ReadOnlySpan<byte> src)
    {
        if (!BitConverter.IsLittleEndian)
        {
            Span<byte> tmp = stackalloc byte[src.Length];
            src.CopyTo(tmp);
            tmp.Reverse();
            return MemoryMarshal.Read<TSeq>(tmp);
        }

        return MemoryMarshal.Read<TSeq>(src);
    }
}
