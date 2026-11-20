# Pseudo Guid

## Summary
Generate identifiers, conforming to the Guid CLR type, that are based on a prefix and sequence.
The identifiers are not compliant with RFC4122 (and are marked with a version outside of what is defined in RFC4122 / RFC9562,
as to guarantee never colliding with an actually compliant UUID, such as one generated using `Guid.NewGuid`).

Targets `netstandard2.0` and `net10.0` (the latter is allocation-free and trimming/AOT compatible).

## Prefixes
Identifier prefixes are based on an Enum type. Each enum value's *name* is hashed (MD5 by default) to generate a prefix.
The prefix is 12 bytes long when the sequence is 4 bytes (`int`, `uint`), and 8 bytes long when the sequence is 8 bytes (`long`, `ulong`).
In the event that the Enum type being used results in a potential prefix collision, a `PseudoGuidException` is thrown.

Because prefixes come from names, renaming an enum member changes its identifiers. Enums with aliased members
(two names for the same value) are rejected.

## Usage
```csharp
// Construct once and reuse; instances are immutable and thread-safe.
var pgs = new PseudoGuids<Color, int>();

Guid id = pgs.Encode(Color.Red, 1);

// Decode, discovering the kind
if (pgs.TryDecode(id, out var kind, out var seq)) { /* kind == Color.Red, seq == 1 */ }
var (k, s) = pgs.Decode(id); // throws ArgumentException if not a pseudo-guid

// Decode, requiring a specific kind (e.g. an API route that only accepts Red ids)
if (pgs.TryDecode(id, Color.Red, out var redSeq)) { /* ... */ }
int redSeq2 = pgs.Decode(id, Color.Red);

pgs.IsPseudoGuid(Guid.NewGuid()); // false
```

A custom hash algorithm can be supplied via `new PseudoGuids<Color, int>(new KindHashes<Color>(hashAlgorithm))`.
The algorithm is only used during construction and is not disposed.

## Compatibility
The wire format is stable: identifiers produced by 1.0.x decode identically with later versions
(covered by golden-value tests). Sequences are stored little-endian.
