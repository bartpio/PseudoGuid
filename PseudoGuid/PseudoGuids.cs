using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Collections.ObjectModel;
using PseudoGuid.Rfc;

namespace PseudoGuid
{
    /// <summary>
    /// provider of pseudo-guids
    /// </summary>
    /// <typeparam name="TKind">
    /// enumeration type to base the pseudo-guid prefixes on
    /// </typeparam>
    /// <typeparam name="TSeq">
    /// type of sequence value that will be used for the trailing end of the pseudo-guid
    /// specify <see cref="int"/> or <see cref="long"/>
    /// </typeparam>
    public class PseudoGuids<TKind, TSeq> where TKind : struct, Enum where TSeq : unmanaged
    {
        private readonly KindHashes<TKind> _khashes;
        private readonly int _seqsiz;
        private readonly int _rfcversion;
        private readonly int _pfxsiz;
        private readonly ReadOnlyDictionary<TKind, ReadOnlyMemory<byte>> _encodermap;
        private readonly ReadOnlyDictionary<Guid, TKind> _decodermap;

        private const int _totalsize = KindHashes<TKind>.TotalSize; // aka sizeof(Guid)

        /// <summary>
        /// construct a provider of pseudo-guids
        /// </summary>
        /// <param name="khashes">
        /// mappings of all 
        /// </param>
        public PseudoGuids(KindHashes<TKind> khashes)
        {
            _khashes = khashes ?? throw new ArgumentNullException(nameof(khashes));
            unsafe
            {
                _seqsiz = sizeof(TSeq);
            }

            _rfcversion = GetRfcVersion(_seqsiz);
            _pfxsiz = _totalsize - _seqsiz;

            _encodermap = _khashes.GetEncoderMap(_pfxsiz, ApplyRfcVersion);
            _decodermap = _khashes.GetDecoderMap(_pfxsiz, ApplyRfcVersion);
        }

        /// <summary>
        /// get an rfc version for a given sequence size,
        /// that is not actually a version listed in the rfc
        /// the rfc defines versions 1 through 5
        /// here, we'll return nonstandard version 10 or 11 (for sequence type int and long, respectively)
        /// </summary>
        /// <param name="seqsiz">
        /// sequence size (4 for int, 8 for long)
        /// </param>
        /// <returns>
        /// nonstandard version 10 or 11 (for sequence type int and long, respectively)
        /// </returns>
        public static int GetRfcVersion(int seqsiz)
        {
            // the RFC ( https://datatracker.ietf.org/doc/html/rfc4122#section-4.1.3 ) defines versions 1 to 5
            // since we're using nonstandard uids, let's intentionally use a version OUT of this range!

            if (seqsiz == sizeof(int))
            {
                return 0xA;
            }
            else if (seqsiz == sizeof(long))
            {
                return 0xB;
            }
            else
            {
                throw new InvalidOperationException($"{nameof(TSeq)} must be sized 4 bytes or 8 bytes (not {seqsiz})");
            }
        }

        private ReadOnlyMemory<byte> ApplyRfcVersion(ReadOnlyMemory<byte> buf)
        {
            var newbuf = new byte[_totalsize];
            var newspan = newbuf.AsSpan();
            buf.Span.CopyTo(newspan);
            VersionModder.ApplyRfcVersion(newspan, _rfcversion);
            return newbuf;
        }

        private Span<byte> GetSuffix(TSeq seq)
        {
            dynamic dyn = seq;
            byte[] result = BitConverter.GetBytes(dyn);
            if (result is null || result.Length != _seqsiz)
            {
                throw new PseudoGuidException($"a {nameof(TSeq)} didn't convert to the expected size {_seqsiz}");
            }
            return result;
        }

        private ReadOnlyMemory<byte> GetHashPrefix(TKind kind) => _khashes.GetHash(kind, _pfxsiz);

        /// <summary>
        /// encode a particular enumeration value and sequence to a pseudo-guid
        /// </summary>
        /// <param name="kind">
        /// a particular enumeration value
        /// </param>
        /// <param name="seq">
        /// sequence value
        /// </param>
        /// <returns>
        /// pseudo-guid with prefix based on the enumeration value, and suffix based on the sequence
        /// </returns>
        public Guid Encode(TKind kind, TSeq seq)
        {
            var buf = new byte[_totalsize];
            var dest = new Span<byte>(buf);
            _encodermap[kind].Span.CopyTo(dest);
            var suffix = GetSuffix(seq);
            suffix.CopyTo(dest.Slice(_pfxsiz));
            return new Guid(buf);
        }

        private dynamic Decode(Span<byte> bytes)
        {
            if (_seqsiz == sizeof(int))
            {
                return BitConverter.ToInt32(bytes.ToArray(), 0);
            }
            else if (_seqsiz == sizeof(long))
            {
                return BitConverter.ToInt64(bytes.ToArray(), 0);
            }
            else
            {
                // this can't really happen
                throw new PseudoGuidException($"a decode {nameof(TSeq)} didn't convert to the expected size {_seqsiz}");
            }
        }

        /// <summary>
        /// try to decode a candidate pseudo-guid, recovering the original enumeration value and sequence value
        /// </summary>
        /// <param name="encoded">
        /// candidate pseudo-guid to decode (should have been encoded using <see cref="Encode(TKind, TSeq)"/>, using the same enumeration type <see cref="TKind"/>)
        /// </param>
        /// <param name="kind">
        /// outputs the particular enumeration value, whose hash is the prefix of the encoded pseudo-guid
        /// </param>
        /// <param name="seq">
        /// outputs the sequence value, that is present as the suffix of the encoded pseudo-guid
        /// </param>
        /// <returns>
        /// true if the provided <see cref="Guid"/> was in fact a pseudo-guid encoded for the <see cref="TKind"/> enumeration type
        /// otherwise, false
        /// </returns>
        public bool TryDecode(Guid encoded, out TKind kind, out TSeq seq)
        {
            var span = encoded.ToByteArray().AsSpan();
            var anotherbuf = new byte[_totalsize];
            span.Slice(0, _pfxsiz).CopyTo(anotherbuf);
            var pfxguid = new Guid(anotherbuf);

            if (_decodermap.TryGetValue(pfxguid, out kind))
            {
                seq = Decode(span.Slice(_pfxsiz));
                return true;
            }
            else
            {
                kind = default;
                seq = default;
                return false;
            }
        }
    }
}
