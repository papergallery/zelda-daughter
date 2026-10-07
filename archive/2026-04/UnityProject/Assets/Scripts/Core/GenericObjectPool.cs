using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core
{
    public class GenericObjectPool<T>
    {
        private readonly Stack<T> _stack = new();
        private readonly Func<T> _createFunc;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;

        public int CountInactive => _stack.Count;

        public GenericObjectPool(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, int initialCapacity = 0)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _onGet = onGet;
            _onRelease = onRelease;
            Prewarm(initialCapacity);
        }

        public T Get()
        {
            T obj = _stack.Count > 0 ? _stack.Pop() : _createFunc();
            _onGet?.Invoke(obj);
            return obj;
        }

        public void Release(T obj)
        {
            _onRelease?.Invoke(obj);
            _stack.Push(obj);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
                _stack.Push(_createFunc());
        }
    }
}
