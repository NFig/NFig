using System;

#pragma warning disable 1591 // disable missing XML comments warning
namespace NFig.Converters
{
    public class CharSettingConverter : ISettingConverter<char>
    {
        public string GetString(char value)
        {
            return value.ToString();
        }

        public char GetValue(string s)
        {
            if (s == null || s.Length != 1)
                throw new Exception("Cannot convert \"" + s + "\" to char.");

            return s[0];
        }
    }
}