using System.Collections.Generic;
using NFig.Logging;

namespace NFig
{
    public class AppSnapshot<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        public string ApplicationName { get; set; }
        public string Commit { get; set; }
        public IList<SettingValue<TSubApp, TTier, TDataCenter>> Overrides { get; set; }
        public NFigLogEvent<TDataCenter> LastEvent { get; set; }
    }
}