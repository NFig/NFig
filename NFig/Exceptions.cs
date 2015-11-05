using System;

namespace NFig
{
    public class NFigException : Exception
    {
        public NFigException (string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }
    public class InvalidSettingConverterException : NFigException
    {
        public Type SettingType { get; }

        public InvalidSettingConverterException(string message, Type settingType, Exception innerException = null) : base(message, innerException)
        {
            SettingType = settingType;
        }
    }

    public class InvalidSettingValueException<TTier, TDataCenter> : NFigException
        where TTier : struct
        where TDataCenter : struct
    {
        public string SettingName { get; }
        public object Value { get; }
        public bool IsDefault { get; }
        public bool IsOverride => !IsDefault;
        public TTier Tier { get; }
        public TDataCenter DataCenter { get; }

        public InvalidSettingValueException(
            string message,
            string settingName,
            object value,
            bool isDefault,
            TTier tier,
            TDataCenter dataCenter,
            Exception innerException = null) 
            : base(message, innerException)
        {
            Data["SettingName"] = SettingName = settingName;
            Data["Value"] = Value = value;
            Data["IsDefault"] = IsDefault = isDefault;
            Data["IsOverride"] = !isDefault;
            Data["Tier"] = Tier = tier;
            Data["DataCenter"] = DataCenter = dataCenter;
        }
    }
}