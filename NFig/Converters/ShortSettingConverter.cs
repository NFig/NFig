#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class ShortSettingConverter : ISettingConverter<short>
    {
        public string GetString(short value) { return value.ToString(); }
        public short GetValue(string s) { return short.Parse(s); }
    }
}