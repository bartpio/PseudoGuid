using System;

namespace PseudoGuid.Rfc;

/// <summary>
/// Tools to apply modifications to the "version" of an identifier.
/// </summary>
public static class VersionModder
{
    private const byte ClearRfcVersion = 0b0000_1111;

    /// <summary>
    /// The index (within the <see cref="Guid.ToByteArray()"/> layout) of the byte whose high nibble holds the version.
    /// </summary>
    public const int VersionByteIndex = 7;

    /// <summary>
    /// Apply an RFC4122 version to an identifier.
    /// </summary>
    /// <param name="buf">
    /// A 16 byte identifier (in <see cref="Guid.ToByteArray()"/> layout), or a prefix thereof that's at least 8 bytes long.
    /// </param>
    /// <param name="rfcversion">
    /// Version to apply, 1 through 15.
    /// 1 through 8 are versions defined by RFC4122 / RFC9562;
    /// higher values indicate intentional deviation from those RFCs.
    /// </param>
    public static void ApplyRfcVersion(Span<byte> buf, int rfcversion)
    {
        if (rfcversion <= 0 || rfcversion > 0xF)
        {
            throw new ArgumentOutOfRangeException(nameof(rfcversion), rfcversion, "Version must be between 1 and 15.");
        }

        if (buf.Length <= VersionByteIndex)
        {
            throw new ArgumentException($"Buffer must be at least {VersionByteIndex + 1} bytes long.", nameof(buf));
        }

        buf[VersionByteIndex] &= ClearRfcVersion;
        buf[VersionByteIndex] |= (byte)(rfcversion << 4);
    }

    /// <summary>
    /// Read the version nibble of an identifier.
    /// </summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <returns>The version, 0 through 15.</returns>
    public static int GetRfcVersion(Guid identifier)
    {
#if NET
        Span<byte> buf = stackalloc byte[16];
        identifier.TryWriteBytes(buf);
        return buf[VersionByteIndex] >> 4;
#else
        return identifier.ToByteArray()[VersionByteIndex] >> 4;
#endif
    }
}
