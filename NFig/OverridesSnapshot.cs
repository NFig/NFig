using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NFig.Logging;

namespace NFig
{
    /// <summary>
    /// Represents the state of all overrides, and the last event, for an application.
    /// </summary>
    public class OverridesSnapshot<TSubApp, TTier, TDataCenter>
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// The "Global" application name.
        /// </summary>
        [NotNull]
        public string GlobalAppName { get; }
        /// <summary>
        /// The commit ID at the time the snapshot was taken.
        /// </summary>
        [NotNull]
        public string Commit { get; }
        /// <summary>
        /// A list of overrides which existed at the time the snapshot was taken.
        /// </summary>
        [CanBeNull]
        public IList<OverrideValue<TSubApp, TTier, TDataCenter>> Overrides { get; }
        /// <summary>
        /// The last event which had occurred prior to taking the snapshot.
        /// </summary>
        [CanBeNull]
        public NFigLogEvent<TDataCenter> LastEvent { get; }

        /// <summary>
        /// Initializes a new snapshot object.
        /// </summary>
        /// <param name="globalAppName">The "Global" application name.</param>
        /// <param name="commit">The commit ID at the time of the snapshot.</param>
        /// <param name="overrides">A list of the overrides which exist at the time of the snapshot.</param>
        /// <param name="lastEvent">The last event which occurred before the snapshot.</param>
        public OverridesSnapshot(
            [NotNull] string globalAppName,
            [NotNull] string commit,
            IList<OverrideValue<TSubApp, TTier, TDataCenter>> overrides,
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