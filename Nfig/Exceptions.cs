using System;

namespace Nfig
{
    public class NfigException : Exception
    {
        public NfigException (string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }

    public class SettingConversionException : NfigException
    {
        public SettingConversionException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }
}