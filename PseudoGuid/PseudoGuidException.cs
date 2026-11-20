using System;

namespace PseudoGuid;

/// <summary>
/// Represents a problem particular to pseudo-guids, such as an enum type whose values produce colliding prefixes.
/// </summary>
public class PseudoGuidException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PseudoGuidException"/> class.
    /// </summary>
    public PseudoGuidException()
    {
    }

    /// <inheritdoc />
    public PseudoGuidException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public PseudoGuidException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
