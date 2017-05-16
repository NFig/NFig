using System.Collections;
using System.Collections.Generic;

namespace NFig
{
    public abstract partial class BySettingBase<TValue>
    {
        /// <summary>
        /// A value-type collection of dictionary values for <see cref="BySetting{TValue}"/>.
        /// </summary>
        public struct ValueCollection : IReadOnlyCollection<TValue>
        {
            readonly BySetting<TValue> _dictionary;

            /// <summary>
            /// The number of values in the collection.
            /// </summary>
            public int Count => _dictionary._entries.Length;

            internal ValueCollection(BySetting<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            /// <summary>
            /// Gets the enumerator for dictionary values.
            /// </summary>
            public Enumerator GetEnumerator() => new Enumerator(_dictionary);
            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Enumerator for <see cref="ValueCollection"/>.
            /// </summary>
            public struct Enumerator : IEnumerator<TValue>
            {
                readonly Entry[] _entries;
                int _entryIndex;

                /// <summary>
                /// The current value of the enumerator. This is undefined before <see cref="MoveNext"/> is called, and after <see cref="MoveNext"/> returns false.
                /// </summary>
                public TValue Current { get; private set; }
                object IEnumerator.Current => Current;

                internal Enumerator(BySetting<TValue> dictionary)
                {
                    _entries = dictionary._entries;
                    _entryIndex = -1;
                    Current = default(TValue);
                }

                /// <summary>
                /// Does nothing.
                /// </summary>
                public void Dispose()
                {
                }

                /// <summary>
                /// Advances the enumerator. Returns false if the enumerator has reached the end of the dictionary.
                /// </summary>
                public bool MoveNext()
                {
                    var index = _entryIndex + 1;
                    _entryIndex = index;

                    if (index < _entries.Length)
                    {
                        Current = _entries[index].Value;
                        return true;
                    }

                    Current = default(TValue);
                    return false;
                }

                /// <summary>
                /// Resets the enumerator to its original position.
                /// </summary>
                public void Reset()
                {
                    _entryIndex = -1;
                }
            }
        }
    }
}
