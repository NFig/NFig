using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NFig.Logging;

namespace NFig
{
    public class OverridesSnapshot<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        [NotNull]
        public string GlobalAppName { get; }
        [NotNull]
        public string Commit { get; }
        [CanBeNull]
        public IList<SettingValue<TSubApp, TTier, TDataCenter>> Overrides { get; }
        [CanBeNull]
        public NFigLogEvent<TDataCenter> LastEvent { get; }

        public OverridesSnapshot(
            [NotNull] string globalAppName,
            [NotNull] string commit,
            IList<SettingValue<TSubApp, TTier, TDataCenter>> overrides,
            NFigLogEvent<TDataCenter> lastEvent)
        {
            if (globalAppName == null)
                throw new ArgumentNullException(nameof(globalAppName));

            if (commit == null)
                throw new ArgumentNullException(nameof(commit));

            GlobalAppName = globalAppName;
            Commit = commit;
            Overrides = overrides;
            LastEvent = lastEvent;
        }
    }
}