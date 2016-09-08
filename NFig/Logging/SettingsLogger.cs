using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NFig.Logging
{
    public abstract class SettingsLogger<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        readonly Action<Exception, OverridesSnapshot<TSubApp, TTier, TDataCenter>> _onLogException;

        protected SettingsLogger(Action<Exception, OverridesSnapshot<TSubApp, TTier, TDataCenter>> onLogException)
        {
            if (onLogException == null)
                throw new ArgumentNullException(nameof(onLogException));

            _onLogException = onLogException;
        }

        public void Log(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot)
        {
            Task.Run(async () =>
            {
                try
                {
                    await LogAsyncImpl(snapshot);
                }
                catch (Exception e)
                {
                    _onLogException(e, snapshot);
                }
            });
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
        public abstract Task<IEnumerable<NFigLogEvent<TDataCenter>>> GetLogsAsync(
            string globalAppName = null,
            string settingName = null,
            bool includeRestores = true,
            DateTime? minDate = null,
            DateTime? maxDate = null,
            string user = null,
            int? limit = null,
            int skip = 0);

        public abstract Task<OverridesSnapshot<TSubApp, TTier, TDataCenter>> GetSnapshotAsync(string globalAppName, string commit);

        protected abstract Task LogAsyncImpl(OverridesSnapshot<TSubApp, TTier, TDataCenter> snapshot);
    }
}