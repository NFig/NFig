using System;

namespace NFig
{
    public class SettingInfo
    {
        public string Key { get; internal set; }
        public string ActiveStringValue { get; internal set; }
        public string DefaultStringValue { get; internal set; }

        public bool HasOverride { get; internal set; }

        public string GetOverride<TTier, TDataCenter>(TTier? tier, TDataCenter? dataCenter)
            where TTier : struct
            where TDataCenter : struct
        {
            throw new NotImplementedException();
        }
    }
}