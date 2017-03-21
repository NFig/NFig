namespace NFig
{
    /// <summary>
    /// The interface for the values of an <see cref="BySettingDictionary{TValue}"/>.
    /// </summary>
    public interface IBySettingDictionaryItem
    {
        /// <summary>
        /// The setting name. When used in a <see cref="BySettingDictionary{TValue}"/>, this property will become the key.
        /// </summary>
        string Name { get; }
    }
}