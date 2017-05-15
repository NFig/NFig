#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class DecimalSettingConverter : ISettingConverter<decimal>
    {
        public string GetString(decimal value) { return value.ToString(); }
        public decimal GetValue(string s) { return decimal.Parse(s); }
    }
}