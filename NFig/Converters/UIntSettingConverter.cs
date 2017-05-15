#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class UIntSettingConverter : ISettingConverter<uint>
    {
        public string GetString(uint value) { return value.ToString(); }
        public uint GetValue(string s) { return uint.Parse(s); }
    }
}