using System;

namespace PseudoGuid.Rfc
{
    /// <summary>
    /// tools to apply modifications to the "version" of an identifier
    /// </summary>
    public static class VersionModder
    {
        private const byte _clearRfcVersion = 0b00001111;

        /// <summary>
        /// apply an RFC4122 version to an identifier
        /// </summary>
        /// <param name="buf">
        /// a 16 byte identifier, or prefix thereof that's at least 8 bytes long
        /// </param>
        /// <param name="rfcversion">
        /// version to apply
        /// 1 through 5 are actually compliant versions
        /// 6 to 16 indicate intentional deviation from RFC4122
        /// </param>
        public static void ApplyRfcVersion(Span<byte> buf, int rfcversion)
        {
            if (rfcversion <= 0 || rfcversion > 0xF)
            {
                throw new ArgumentOutOfRangeException(nameof(rfcversion));
            }

            buf[7] &= _clearRfcVersion;
            buf[7] |= (byte)(rfcversion << 4);
        }
    }
}
