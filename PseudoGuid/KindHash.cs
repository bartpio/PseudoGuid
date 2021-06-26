using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace PseudoGuid
{
    internal class KindHash<TKind> where TKind : struct, Enum
    {
        public KindHash(HashAlgorithm hashAlgorithm, TKind entityKind)
        {
            if (hashAlgorithm is null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithm));
            }

            Kind = entityKind;
            KindString = Kind.ToStringAsserted();
            Hash = hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes(KindString));
        }

        public TKind Kind { get; }

        public string KindString { get; }

        private byte[] Hash { get; }

        public ReadOnlyMemory<byte> GetHash(int? take)
        {
            if (take is not null)
            {
                ReadOnlyMemory<byte> result = Hash;
                return result.Slice(0, take.Value);
            }
            else
            {
                return Hash;
            }
        }
    }
}
