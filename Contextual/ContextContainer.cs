using System.Threading;

namespace Contextual
{
    internal static class ContextContainer<T> where T : Context
    {
        public static AsyncLocal<T?> Current { get; } = new();
    }
}