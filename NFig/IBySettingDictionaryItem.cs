namespace NFig
{
    /// <summary>
    /// The interface for the values of an <see cref="BySettingDictionary{TValue}"/>.
    /// </summary>
    public interface IBySettingDictionaryItem
    {
        /// <summary>
        /// The setting name. This property will become the key in a <see cref="BySettingDictionary{TValue}"/>.
        /// </summary>
        string Name { get; }
    }
}