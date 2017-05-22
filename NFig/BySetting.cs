using System.Collections;
using System.Collections.Generic;

namespace NFig
{
    /// <summary>
    /// An immutable dictionary where they keys are a setting name, and the values are <typeparamref name="TValue"/>.
    /// </summary>
    public class BySetting<TValue> : BySettingBase<TValue>, IReadOnlyDictionary<string, TValue>
        where TValue : IBySettingDictionaryItem
    {
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
        /// Gets a <typeparamref name="TValue"/> by setting name, or throws an exception if not found.
        /// </summary>
        public TValue this[string settingName]
        {
            get
            {
                if (TryGetValue(settingName, out var value))
                    return value;

                throw new KeyNotFoundException($"Setting name \"{settingName}\" was not found in the dictionary.");
            }
        }

        /// <summary>
        /// Initializes a new BySetting dictionary with <typeparamref name="TValue"/> as values.
        /// </summary>
        public BySetting(IReadOnlyCollection<TValue> values) : base(values, false)
        {
        }

        /// <summary>
        /// Gets a key/value enumerator for <see cref="BySetting{TValue}"/>.
        /// </summary>
        public KeyValueEnumerator GetEnumerator() => new KeyValueEnumerator(this);
        IEnumerator<KeyValuePair<string, TValue>> IEnumerable<KeyValuePair<string, TValue>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Trys to get a <typeparamref name="TValue"/> by setting name from the dictionary. Returns false if no list was found.
        /// </summary>
        public bool TryGetValue(string settingName, out TValue value) => TryGetValueInternal(settingName, out value);
    }
}