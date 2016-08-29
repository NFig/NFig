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
        readonly Action<Exception, AppSnapshot<TSubApp, TTier, TDataCenter>> _onLogException;

        protected SettingsLogger(Action<Exception, AppSnapshot<TSubApp, TTier, TDataCenter>> onLogException)
        {
            if (onLogException == null)
                throw new ArgumentNullException(nameof(onLogException));

            _onLogException = onLogException;
        }

        public void Log(AppSnapshot<TSubApp, TTier, TDataCenter> snapshot)
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
        /// <param name="appName">Application name to filter on. Null to include all applications.</param>
        /// <param name="settingName">Setting name to filter on. Null to include all settings.</param>
        /// <param name="includeRestores">If true, restore events will be included, even when filtering by setting name.</param>
        /// <param name="minDate">Minimum (inclusive) event date to include in results.</param>
        /// <param name="maxDate">Maximum (exclusive) event date to include in results.</param>
        /// <param name="user">User to filter on. Null to include events by all users.</param>
        /// <param name="limit">Maximum number of results to return.</param>
        /// <param name="skip">Number of results to skip - useful for pagenation.</param>
        /// <returns></returns>
        public abstract Task<IEnumerable<NFigLogEvent<TDataCenter>>> GetLogsAsync(
            string appName = null,
            string settingName = null,
            bool includeRestores = true,
            DateTime? minDate = null,
            DateTime? maxDate = null,
            string user = null,
            int? limit = null,
            int skip = 0);

        public abstract Task<AppSnapshot<TSubApp, TTier, TDataCenter>> GetSnapshotAsync(string appName, string commit);

        protected abstract Task LogAsyncImpl(AppSnapshot<TSubApp, TTier, TDataCenter> snapshot);
    }
}