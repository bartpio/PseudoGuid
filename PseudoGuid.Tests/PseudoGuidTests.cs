using NUnit.Framework;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using PseudoGuid.Rfc;

namespace PseudoGuid.Tests
{
    public class PseudoGuidTests
    {
        private HashAlgorithm HashAlgorithm { get; set; }

        [SetUp]
        public void Setup()
        {
            HashAlgorithm = MD5.Create();
        }

        [TearDown]
        public void Tear()
        {
            HashAlgorithm.Dispose();
        }

        [Test]
        public void TestPseudoGuids()
        {
            var khashes = new KindHashes<Color>(HashAlgorithm);
            var pgs = new PseudoGuids<Color, int>(khashes);
            var one = pgs.Encode(Color.Red, 1);
            var two = pgs.Encode(Color.Red, 2);

            AssertRfcVersion(one, 0xA);
            AssertRfcVersion(two, 0xA);

            TestContext.WriteLine(one.ToString());
            TestContext.WriteLine(two.ToString());

            var expectedpfx = HashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes("Red")).AsSpan().Slice(0, 12);  // 16 - 4 = 12
            VersionModder.ApplyRfcVersion(expectedpfx, 0xA);
            Assert.That(one.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(1)).ToArray()));
            Assert.That(two.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(2)).ToArray()));
            Assert.That(one.ToByteArray(), Is.Not.EqualTo(two.ToByteArray()));

            Assert.That(pgs.TryDecode(one, out var kind1, out var seq1), Is.True);
            Assert.That(kind1, Is.EqualTo(Color.Red));
            Assert.That(seq1, Is.EqualTo(1));
            Assert.That(pgs.TryDecode(two, out var kind2, out var seq2), Is.True);
            Assert.That(kind2, Is.EqualTo(Color.Red));
            Assert.That(seq2, Is.EqualTo(2));

            Assert.That(pgs.TryDecode(Guid.NewGuid(), out var kindX, out var seqX), Is.False);
        }

        [Test]
        public void TestPseudoGuidsLong()
        {
            var khashes = new KindHashes<Color>(HashAlgorithm);
            var pgs = new PseudoGuids<Color, long>(khashes);
            var one = pgs.Encode(Color.Red, 1L * int.MaxValue);
            var two = pgs.Encode(Color.Red, 2L * int.MaxValue);

            AssertRfcVersion(one, 0xB);
            AssertRfcVersion(two, 0xB);

            TestContext.WriteLine(one.ToString());
            TestContext.WriteLine(two.ToString());

            var expectedpfx = HashAlgorithm.ComputeHash(Encoding.ASCII.GetBytes("Red")).AsSpan().Slice(0, 8);  // 16 - 8 = 8
            VersionModder.ApplyRfcVersion(expectedpfx, 0xB);
            Assert.That(one.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(1L * int.MaxValue)).ToArray()));
            Assert.That(two.ToByteArray(), Is.EqualTo(expectedpfx.ToArray().Concat(BitConverter.GetBytes(2L * int.MaxValue)).ToArray()));
            Assert.That(one.ToByteArray(), Is.Not.EqualTo(two.ToByteArray()));

            Assert.That(pgs.TryDecode(one, out var kind1, out var seq1), Is.True);
            Assert.That(kind1, Is.EqualTo(Color.Red));
            Assert.That(seq1, Is.EqualTo(1L * int.MaxValue));
            Assert.That(pgs.TryDecode(two, out var kind2, out var seq2), Is.True);
            Assert.That(kind2, Is.EqualTo(Color.Red));
            Assert.That(seq2, Is.EqualTo(2L * int.MaxValue));

            Assert.That(pgs.TryDecode(Guid.NewGuid(), out var kindX, out var seqX), Is.False);
        }

        [Test]
        public void TestIncompatibleEnums()
        {
            var khashes = new KindHashes<Color>(HashAlgorithm);
            var pgs = new PseudoGuids<Color, int>(khashes);
            var one = pgs.Encode(Color.Red, 1);

            var khashesanother = new KindHashes<AnotherEnum>(HashAlgorithm);
            var pgsanother = new PseudoGuids<AnotherEnum, int>(khashesanother);
            Assert.That(pgsanother.TryDecode(one, out var kindX, out var seqX), Is.False);

            var another = pgsanother.Encode(AnotherEnum.Why, int.MaxValue);
            Assert.That(pgsanother.TryDecode(another, out var anotherkind, out var anotherseq), Is.True);
            Assert.That(anotherkind, Is.EqualTo(AnotherEnum.Why));
            Assert.That(anotherseq, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void VerifyFrameworkGuidHasDifferentVersion()
        {
            var frameworkguid = Guid.NewGuid();
            AssertRfcVersion(frameworkguid, 0x4);
        }

        public void AssertRfcVersion(Guid guid, int expected)
        {
            var buf = guid.ToByteArray();
            var actual = buf[7] >> 4;
            Assert.That(actual, Is.EqualTo(expected), $"RFC version was meant to be {expected:x2}");
        }
    }
}