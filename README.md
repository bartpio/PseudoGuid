# Pseudo Guid
## Summary
Generate identifiers, conforming to the Guid CLR type, that are based on a prefix and sequence.
The identifiers are not compliant with RFC4122 (and are marked with a version outside of what is defined in RFC4122,
as to guarantee never colliding with an actually compliant UUID, such as one generated using Guid.NewGuid).

## Prefixes
Identifier prefixes are based on an Enum type. Each enum value is hashed, to generate a prefix.
The prefix is 12 bytes long when sequence is of type int, and 8 bytes long when sequence is of type long.
In the event that the Enum type being used results in a potential prefix collision, an exception is thrown.

## Usage
```csharp
var HashAlgorithm = MD5.Create();
var khashes = new KindHashes<Color>(HashAlgorithm);
var pgs = new PseudoGuids<Color, int>(khashes);

var one = pgs.Encode(Color.Red, 1);
var two = pgs.Encode(Color.Red, 2);

Assert.That(pgs.TryDecode(one, out var kind1, out var seq1), Is.True);
Assert.That(kind1, Is.EqualTo(Color.Red));
Assert.That(seq1, Is.EqualTo(1));

Assert.That(pgs.TryDecode(two, out var kind2, out var seq2), Is.True);
Assert.That(kind2, Is.EqualTo(Color.Red));
Assert.That(seq2, Is.EqualTo(2));
```
