using System.Collections.Generic;
using System.Threading;

namespace Contextual
{
    internal static class ContextStack
    {
        private static class Container<T> where T : Context
        {
            public static AsyncLocal<Stack<T>?> Stack { get; } = new AsyncLocal<Stack<T>?>();
        }

        public static Stack<T> Of<T>() where T : Context =>
            Container<T>.Stack.Value ??= new Stack<T>();
    }
}