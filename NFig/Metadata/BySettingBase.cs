using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NFig.Metadata
{
    /*
     * This is a custom dictionary implementation which is optimized for grouping values by a setting name. It can be used in two different modes: single value
     * and muti-value. In single value, the class essentially acts as IReadOnlyDictionary<string, TValue>. In multi-value mode, it acts as
     * IReadOnlyDictionary<string, IReadOnlyCollection<TValue>>. However, the internal representation is identical in both cases.
     * 
     * It is implemented using similar methods to how the BCL Dictionary is implemented, with a buckets array and entries array. The buckets represent the hash
     * buckets. You take the Key (a string), calculate its hash code, then modulo the buckets length. This gives you an index into the buckets array. If the
     * value of that bucket is zero, then nothing matches that hash code. If it is greater than zero, then the value represents a 1-based index into the
     * entries array.
     * 
     * Due to collisions, more than one entry may match the same HashCode % Buckets.Length value. In this case, a linked list is formed via the NextNonDuplicate
     * field.
     * 
     * In multi-value mode, duplicate keys are allowed. The entries array is sorted by key, so all duplicate keys are adjacent to each other. The first entry
     * with a given key is treated as the first element (the head) of a list. The head entry will have its ListLength field set to indicate how many entries
     * (including itself) share the same key. Buckets, and the NextNonDuplicate field, always point to the head of a list.
     */
     /// <summary>
     /// This is the base class for <see cref="BySetting{TValue}"/> and <see cref="ListBySetting{TValue}"/>. It is not usable on its own.
     /// </summary>
    public abstract partial class BySettingBase<TValue> where TValue : IBySettingItem
    {
        struct Entry
        {
            /// <summary>
            /// The low 31-bits of the key's hash code.
            /// </summary>
            public int HashCode;
            /// <summary>
            /// Linked list which points to the head of another list. This is a 1-based index, so you'll need to subtract one from it before using it as a
            /// 0-based index. A zero value indicates this is the end of the linked list.
            /// </summary>
            public int NextNonDuplicate;
            /// <summary>
            /// How many items are in the values-list for the current key. This value is only set for the first entry in a list. For all other entries, this
            /// value will be zero.
            /// </summary>
            public int ListLength;
            /// <summary>
            /// The dictionary key value of the entry.
            /// </summary>
            public string Key;
            /// <summary>
            /// The value of the dictionary entry.
            /// </summary>
            public TValue Value;
        }

        const int LOW_31_BITS = 0x7FFFFFFF;

        readonly int[] _buckets;
        readonly Entry[] _entries;

        /// <summary>
        /// The number of key/value pairs in the dictionary.
        /// </summary>
        public int Count { get; }

        internal BySettingBase(IReadOnlyCollection<TValue> values, BySettingBase<TValue> mergeDictionary, bool allowDuplicates)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            _entries = GetEntries(values, mergeDictionary);
            _buckets = GetBuckets(_entries, allowDuplicates, out var keyCount);
            Count = keyCount;
        }

        /// <summary>
        /// Serializes the dictionary to JSON.
        /// </summary>
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Returns true if the dictionary contains the key (a setting name).
        /// </summary>
        public bool ContainsKey(string key)
        {
            return GetEntryIndex(key) > -1;
        }

        /// <summary>
        /// Provides functionality for single-value try-get.
        /// </summary>
        protected bool TryGetValueInternal(string key, out TValue value)
        {
            var ei = GetEntryIndex(key);

            if (ei < 0)
            {
                value = default(TValue);
                return false;
            }

            value = _entries[ei].Value;
            return true;
        }

        /// <summary>
        /// Provides functionality for multi-value try-get.
        /// </summary>
        protected bool TryGetList(string key, out ValueList list)
        {
            var ei = GetEntryIndex(key);

            if (ei < 0)
            {
                list = default(ValueList);
                return false;
            }

            list = new ValueList(this, ei);
            return true;
        }

        int GetEntryIndex(string key)
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
                ref var entry = ref entries[ei];

                if (entry.HashCode == hash && entry.Key == key)
                {
                    return ei;
                }

                next = entry.NextNonDuplicate;
            }

            return -1;
        }

        static Entry[] GetEntries(IReadOnlyCollection<TValue> values, BySettingBase<TValue> mergeDictionary)
        {
            var count = values.Count;

            if (mergeDictionary != null)
                count += mergeDictionary._entries.Length;

            var entries = new Entry[count];

            var i = 0;
            foreach (var v in values) // this actually won't be null because of the while condition, even though it looks like maybe it can be
            {
                var name = v.Name;

                entries[i].Key = name;
                entries[i].Value = v;
                entries[i].HashCode = name.GetHashCode() & LOW_31_BITS;
                i++;
            }

            if (mergeDictionary != null)
            {
                foreach (var v in new ValueCollection(mergeDictionary))
                {
                    var name = v.Name;

                    entries[i].Key = name;
                    entries[i].Value = v;
                    entries[i].HashCode = name.GetHashCode() & LOW_31_BITS;
                    i++;
                }
            }

            // sort alphabetically
            Array.Sort(entries, CompareValues);

            return entries;
        }

        static int[] GetBuckets(Entry[] entries, bool allowDuplicates, out int keyCount)
        {
            var bucketCount = (int)(entries.Length * 1.3); // create a few more buckets than entries so we get a load factor less than 100%
            var buckets = new int[bucketCount];

            keyCount = 0;
            var listHeadIndex = -1;
            for (var ei = 0; ei < entries.Length; ei++)
            {
                ref var entry = ref entries[ei];

                if (listHeadIndex > -1 && entries[listHeadIndex].HashCode == entry.HashCode && entries[listHeadIndex].Key == entry.Key)
                {
                    // duplicate key
                    if (!allowDuplicates)
                        throw new InvalidOperationException($"Duplicate key \"{entry.Key}\"");

                    entries[listHeadIndex].ListLength++;
                    continue; // duplicate keys don't get recorded in a bucket
                }

                // start of a new list
                listHeadIndex = ei;
                entry.ListLength = 1;
                keyCount++;

                var bi = entry.HashCode % bucketCount;

                // set up a linked list, in case an entry is already using this bucket
                entry.NextNonDuplicate = buckets[bi];
                buckets[bi] = ei + 1;
            }

            return buckets;
        }

        static int CompareValues(Entry a, Entry b)
        {
            return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        }
    }
}