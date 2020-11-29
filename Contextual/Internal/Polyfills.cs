// ReSharper disable CheckNamespace

// Polyfills to bridge the missing APIs in older versions of the framework/standard.

#if NETSTANDARD2_0
namespace System.Collections.Generic
{
    internal static class PolyfillExtensions
    {
        public static bool TryPop<T>(this Stack<T> stack, out T result)
        {
            if (stack.Count > 0)
            {
                result = stack.Pop();
                return true;
            }
            else
            {
                result = default!;
                return false;
            }
        }

        public static bool TryPeek<T>(this Stack<T> stack, out T result)
        {
            if (stack.Count > 0)
            {
                result = stack.Peek();
                return true;
            }
            else
            {
                result = default!;
                return false;
            }
        }
    }
}
#endif