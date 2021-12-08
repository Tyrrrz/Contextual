using System;

namespace Contextual;

/// <summary>
/// Encapsulates a temporary provision of a typed context.
/// </summary>
public readonly struct ContextScope<T> : IDisposable where T : Context
{
    /// <summary>
    /// Provided context instance.
    /// </summary>
    public T Context { get; }

    /// <summary>
    /// Previous context instance.
    /// </summary>
    private T? PreviousContext { get; }

    /// <summary>
    /// Initializes an instance of <see cref="ContextScope{T}"/>.
    /// </summary>
    public ContextScope(T context, T? previousContext)
    {
        Context = context;
        PreviousContext = previousContext;
    }

    /// <summary>
    /// Restores the previous context instance.
    /// </summary>
    public void Dispose()
    {
        // Guard against re-entry
        // (weak attempt, but can't have state in a struct)
        if (ContextContainer<T>.Current.Value == Context)
        {
            ContextContainer<T>.Current.Value = PreviousContext;
        }
    }
}