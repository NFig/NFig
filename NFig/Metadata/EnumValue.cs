using Newtonsoft.Json;

namespace NFig.Metadata
{
    /// <summary>
    /// The name and numeric value of an individual enum value.
    /// </summary>
    public struct EnumValue
    {
        /// <summary>
        /// The numeric value.
        /// </summary>
        public long Value { get; }
        /// <summary>
        /// The text value.
        /// </summary>
        public string Name { get; }

        [JsonConstructor]
        internal EnumValue(long value, string name)
        {
            Value = value;
            Name = name;
        }
    }
}