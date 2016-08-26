using System;
using System.Collections.Generic;
using System.Linq;

namespace NFig
{
    public class NFigException : Exception
    {
        public NFigException (string message, Exception innerException = null) : base(message, innerException)
        {
        }

        internal string UnthrownStackTrace { get; set; }

        public override string StackTrace
        {
            get
            {
                if (string.IsNullOrEmpty(UnthrownStackTrace))
                    return base.StackTrace;

                if (string.IsNullOrEmpty(base.StackTrace))
                    return UnthrownStackTrace;

                return "--- Original Stack Trace ---\r\n" + UnthrownStackTrace + "\r\n\r\n--- Thrown From ---\r\n" + base.StackTrace;
            }
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

    public class InvalidSettingValueException : NFigException
    {

        public string SettingName { get; }
        public object Value { get; }
        public bool IsDefault { get; }
        public bool IsOverride => !IsDefault;

        public InvalidSettingValueException(
            string message,
            string settingName,
            object value,
            bool isDefault,
            string subApp,
            string dataCenter,
            Exception innerException = null) 
            : base(message, innerException)
        {
            Data["SettingName"] = SettingName = settingName;
            Data["Value"] = Value = value;
            Data["IsDefault"] = IsDefault = isDefault;
            Data["IsOverride"] = !isDefault;
            Data["SubApp"] = subApp;
            Data["DataCenter"] = dataCenter;
        }
    }

    public class InvalidSettingOverridesException : NFigException
    {
        public IList<InvalidSettingValueException> Exceptions { get; }

        public InvalidSettingOverridesException(IList<InvalidSettingValueException> exceptions, string stackTrace) : base(GetMessage(exceptions))
        {
            Exceptions = exceptions;
            UnthrownStackTrace = stackTrace;
        }

        static string GetMessage(IList<InvalidSettingValueException> exceptions)
        {
            return $"{exceptions.Count} invalid setting overrides were not applied ({string.Join(", ", exceptions.Select(e => e.SettingName))}). You should edit or clear these overrides.";
        }
    }
}