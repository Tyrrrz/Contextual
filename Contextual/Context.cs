namespace Contextual;

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
    /// Disposing the scope pops the context from the stack, rendering it unavailable.
    /// </summary>
    /// <remarks>
    /// Remember to wrap the return of this method in a <code>using</code> statement!
    /// </remarks>
    public static ContextScope<T> Provide<T>(T context) where T : Context
    {
        var previousContext = ContextContainer<T>.Current.Value;
        ContextContainer<T>.Current.Value = context;

        return new ContextScope<T>(context, previousContext);
    }

    /// <summary>
    /// Retrieves a context of the specified type from the nearest parent scope on the stack.
    /// If a context of the specified type has not been provided, a fallback is returned by calling the
    /// parameterless constructor on the class.
    /// </summary>
    public static T Use<T>() where T : Context, new() =>
        ContextContainer<T>.Current.Value ?? new T();
}