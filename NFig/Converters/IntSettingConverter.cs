#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class IntSettingConverter : ISettingConverter<int>
    {
        public string GetString(int value) { return value.ToString(); }
        public int GetValue(string s) { return int.Parse(s); }
    }
}