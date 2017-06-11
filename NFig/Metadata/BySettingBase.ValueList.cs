using System;
using System.Collections;
using System.Collections.Generic;

namespace NFig.Metadata
{
    public abstract partial class BySettingBase<TValue>
    {
        /// <summary>
        /// A value-type read-only list.
        /// </summary>
        public struct ValueList : IReadOnlyList<TValue>
        {
            readonly BySettingBase<TValue> _dictionary;
            readonly int _offset;

            /// <summary>
            /// The number of values in the list.
            /// </summary>
            public int Count { get; }

            /// <summary>
            /// Gets a value by index.
            /// </summary>
            public TValue this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                        throw new ArgumentOutOfRangeException(nameof(index));

                    return _dictionary._entries[_offset + index].Value;
                }
            }

            internal ValueList(BySettingBase<TValue> dictionary, int entryIndex)
            {
                _dictionary = dictionary;
                _offset = entryIndex;
                Count = dictionary._entries[entryIndex].ListLength;
            }

            /// <summary>
            /// Gets an enumerator for the list.
            /// </summary>
            public Enumerator GetEnumerator()
            {
                return new Enumerator(_dictionary, _offset);
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            /// Enumerator for <see cref="ValueList"/>.
            /// </summary>
            public struct Enumerator : IEnumerator<TValue>
            {
                readonly Entry[] _entries;
                readonly int _start;
                readonly int _end;
                int _entryIndex;

                /// <summary>
                /// The current value of the enumerator. This is undefined before <see cref="MoveNext()"/> is called, and after <see cref="MoveNext()"/> returns false.
                /// </summary>
                public TValue Current { get; private set; }

                object IEnumerator.Current => Current;

                internal Enumerator(BySettingBase<TValue> dictionary, int entryIndex)
                {
                    _entries = dictionary._entries;
                    _start = entryIndex;
                    _end = entryIndex + dictionary._entries[entryIndex].ListLength;
                    _entryIndex = entryIndex - 1;

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

                    if (index < _end)
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
                    _entryIndex = _start - 1;
                }
            }
        }
    }
}
