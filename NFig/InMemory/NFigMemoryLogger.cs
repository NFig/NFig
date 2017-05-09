using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NFig.Logging;

namespace NFig.InMemory
{
    /// <summary>
    /// An in-memory settings logger.
    /// </summary>
    public class NFigMemoryLogger<TSubApp, TTier, TDataCenter> : SettingsLogger<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        readonly List<OverridesSnapshot<TSubApp, TTier, TDataCenter>> _history = new List<OverridesSnapshot<TSubApp, TTier, TDataCenter>>();

        /// <summary>
        /// Initializes a new in-memory settings logger. This class is primarily for testing purposes. Is has no persistence.
        /// </summary>
        /// <param name="onLogException">
        /// Since events are logged asynchronously, all exceptions will be sent to this callback.
        /// </param>
        public NFigMemoryLogger(Action<Exception, OverridesSnapshot<TSubApp, TTier, TDataCenter>> onLogException) : base(onLogException)
        {
        }

        /// <summary>
        /// Logging implementation.
        /// </summary>
        protected override Task LogAsyncImpl(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot)
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

        /// <summary>
        /// Gets log entries filtered by the provided criteria. Results are always returned in descending chronological order.
        /// </summary>
        /// <param name="globalAppName">Application name to filter on. Null to include all applications.</param>
        /// <param name="settingName">Setting name to filter on. Null to include all settings.</param>
        /// <param name="includeRestores">If true, restore events will be included, even when filtering by setting name.</param>
        /// <param name="minDate">Minimum (inclusive) event date to include in results.</param>
        /// <param name="maxDate">Maximum (exclusive) event date to include in results.</param>
        /// <param name="user">User to filter on. Null to include events by all users.</param>
        /// <param name="limit">Maximum number of results to return.</param>
        /// <param name="skip">Number of results to skip - useful for pagination.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Retrieves a saved snapshot. Should return null if no matching snapshot is found.
        /// </summary>
        public override Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> GetSnapshotAsync(string globalAppName, string commit)
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

            return Task.FromResult<OverridesSnapshot<TSubApp, TTier, TDataCenter>>(null);
        }

        /// <summary>
        /// Actual implementation for logging. Must be provided by inheriting classes.
        /// </summary>
        static int CompareLogs(OverridesSnapshot<TSubApp, TTier, TDataCenter> a, OverridesSnapshot<TSubApp, TTier, TDataCenter> b)
        {
            if (a.LastEvent.Timestamp > b.LastEvent.Timestamp)
                return 1;

            if (a.LastEvent.Timestamp == b.LastEvent.Timestamp)
                return 0;

            return -1;
        }
    }
}