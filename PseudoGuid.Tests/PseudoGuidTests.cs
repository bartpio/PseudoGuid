using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PseudoGuid.Rfc;

namespace PseudoGuid.Tests;

public class PseudoGuidTests
{
    private HashAlgorithm _hashAlgorithm = null!;

    [SetUp]
    public void Setup() => _hashAlgorithm = MD5.Create();

    [TearDown]
    public void Tear() => _hashAlgorithm.Dispose();

    [Test]
    public void TestPseudoGuids()
    {
        var khashes = new KindHashes<Color>(_hashAlgorithm);
        var pgs = new PseudoGuids<Color, int>(khashes);
        var one = pgs.Encode(Color.Red, 1);
        var two = pgs.Encode(Color.Red, 2);

        AssertRfcVersion(one, 0xA);
        AssertRfcVersion(two, 0xA);

        var expectedpfx = _hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes("Red")).AsSpan(0, 12); // 16 - 4 = 12
        VersionModder.ApplyRfcVersion(expectedpfx, 0xA);
        Assert.That(one.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(1)).ToArray()));
        Assert.That(two.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(2)).ToArray()));
        Assert.That(one, Is.Not.EqualTo(two));

        Assert.That(pgs.TryDecode(one, out var kind1, out var seq1), Is.True);
        Assert.That(kind1, Is.EqualTo(Color.Red));
        Assert.That(seq1, Is.EqualTo(1));
        Assert.That(pgs.TryDecode(two, out var kind2, out var seq2), Is.True);
        Assert.That(kind2, Is.EqualTo(Color.Red));
        Assert.That(seq2, Is.EqualTo(2));

        Assert.That(pgs.TryDecode(Guid.NewGuid(), out _, out _), Is.False);
    }

    [Test]
    public void TestPseudoGuidsLong()
    {
        var khashes = new KindHashes<Color>(_hashAlgorithm);
        var pgs = new PseudoGuids<Color, long>(khashes);
        var one = pgs.Encode(Color.Red, 1L * int.MaxValue);
        var two = pgs.Encode(Color.Red, 2L * int.MaxValue);

        AssertRfcVersion(one, 0xB);
        AssertRfcVersion(two, 0xB);

        var expectedpfx = _hashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes("Red")).AsSpan(0, 8); // 16 - 8 = 8
        VersionModder.ApplyRfcVersion(expectedpfx, 0xB);
        Assert.That(one.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(1L * int.MaxValue)).ToArray()));
        Assert.That(two.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(2L * int.MaxValue)).ToArray()));
        Assert.That(one, Is.Not.EqualTo(two));

        Assert.That(pgs.TryDecode(one, out var kind1, out var seq1), Is.True);
        Assert.That(kind1, Is.EqualTo(Color.Red));
        Assert.That(seq1, Is.EqualTo(1L * int.MaxValue));
        Assert.That(pgs.TryDecode(two, out var kind2, out var seq2), Is.True);
        Assert.That(kind2, Is.EqualTo(Color.Red));
        Assert.That(seq2, Is.EqualTo(2L * int.MaxValue));

        Assert.That(pgs.TryDecode(Guid.NewGuid(), out _, out _), Is.False);
    }

    /// <summary>
    /// Locks the wire format: identifiers already persisted by earlier versions must keep decoding.
    /// These values were computed directly from MD5, independently of the library.
    /// </summary>
    [TestCase(Color.Red, 1, "d5e438ee-68dd-a4c4-4082-501801000000")]
    [TestCase(Color.Blue, 123456, "c9ee9495-e75b-ab0e-1710-f73040e20100")]
    public void GoldenValuesInt(Color kind, int seq, string expected)
    {
        var pgs = new PseudoGuids<Color, int>();
        Assert.That(pgs.Encode(kind, seq), Is.EqualTo(Guid.Parse(expected)));
        Assert.That(pgs.Decode(Guid.Parse(expected)), Is.EqualTo((kind, seq)));
    }

    [TestCase(Color.Red, 4294967294L, "d5e438ee-68dd-b4c4-feff-ffff00000000")]
    [TestCase(Color.White, -1L, "0117a825-fafb-be4a-ffff-ffffffffffff")]
    public void GoldenValuesLong(Color kind, long seq, string expected)
    {
        var pgs = new PseudoGuids<Color, long>();
        Assert.That(pgs.Encode(kind, seq), Is.EqualTo(Guid.Parse(expected)));
        Assert.That(pgs.Decode(Guid.Parse(expected)), Is.EqualTo((kind, seq)));
    }

    [Test]
    public void DefaultConstructorMatchesExplicitMd5()
    {
        var explicitPgs = new PseudoGuids<Color, int>(new KindHashes<Color>(_hashAlgorithm));
        var defaultPgs = new PseudoGuids<Color, int>();
        Assert.That(defaultPgs.Encode(Color.Blue, 42), Is.EqualTo(explicitPgs.Encode(Color.Blue, 42)));
    }

    [TestCase(int.MinValue)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(int.MaxValue)]
    public void RoundTripsEdgeValuesInt(int seq)
    {
        var pgs = new PseudoGuids<Color, int>();
        foreach (var kind in new[] { Color.Red, Color.White, Color.Blue })
        {
            Assert.That(pgs.Decode(pgs.Encode(kind, seq)), Is.EqualTo((kind, seq)));
        }
    }

    [TestCase(long.MinValue)]
    [TestCase(0L)]
    [TestCase(long.MaxValue)]
    public void RoundTripsEdgeValuesLong(long seq)
    {
        var pgs = new PseudoGuids<Color, long>();
        foreach (var kind in new[] { Color.Red, Color.White, Color.Blue })
        {
            Assert.That(pgs.Decode(pgs.Encode(kind, seq)), Is.EqualTo((kind, seq)));
        }
    }

    [Test]
    public void UnsignedSequencesRoundTrip()
    {
        var pgu = new PseudoGuids<Color, uint>();
        Assert.That(pgu.Decode(pgu.Encode(Color.Red, uint.MaxValue)), Is.EqualTo((Color.Red, uint.MaxValue)));
        Assert.That(pgu.RfcVersion, Is.EqualTo(0xA));

        var pgul = new PseudoGuids<Color, ulong>();
        Assert.That(pgul.Decode(pgul.Encode(Color.Red, ulong.MaxValue)), Is.EqualTo((Color.Red, ulong.MaxValue)));
        Assert.That(pgul.RfcVersion, Is.EqualTo(0xB));
    }

    [Test]
    public void UnsupportedSequenceSizeThrows()
    {
        Assert.Throws<InvalidOperationException>(() => _ = new PseudoGuids<Color, short>());
        Assert.Throws<InvalidOperationException>(() => _ = new PseudoGuids<Color, Guid>());
    }

    [Test]
    public void IntAndLongSequencesDoNotCrossDecode()
    {
        var pgi = new PseudoGuids<Color, int>();
        var pgl = new PseudoGuids<Color, long>();
        Assert.That(pgl.IsPseudoGuid(pgi.Encode(Color.Red, 1)), Is.False);
        Assert.That(pgi.IsPseudoGuid(pgl.Encode(Color.Red, 1)), Is.False);
    }

    [Test]
    public void TryDecodeWithExpectedKind()
    {
        var pgs = new PseudoGuids<Color, int>();
        var red = pgs.Encode(Color.Red, 7);

        Assert.That(pgs.TryDecode(red, Color.Red, out var seq), Is.True);
        Assert.That(seq, Is.EqualTo(7));
        Assert.That(pgs.TryDecode(red, Color.Blue, out seq), Is.False);
        Assert.That(seq, Is.Zero);

        Assert.That(pgs.Decode(red, Color.Red), Is.EqualTo(7));
        Assert.Throws<ArgumentException>(() => pgs.Decode(red, Color.Blue));
    }

    [Test]
    public void DecodeThrowsForForeignGuid()
    {
        var pgs = new PseudoGuids<Color, int>();
        Assert.Throws<ArgumentException>(() => pgs.Decode(Guid.NewGuid()));
        Assert.That(pgs.IsPseudoGuid(Guid.NewGuid()), Is.False);
        Assert.That(pgs.IsPseudoGuid(Guid.Empty), Is.False);
    }

    [Test]
    public void EncodeUndefinedKindThrowsArgumentOutOfRange()
    {
        var pgs = new PseudoGuids<Color, int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => pgs.Encode((Color)99, 1));
    }

    [Test]
    public void AliasedKindValuesAreRejected()
    {
        var ex = Assert.Throws<PseudoGuidException>(() => _ = new KindHashes<AliasedKind>());
        Assert.That(ex!.Message, Does.Contain("First").And.Contain("Primary"));
    }

    [Test]
    public void TestIncompatibleEnums()
    {
        var khashes = new KindHashes<Color>(_hashAlgorithm);
        var pgs = new PseudoGuids<Color, int>(khashes);
        var one = pgs.Encode(Color.Red, 1);

        var khashesanother = new KindHashes<AnotherEnum>(_hashAlgorithm);
        var pgsanother = new PseudoGuids<AnotherEnum, int>(khashesanother);
        Assert.That(pgsanother.TryDecode(one, out _, out _), Is.False);

        var another = pgsanother.Encode(AnotherEnum.Why, int.MaxValue);
        Assert.That(pgsanother.TryDecode(another, out var anotherkind, out var anotherseq), Is.True);
        Assert.That(anotherkind, Is.EqualTo(AnotherEnum.Why));
        Assert.That(anotherseq, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void IsThreadSafe()
    {
        var pgs = new PseudoGuids<Color, long>();
        Parallel.For(0, 10_000, i =>
        {
            var kind = (Color)(i % 3);
            var (k, s) = pgs.Decode(pgs.Encode(kind, i));
            if (k != kind || s != i)
            {
                throw new AssertionException($"round trip failed for {i}");
            }
        });
    }

    [Test]
    public void VersionModderValidatesInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VersionModder.ApplyRfcVersion(new byte[16], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => VersionModder.ApplyRfcVersion(new byte[16], 16));
        Assert.Throws<ArgumentException>(() => VersionModder.ApplyRfcVersion(new byte[7], 1));
    }

    [Test]
    public void VerifyFrameworkGuidHasDifferentVersion()
    {
        AssertRfcVersion(Guid.NewGuid(), 0x4);
    }

    private static void AssertRfcVersion(Guid guid, int expected)
    {
        Assert.That(VersionModder.GetRfcVersion(guid), Is.EqualTo(expected), $"RFC version was meant to be {expected:x}");
        Assert.That(guid.ToByteArray()[7] >> 4, Is.EqualTo(expected));
    }
}
