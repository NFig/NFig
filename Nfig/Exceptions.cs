using System;

namespace NFig
{
    public class NFigException : Exception
    {
        public NFigException (string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }

    public class SettingConversionException : NFigException
    {
        public SettingConversionException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }
}