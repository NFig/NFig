#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class UShortSettingConverter : ISettingConverter<ushort>
    {
        public string GetString(ushort value) { return value.ToString(); }
        public ushort GetValue(string s) { return ushort.Parse(s); }
    }
}