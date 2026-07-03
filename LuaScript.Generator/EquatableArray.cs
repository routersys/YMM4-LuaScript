using System.Collections;

namespace LuaScript.Generator
{
    internal readonly struct EquatableArray<T>(T[] items) : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
        where T : IEquatable<T>
    {
        private readonly T[] _items = items;

        public static readonly EquatableArray<T> Empty = new([]);

        public int Count => _items?.Length ?? 0;

        public T this[int index] => _items[index];

        public bool Equals(EquatableArray<T> other)
        {
            var a = _items ?? [];
            var b = other._items ?? [];
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var items = _items ?? [];
            int hash = 17;
            foreach (var item in items)
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            return hash;
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? [])).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
