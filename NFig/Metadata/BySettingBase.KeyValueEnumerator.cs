using System.Collections;
using System.Collections.Generic;

namespace NFig.Metadata
{
    public abstract partial class BySettingBase<TValue>
    {
        /// <summary>
        /// Enumerator for <see cref="BySetting{TValue}"/>
        /// </summary>
        public struct KeyValueEnumerator : IEnumerator<KeyValuePair<string, TValue>>
        {
            readonly Entry[] _entries;
            int _entryIndex;

            /// <summary>
            /// The current value of the enumerator. This is undefined before <see cref="MoveNext()"/> is called, and after <see cref="MoveNext()"/> returns false.
            /// </summary>
            public KeyValuePair<string, TValue> Current { get; private set; }
            object IEnumerator.Current => Current;

            internal KeyValueEnumerator(BySetting<TValue> dictionary)
            {
                _entries = dictionary._entries;
                _entryIndex = -1;
                Current = default(KeyValuePair<string, TValue>);
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
                    Current = new KeyValuePair<string, TValue>(_entries[index].Key, _entries[index].Value);
                    return true;
                }

                Current = default(KeyValuePair<string, TValue>);
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
