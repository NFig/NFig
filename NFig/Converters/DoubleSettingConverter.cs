#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class DoubleSettingConverter : ISettingConverter<double>
    {
        public string GetString(double value) { return value.ToString(); }
        public double GetValue(string s) { return double.Parse(s); }
    }
}