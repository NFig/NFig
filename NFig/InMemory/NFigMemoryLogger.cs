using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NFig.Logging;

namespace NFig.InMemory
{
    public class NFigMemoryLogger<TSubApp, TTier, TDataCenter> : SettingsLogger<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        readonly List<AppSnapshot<TSubApp, TTier, TDataCenter>> _history = new List<AppSnapshot<TSubApp, TTier, TDataCenter>>();

        public NFigMemoryLogger(Action<Exception, AppSnapshot<TSubApp, TTier, TDataCenter>> onLogException) : base(onLogException)
        {
        }

        protected override Task LogAsyncImpl(AppSnapshot<TSubApp, TTier, TDataCenter> snapshot)
        {
            lock (_history)
            {
                var needsResort = _history.Count > 0 && snapshot.LastEvent.Timestamp < _history[_history.Count - 1].LastEvent.Timestamp;
                _history.Add(snapshot);

                if (needsResort)
                    _history.Sort(CompareLogs);
            }

            return Task.FromResult(0);
        }

        public override Task<IEnumerable<NFigLogEvent<TDataCenter>>> GetLogsAsync(
            string globalAppName = null,
            string settingName = null,
            bool includeRestores = true,
            DateTime? minDate = null,
            DateTime? maxDate = null,
            string user = null,
            int? limit = null,
            int skip = 0)
        {
            var list = new List<NFigLogEvent<TDataCenter>>();
            var skipped = 0;

            lock (_history)
            {
                for (var i = _history.Count - 1; i >= 0; i--)
                {
                    if (limit.HasValue && list.Count >= limit.Value)
                        break;

                    var log = _history[i].LastEvent;

                    if (globalAppName != null && log.GlobalAppName != globalAppName)
                        continue;

                    if (log.Type == NFigLogEventType.RestoreSnapshot && !includeRestores)
                        continue;

                    if (settingName != null && log.SettingName != settingName && log.Type != NFigLogEventType.RestoreSnapshot)
                        continue;

                    if (minDate.HasValue && log.Timestamp < minDate.Value)
                        continue;

                    if (maxDate.HasValue && log.Timestamp >= maxDate.Value)
                        continue;

                    if (user != null && log.User != user)
                        continue;

                    if (skipped < skip)
                    {
                        skipped++;
                        continue;
                    }

                    // we've got an entry that's relevant
                    list.Add(log);
                }
            }

            return Task.FromResult<IEnumerable<NFigLogEvent<TDataCenter>>>(list);
        }

        public override Task<AppSnapshot<TSubApp, TTier, TDataCenter>> GetSnapshotAsync(string globalAppName, string commit)
        {
            lock (_history)
            {
                for (var i = _history.Count - 1; i >= 0; i--)
                {
                    var snap = _history[i];
                    if (snap.Commit == commit && snap.GlobalAppName == globalAppName)
                    {
                        // a better implementation would return a copy so that the caller can't change history, but this MemoryLogger is really just for sample
                        // applications - not real world use.
                        return Task.FromResult(snap);
                    }
                }
            }

            return Task.FromResult<AppSnapshot<TSubApp, TTier, TDataCenter>>(null);
        }

        static int CompareLogs(AppSnapshot<TSubApp, TTier, TDataCenter> a, AppSnapshot<TSubApp, TTier, TDataCenter> b)
        {
            if (a.LastEvent.Timestamp > b.LastEvent.Timestamp)
                return 1;

            if (a.LastEvent.Timestamp == b.LastEvent.Timestamp)
                return 0;

            return -1;
        }
    }
}