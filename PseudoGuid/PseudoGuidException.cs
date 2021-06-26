using System;

namespace PseudoGuid
{
    /// <summary>
    /// represents a problem particular to pseudoguids
    /// </summary>
    public class PseudoGuidException : Exception
    {
        /// <summary>
        /// something went wrong, so let's construct an exception
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
}
