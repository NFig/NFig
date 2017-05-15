#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class BooleanSettingConverter : ISettingConverter<bool>
    {
        public string GetString(bool b) { return b.ToString(); }
        public bool GetValue(string s) { return bool.Parse(s); }
    }
}