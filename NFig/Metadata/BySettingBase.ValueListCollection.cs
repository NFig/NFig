using System.Collections;
using System.Collections.Generic;

namespace NFig.Metadata
{
    public abstract partial class BySettingBase<TValue>
    {
        /// <summary>
        /// A value-type collection of dictionary values for <see cref="ListBySetting{TValue}"/>.
        /// </summary>
        public struct ValueListCollection : IReadOnlyCollection<ValueList>
        {
            readonly ListBySetting<TValue> _dictionary;

            /// <summary>
            /// The number of lists to enumerate.
            /// </summary>
            public int Count => _dictionary.Count;

            internal ValueListCollection(ListBySetting<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            /// <summary>
            /// Gets the enumerator for keys (setting names).
            /// </summary>
            public Enumerator GetEnumerator() => new Enumerator(_dictionary);
            IEnumerator<ValueList> IEnumerable<ValueList>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Enumerator for <see cref="ValueListCollection"/>.
            /// </summary>
            public struct Enumerator : IEnumerator<ValueList>
            {
                readonly ListBySetting<TValue> _dictionary;
                int _entryIndex;

                /// <summary>
                /// The current value of the enumerator. This is undefined before <see cref="MoveNext()"/> is called, and after <see cref="MoveNext()"/> returns
                /// false.
                /// </summary>
                public ValueList Current { get; private set; }
                object IEnumerator.Current => Current;

                internal Enumerator(ListBySetting<TValue> dictionary)
                {
                    _dictionary = dictionary;
                    _entryIndex = -1;
                    Current = default(ValueList);
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
                    var entries = _dictionary._entries;

                    // We want to advance to the next unique key, so we jump by ListLength rather than just += 1.
                    index += index < 0 ? 1 : entries[index].ListLength;
                    _entryIndex = index;

                    if (index < entries.Length)
                    {
                        Current = new ValueList(_dictionary, index);
                        return true;
                    }

                    Current = default(ValueList);
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
