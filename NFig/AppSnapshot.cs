using System.Collections.Generic;

namespace NFig
{
    public class AppSnapshot<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        public string ApplicationName { get; set; }
        public string Commit { get; set; }
        public IList<SettingValue<TTier, TDataCenter>> Overrides { get; set; }
        public NFigLogEvent<TDataCenter> LastEvent { get; set; }
    }
}