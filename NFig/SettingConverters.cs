using System;

namespace NFig
{
    public interface ISettingConverter<TValue>
    {
        string GetString(TValue value);
        TValue GetValue(string s);
    }

    // Binary Converters

    public class BooleanSettingConverter : ISettingConverter<bool>
    {
        public string GetString(bool b) { return b.ToString(); }
        public bool GetValue(string s) { return bool.Parse(s); }
    }

    // Numeric Converters

    public class ByteSettingConverter : ISettingConverter<byte>
    {
        public string GetString(byte value) { return value.ToString(); }
        public byte GetValue(string s) { return byte.Parse(s); }
    }

    public class ShortSettingConverter : ISettingConverter<short>
    {
        public string GetString(short value) { return value.ToString(); }
        public short GetValue(string s) { return short.Parse(s); }
    }

    public class UShortSettingConverter : ISettingConverter<ushort>
    {
        public string GetString(ushort value) { return value.ToString(); }
        public ushort GetValue(string s) { return ushort.Parse(s); }
    }

    public class IntSettingConverter : ISettingConverter<int>
    {
        public string GetString(int value) { return value.ToString(); }
        public int GetValue(string s) { return int.Parse(s); }
    }

    public class UIntSettingConverter : ISettingConverter<uint>
    {
        public string GetString(uint value) { return value.ToString(); }
        public uint GetValue(string s) { return uint.Parse(s); }
    }

    public class LongSettingConverter : ISettingConverter<long>
    {
        public string GetString(long value) { return value.ToString(); }
        public long GetValue(string s) { return long.Parse(s); }
    }

    public class ULongSettingConverter : ISettingConverter<ulong>
    {
        public string GetString(ulong value) { return value.ToString(); }
        public ulong GetValue(string s) { return ulong.Parse(s); }
    }

    public class FloatSettingConverter : ISettingConverter<float>
    {
        public string GetString(float value) { return value.ToString(); }
        public float GetValue(string s) { return float.Parse(s); }
    }

    public class DoubleSettingConverter : ISettingConverter<double>
    {
        public string GetString(double value) { return value.ToString(); }
        public double GetValue(string s) { return double.Parse(s); }
    }

    public class DecimalSettingConverter : ISettingConverter<decimal>
    {
        public string GetString(decimal value) { return value.ToString(); }
        public decimal GetValue(string s) { return decimal.Parse(s); }
    }

    // Text Converters

    public class StringSettingConverter : ISettingConverter<string>
    {
        public string GetString(string value) { return value; }
        public string GetValue(string s) { return s; }
    }

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