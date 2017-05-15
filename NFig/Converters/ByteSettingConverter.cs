#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class ByteSettingConverter : ISettingConverter<byte>
    {
        public string GetString(byte value) { return value.ToString(); }
        public byte GetValue(string s) { return byte.Parse(s); }
    }
}