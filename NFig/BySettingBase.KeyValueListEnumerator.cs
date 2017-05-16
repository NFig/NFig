using System.Collections;
using System.Collections.Generic;

namespace NFig
{
    public abstract partial class BySettingBase<TValue>
    {
        /// <summary>
        /// Enumerator for <see cref="ListBySetting{TValue}"/>.
        /// </summary>
        public struct KeyValueListEnumerator : IEnumerator<KeyValuePair<string, ValueList>>
        {
            readonly ListBySetting<TValue> _dictionary;
            int _entryIndex;

            /// <summary>
            /// The current value of the enumerator. This is undefined before <see cref="MoveNext"/> is called, and after <see cref="MoveNext"/> returns false.
            /// </summary>
            public KeyValuePair<string, ValueList> Current { get; private set; }
            object IEnumerator.Current => Current;

            internal KeyValueListEnumerator(ListBySetting<TValue> dictionary)
            {
                _dictionary = dictionary;
                _entryIndex = -1;
                Current = default(KeyValuePair<string, ValueList>);
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
                    Current = new KeyValuePair<string, ValueList>(entries[index].Key, new ValueList(_dictionary, index));
                    return true;
                }

                Current = default(KeyValuePair<string, ValueList>);
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
