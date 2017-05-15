#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class StringSettingConverter : ISettingConverter<string>
    {
        public string GetString(string value) { return value; }
        public string GetValue(string s) { return s; }
    }
}