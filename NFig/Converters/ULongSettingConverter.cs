#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class ULongSettingConverter : ISettingConverter<ulong>
    {
        public string GetString(ulong value) { return value.ToString(); }
        public ulong GetValue(string s) { return ulong.Parse(s); }
    }
}