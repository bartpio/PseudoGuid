using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PseudoGuid
{
    /// <summary>
    /// deal with all of the prefix hashes for a particular enum type
    /// </summary>
    /// <typeparam name="TKind">
    /// an enum type
    /// </typeparam>
    public sealed class KindHashes<TKind> where TKind : struct, Enum
    {
        private readonly HashAlgorithm _alg;
        private readonly ReadOnlyDictionary<TKind, KindHash<TKind>> _map;

        /// <summary>
        /// construct
        /// </summary>
        /// <param name="hashAlgorithm">
        /// hash algorithm to use (generally MD5)
        /// </param>
        public KindHashes(HashAlgorithm hashAlgorithm)
        {
            _alg = hashAlgorithm ?? throw new ArgumentNullException(nameof(hashAlgorithm));

            var map = Enum.GetValues(typeof(TKind))
                .Cast<TKind>()
                .Select(x => new KindHash<TKind>(_alg, x))
                .ToDictionary(x => x.Kind, x => x);

            _map = new ReadOnlyDictionary<TKind, KindHash<TKind>>(map);
        }

        /// <summary>
        /// get hash for a particular enum value
        /// </summary>
        /// <param name="kind">
        /// a particular enum value
        /// </param>
        /// <param name="take">
        /// length of hash desired
        /// </param>
        /// <returns>
        /// hash of specified enum value, trimmed to specified length
        /// </returns>
        public ReadOnlyMemory<byte> GetHash(TKind kind, int? take)
        {
            if (_map.TryGetValue(kind, out var ekh))
            {
                return ekh.GetHash(take);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>
        /// get a map of enum value to hash, for every enum value
        /// </summary>
        /// <param name="take">
        /// length of hashes desired
        /// </param>
        /// <param name="modder">
        /// optional function to apply to each hash (such as to apply an RFC4122 based version mod)
        /// </param>
        /// <returns>
        /// map of enum value to hash, for every enum value
        /// </returns>
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
                    throw new PseudoGuidException("the hash values don't form a unique set");
                }

                map.Add(key, hash);
            }

            return new ReadOnlyDictionary<TKind, ReadOnlyMemory<byte>>(map);
        }

        public const int TotalSize = 16; // aka sizeof(Guid)

        /// <summary>
        /// get map of hashbased guid prefix to enum value, for every enum value
        /// </summary>
        /// <param name="take">
        /// length of hashes desired
        /// </param>
        /// <param name="modder">
        /// optional function to apply to each hash (such as to apply an RFC4122 based version mod)
        /// </param>
        /// <returns>
        /// map of hashbased guid prefix to enum value, for every enum value
        /// the hashbased guid will be zero-padded on the trailing end, if "take" of less than 16 is specified
        /// </returns>
        public ReadOnlyDictionary<Guid, TKind> GetDecoderMap(int? take, Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>? modder)
        {
            var map = new Dictionary<Guid, TKind>(_map.Count);
            var buf = new byte[TotalSize];
            var span = new Span<byte>(buf);
            foreach (var key in _map.Keys)
            {
                var hash = GetHash(key, take);
                if (modder is not null)
                {
                    hash = modder(hash);
                }
                hash.Span.CopyTo(span);
                map.Add(new Guid(buf), key);
            }

            return new ReadOnlyDictionary<Guid, TKind>(map);
        }
    }
}
