using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NFig
{
    public class MemoryLogger<TTier, TDataCenter> : SettingsLogger<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        private readonly List<AppSnapshot<TTier, TDataCenter>> _history = new List<AppSnapshot<TTier, TDataCenter>>();

        public MemoryLogger(Action<Exception, AppSnapshot<TTier, TDataCenter>> onLogException) : base(onLogException)
        {
        }

        protected override Task LogAsyncImpl(AppSnapshot<TTier, TDataCenter> snapshot)
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
            string appName = null,
            string settingName = null,
            bool includeRestores = true,
            DateTime? minDate = null,
            DateTime? maxDate = null,
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

                    if (appName != null && log.ApplicationName != appName)
                        continue;

                    if (log.Type == NFigLogEventType.RestoreSnapshot && !includeRestores)
                        continue;

                    if (settingName != null && log.SettingName != settingName && log.Type != NFigLogEventType.RestoreSnapshot)
                        continue;

                    if (minDate.HasValue && log.Timestamp < minDate.Value)
                        continue;

                    if (maxDate.HasValue && log.Timestamp >= maxDate.Value)
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

        public override Task<AppSnapshot<TTier, TDataCenter>> GetSnapshotAsync(string appName, string commit)
        {
            lock (_history)
            {
                for (var i = _history.Count - 1; i >= 0; i--)
                {
                    var snap = _history[i];
                    if (snap.Commit == commit && snap.ApplicationName == appName)
                    {
                        // a better implementation would return a copy so that the caller can't change history, but this MemoryLogger is really just for sample
                        // applications - not real world use.
                        return Task.FromResult(snap);
                    }
                }
            }

            return Task.FromResult<AppSnapshot<TTier, TDataCenter>>(null);
        }

        private static int CompareLogs(AppSnapshot<TTier, TDataCenter> a, AppSnapshot<TTier, TDataCenter> b)
        {
            if (a.LastEvent.Timestamp > b.LastEvent.Timestamp)
                return 1;

            if (a.LastEvent.Timestamp == b.LastEvent.Timestamp)
                return 0;

            return -1;
        }
    }
}