using System;
using System.Collections;
using System.Collections.Generic;

namespace NFig
{
    /// <summary>
    /// This is a simple read-only dictionary where the key is always a setting name. Keys and values are guaranteed to enumerate in alphabetical order.
    /// </summary>
    public class BySettingDictionary<TValue> : IReadOnlyDictionary<string, TValue> where TValue : IBySettingDictionaryItem
    {
        struct Entry
        {
            public int HashCode;
            public int Next;
            public string Key;
            public TValue Value;
        }

        const int LOW_31_BITS = 0x7FFFFFFF;

        readonly int[] _buckets;
        readonly Entry[] _entries;

        /// <summary>
        /// The number of key/value pairs in the dictionary.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Enumerates the setting names (keys) in alphabetical order.
        /// </summary>
        public KeyCollection Keys => new KeyCollection(this);

        /// <summary>
        /// Enumerates the values in alphabetical order by setting name (key).
        /// </summary>
        public ValueCollection Values => new ValueCollection(this);

        IEnumerable<string> IReadOnlyDictionary<string, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<string, TValue>.Values => Values;

        /// <summary>
        /// Gets a value by setting name. A KeyNotFoundException exception is thrown if the value does not exist.
        /// </summary>
        public TValue this[string settingName]
        {
            get
            {
                TValue value;
                if (TryGetValue(settingName, out value))
                    return value;

                throw new KeyNotFoundException($"Key \"{settingName}\" was not found in the dictionary.");
            }
        }

        /// <summary>
        /// Instantiates a new dictionary with <paramref name="values"/> as the values, and value.Name as the key.
        /// </summary>
        public BySettingDictionary(IReadOnlyCollection<TValue> values)
        {
            _entries = GetEntries(values);
            _buckets = GetBuckets(_entries);
            Count = _entries.Length;
        }

        /// <summary>
        /// Returns true if the dictionary contains an entry for the key (setting name).
        /// </summary>
        public bool ContainsKey(string key)
        {
            TValue _;
            return TryGetValue(key, out _);
        }

        /// <summary>
        /// Gets the value associated with the key (setting name). Returns true if it exists, otherwise false.
        /// </summary>
        public bool TryGetValue(string key, out TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var hash = key.GetHashCode() & LOW_31_BITS;
            var buckets = _buckets;
            var bi = hash % buckets.Length;
            var next = buckets[bi];

            var entries = _entries;
            while (next > 0)
            {
                var ei = next - 1;

                if (entries[ei].HashCode == hash && entries[ei].Key == key)
                {
                    value = entries[ei].Value;
                    return true;
                }

                next = entries[ei].Next;
            }

            value = default(TValue);
            return false;
        }

        /// <summary>
        /// Gets an enumerator for the dictionary. Key/value pairs will enumerate in alphabetical order by setting name (key).
        /// </summary>
        public KeyValueEnumerator GetEnumerator()
        {
            return new KeyValueEnumerator(this);
        }

        IEnumerator<KeyValuePair<string, TValue>> IEnumerable<KeyValuePair<string, TValue>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        static Entry[] GetEntries(IReadOnlyCollection<TValue> values)
        {
            var entries = new Entry[values.Count];

            var i = 0;
            foreach (var v in values)
            {
                var name = v.Name;

                entries[i].Key = name;
                entries[i].Value = v;
                entries[i].HashCode = name.GetHashCode() & LOW_31_BITS;
                i++;
            }

            // sort alphabetically
            Array.Sort(entries, CompareValues);

            return entries;
        }

