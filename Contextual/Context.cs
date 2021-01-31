using System;
using System.Threading;
using Contextual.Internal;

namespace Contextual
{
    /// <summary>
    /// Represents an abstract context.
    /// </summary>
    public abstract partial class Context
    {
    }

    public partial class Context
    {
        /// <summary>
        /// Provides context to nested operations by creating a scope.
        ///
        /// Disposing the scope pops the context from the stack, rendering it unavailable.
        /// If the context implements <see cref="IDisposable"/>, its own <see cref="IDisposable.Dispose"/> method is invoked as well.
        /// </summary>
        /// <remarks>
        /// Remember to wrap the return of this method in a <code>using</code> statement!
        /// </remarks>
        public static IDisposable Provide<T>(T context) where T : Context
        {
            var previous = Container<T>.Current.Value;
            Container<T>.Current.Value = context;

            return Disposable.Create(() =>
            {
                Container<T>.Current.Value = previous;

                // ReSharper disable once SuspiciousTypeConversion.Global
                // Dispose the context if it can be disposed.
                (context as IDisposable)?.Dispose();
            });
        }

        /// <summary>
        /// Retrieves a context of the specified type from the nearest parent scope on the stack.
        /// If a context of the specified type has not been provided, a fallback is returned by calling the
        /// parameterless constructor on the class.
        /// </summary>
        public static T Use<T>() where T : Context, new() =>
            Container<T>.Current.Value ?? new T();
    }

    public partial class Context
    {
        private static class Container<T> where T : Context
        {
            public static AsyncLocal<T?> Current { get; } = new();
        }
    }
}
