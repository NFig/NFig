namespace NFig
{
    /// <summary>
    /// The interface for the values of an <see cref="BySetting{TValue}"/> or <see cref="ListBySetting{TValue}"/>.
    /// </summary>
    public interface IBySettingItem
    {
        /// <summary>
        /// The setting name. When used in a <see cref="BySetting{TValue}"/> or <see cref="ListBySetting{TValue}"/>, this property will become the key.
        /// </summary>
        string Name { get; }
    }
}