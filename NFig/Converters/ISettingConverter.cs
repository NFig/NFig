namespace NFig.Converters
{
    /// <summary>
    /// An empty non-generic interface to help the type system give hints on correct converter parameters.
    /// </summary>
    public interface ISettingConverter
    {
    }

    /// <summary>
    /// The interface for converting the value of a setting between a value/object representation and a string representation.
    /// </summary>
    public interface ISettingConverter<TValue> : ISettingConverter
    {
        /// <summary>
        /// Transforms a value/object representation into a string representation.
        /// </summary>
        string GetString(TValue value);
        /// <summary>
        /// Transforms a string representation of setting into a value/object representation.
        /// </summary>
        TValue GetValue(string s);
    }
}