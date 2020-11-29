using System.Threading;

namespace Contextual.Contexts
{
    /// <summary>
    /// Provides ambient cancellation.
    /// </summary>
    public class CancellationContext : Context
    {
        /// <summary>
        /// Cancellation token.
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>
        /// Initializes an instance of <see cref="CancellationContext"/>.
        /// </summary>
        public CancellationContext(CancellationToken token) => Token = token;

        /// <summary>
        /// Initializes an instance of <see cref="CancellationContext"/>.
        /// </summary>
        public CancellationContext() : this(default) {}
    }
}