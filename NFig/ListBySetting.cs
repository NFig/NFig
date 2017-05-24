using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NFig
{
    /// <summary>
    /// An immutable dictionary where they keys are a setting name, and the values are lists of <typeparamref name="TValue"/>.
    /// </summary>
    public class ListBySetting<TValue> : BySettingBase<TValue>, IReadOnlyDictionary<string, BySettingBase<TValue>.ValueList>
        where TValue : IBySettingItem
    {
        /// <summary>
        /// The keys of the dictionary (setting names).
        /// </summary>
        public KeyCollection Keys => new KeyCollection(this);
        /// <summary>
        /// The values of the dictionary (lists of <typeparamref name="TValue"/>).
        /// </summary>
        public ValueListCollection Values => new ValueListCollection(this);

        IEnumerable<string> IReadOnlyDictionary<string, ValueList>.Keys => Keys;
        IEnumerable<ValueList> IReadOnlyDictionary<string, ValueList>.Values => Values;

        /// <summary>
        /// Gets a list of <typeparamref name="TValue"/> by setting name, or throws an exception if not found.
        /// </summary>
        public ValueList this[string settingName]
        {
            get
            {
                if (TryGetValue(settingName, out var value))
                    return value;

                throw new KeyNotFoundException($"Setting name \"{settingName}\" was not found in the dictionary.");
            }
        }

        /// <summary>
        /// Initializes a new ListBySetting dictionary. Values with identical setting names will be grouped into lists as the values of the dictionary.
        /// </summary>
        /// <param name="values">A collection of values to populate the dictionary with.</param>
        /// <param name="mergeDictionary">
        /// (optional) A dictionary whose values you want to merge with <paramref name="values"/> to create a new dictionary.
        /// </param>
        public ListBySetting([NotNull] IReadOnlyCollection<TValue> values, BySettingBase<TValue> mergeDictionary = null)
            : base(values, mergeDictionary, false)
        {
        }

        /// <summary>
        /// Gets an enumeration of all <typeparamref name="TValue"/> items in all lists, as if it were a single collection.
        /// </summary>
        public ValueCollection GetAllValues() => new ValueCollection(this);

        /// <summary>
        /// Gets a key/value enumerator for <see cref="ListBySetting{TValue}"/>.
        /// </summary>
        public KeyValueListEnumerator GetEnumerator() => new KeyValueListEnumerator(this);
        IEnumerator<KeyValuePair<string, ValueList>> IEnumerable<KeyValuePair<string, ValueList>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Trys to get a list of <typeparamref name="TValue"/> by setting name from the dictionary. Returns false if no list was found.
        /// </summary>
        public bool TryGetValue(string settingName, out ValueList value) => TryGetList(settingName, out value);
    }
}