        static int[] GetBuckets(Entry[] entries)
        {
            var bucketCount = (int)(entries.Length * 1.3); // create a few more buckets than entries so we get a load factor less than 100%
            var buckets = new int[bucketCount];

            for (var ei = 0; ei < entries.Length; ei++)
            {
                var bi = entries[ei].HashCode % bucketCount;

                // set up a linked list if an entry is already using this bucket
                var oldBucketValue = buckets[bi];
                if (oldBucketValue != 0)
                {
                    entries[ei].Next = oldBucketValue;

                    // make sure there are no duplicates
                    var prevValue = oldBucketValue;
                    while (prevValue > 0)
                    {
                        if (entries[prevValue - 1].HashCode == entries[ei].HashCode && entries[prevValue - 1].Key == entries[ei].Key)
                            throw new InvalidOperationException($"Duplicate key \"{entries[ei].Key}\"");

                        prevValue = entries[prevValue - 1].Next;
                    }
                }

                buckets[bi] = ei + 1;
            }

            return buckets;
        }

        static int CompareValues(Entry a, Entry b)
        {
            return string.Compare(a.Key, b.Key, StringComparison.InvariantCulture);
        }

        /******************************************************************************************************************************************************
         * Enumerators
        ******************************************************************************************************************************************************/

        /// <summary>
        /// Enumerator for <see cref="BySettingDictionary{TValue}"/>
        /// </summary>
        public struct KeyValueEnumerator : IEnumerator<KeyValuePair<string, TValue>>
        {
            readonly Entry[] _entries;
            int _entryIndex;

            /// <summary>
            /// The current value of the enumerator. This is undefined before <see cref="MoveNext"/> is called, and after <see cref="MoveNext"/> returns false.
            /// </summary>
            public KeyValuePair<string, TValue> Current { get; private set; }

            object IEnumerator.Current => Current;

            internal KeyValueEnumerator(BySettingDictionary<TValue> dictionary)
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

        /// <summary>
        /// A value-type collection for keys (setting names).
        /// </summary>
        public struct KeyCollection : IReadOnlyCollection<string>
        {
            readonly BySettingDictionary<TValue> _dictionary;

            /// <summary>
            /// The number of keys to enumerate.
            /// </summary>
            public int Count => _dictionary.Count;

            internal KeyCollection(BySettingDictionary<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            /// <summary>
            /// Gets the enumerator for keys (setting names).
            /// </summary>
            public KeyEnumerator GetEnumerator()
            {
                return new KeyEnumerator(_dictionary);
            }

            IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// A value-type enumerator for keys (setting names).
        /// </summary>
        public struct KeyEnumerator : IEnumerator<string>
        {
            readonly Entry[] _entries;
            int _entryIndex;

            /// <summary>
            /// The current value of the enumerator. This is undefined before <see cref="MoveNext"/> is called, and after <see cref="MoveNext"/> returns false.
            /// </summary>
            public string Current { get; private set; }

            object IEnumerator.Current => Current;

            internal KeyEnumerator(BySettingDictionary<TValue> dictionary)
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
                var index = _entryIndex + 1;
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

        /// <summary>
        /// A value-type collection of dictionary values.
        /// </summary>
        public struct ValueCollection : IReadOnlyCollection<TValue>
        {
            readonly BySettingDictionary<TValue> _dictionary;

            /// <summary>
            /// The number of values to enumerate.
            /// </summary>
            public int Count => _dictionary.Count;

            internal ValueCollection(BySettingDictionary<TValue> dictionary)
            {
                _dictionary = dictionary;
            }

            /// <summary>
            /// Gets the enumerator for dictionary values.
            /// </summary>
            public ValueEnumerator GetEnumerator()
            {
                return new ValueEnumerator(_dictionary);
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// A value-type enumerable for dictionary values.
        /// </summary>
        public struct ValueEnumerator : IEnumerator<TValue>
        {
            readonly Entry[] _entries;
            int _entryIndex;

            /// <summary>
            /// The current value of the enumerator. This is undefined before <see cref="MoveNext"/> is called, and after <see cref="MoveNext"/> returns false.
            /// </summary>
            public TValue Current { get; private set; }

            object IEnumerator.Current => Current;

            internal ValueEnumerator(BySettingDictionary<TValue> dictionary)
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