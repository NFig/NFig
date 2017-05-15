#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class FloatSettingConverter : ISettingConverter<float>
    {
        public string GetString(float value) { return value.ToString(); }
        public float GetValue(string s) { return float.Parse(s); }
    }
}