#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class LongSettingConverter : ISettingConverter<long>
    {
        public string GetString(long value) { return value.ToString(); }
        public long GetValue(string s) { return long.Parse(s); }
    }
}