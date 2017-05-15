using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace NFig
{
    /// <summary>
    /// Represents the state of all overrides, and the last event, for an application.
    /// </summary>
    public class OverridesSnapshot<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        /// <summary>
        /// The application name.
        /// </summary>
        [NotNull]
        public string AppName { get; }
        /// <summary>
        /// The commit ID at the time the snapshot was taken.
        /// </summary>
        [NotNull]
        public string Commit { get; }
        /// <summary>
        /// A list of overrides which existed at the time the snapshot was taken.
        /// </summary>
        [CanBeNull]
        public IList<OverrideValue<TTier, TDataCenter>> Overrides { get; }

        /// <summary>
        /// Initializes a new snapshot object.
        /// </summary>
        /// <param name="appName">The application name.</param>
        /// <param name="commit">The commit ID at the time of the snapshot.</param>
        /// <param name="overrides">A list of the overrides which exist at the time of the snapshot.</param>
        public OverridesSnapshot([NotNull] string appName, [NotNull] string commit, IList<OverrideValue<TTier, TDataCenter>> overrides)
        {
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            Commit = commit ?? throw new ArgumentNullException(nameof(commit));
            Overrides = overrides;
        }
    }
}