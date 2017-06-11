using System.Collections;
using System.Collections.Generic;

namespace NFig.Metadata
{
    public abstract partial class BySettingBase<TValue>
    {
        /// <summary>
        /// A value-type collection for keys (setting names).
        /// </summary>
        public struct KeyCollection : IReadOnlyCollection<string>
        {
            readonly BySettingBase<TValue> _dictionary;

            /// <summary>
            /// The number of keys to enumerate.
            /// </summary>
            public int Count => _dictionary.Count;

            internal KeyCollection(BySettingBase<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            /// <summary>
            /// Gets the enumerator for keys (setting names).
            /// </summary>
            public Enumerator GetEnumerator() => new Enumerator(_dictionary);
            IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// A value-type enumerator for keys (setting names).
            /// </summary>
            public struct Enumerator : IEnumerator<string>
            {
                readonly Entry[] _entries;
                int _entryIndex;

                /// <summary>
                /// The current value of the enumerator. This is undefined before <see cref="MoveNext()"/> is called, and after <see cref="MoveNext()"/> returns
                /// false.
                /// </summary>
                public string Current { get; private set; }

                object IEnumerator.Current => Current;

                internal Enumerator(BySettingBase<TValue> dictionary)
                {
                    _entries = dictionary._entries;
                    _entryIndex = -1;
                    Current = null;
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
                    var index = _entryIndex;

                    // We want to advance to the next unique key, so we jump by ListLength rather than just += 1.
                    index += index < 0 ? 1 : _entries[index].ListLength;
                    _entryIndex = index;

                    if (index < _entries.Length)
                    {
                        Current = _entries[index].Key;
                        return true;
                    }

                    Current = null;
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
