using System;
using System.Collections.Generic;
using System.Text;

namespace PseudoGuid
{
    internal static class KindExtensions
    {
        public static string ToStringAsserted<TKind>(this TKind kind) where TKind : struct, Enum
        {
            if (Enum.IsDefined(typeof(TKind), kind))
            {
                return kind.ToString();
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
