using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NFig
{
    /// <summary>
    /// An in-memory NFig store. This store is primarily intended for testing and sample apps, but could be used for an app with no persistent backing store.
    /// </summary>
    /// <typeparam name="TTier">The enum type used to represent the deployment tier.</typeparam>
    /// <typeparam name="TDataCenter">The enum type used to represent the data center.</typeparam>
    public class NFigMemoryStore<TTier, TDataCenter> : NFigStore<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        readonly Dictionary<string, App> _apps = new Dictionary<string, App>();

        /// <summary>
        /// Creates an in-memory NFig store. This store is primarily intended for testing and sample apps, but could be used for an app with no persistent
        /// backing store.
        /// </summary>
        /// <param name="tier">The deployment tier of the store.</param>
        /// <param name="dataCenter">The current data center.</param>
        public NFigMemoryStore(TTier tier, TDataCenter dataCenter)
            : base(tier, dataCenter)
        {
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        protected override IEnumerable<SubAppInfo> GetSubApps(string appName)
        {
            throw new NotImplementedException();
        }

        protected override Task<IEnumerable<SubAppInfo>> GetSubAppsAsync(string appName)
        {
            throw new NotImplementedException();
        }

        protected override OverridesSnapshot<TTier, TDataCenter> GetSnapshot(string appName)
        {
            throw new System.NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> GetSnapshotAsync(string appName)
        {
            throw new System.NotImplementedException();
        }

        protected override OverridesSnapshot<TTier, TDataCenter> RestoreSnapshot(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> RestoreSnapshotAsync(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            throw new NotImplementedException();
        }

        protected override OverridesSnapshot<TTier, TDataCenter> SetOverride(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId,
            string commit,
            DateTimeOffset? expirationTime)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsync(
            string appName,
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId,
            string commit,
            DateTimeOffset? expirationTime)
        {
            throw new NotImplementedException();
        }

        protected override OverridesSnapshot<TTier, TDataCenter> ClearOverride(string appName, string settingName, TDataCenter dataCenter, string user, int? subAppId, string commit)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> ClearOverrideAsync(string appName, string settingName, TDataCenter dataCenter, string user, int? subAppId, string commit)
        {
            throw new NotImplementedException();
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        // This class is essentially designed to mimic how NFig.Redis is intended to work. We could design an in-memory store which was a bit more effecient,
        // but it wouldn't be as good of a test platform. The most common use cases for this memory store are: testing, sample apps, and apps where the settings
        // don't change. None of those need anything more effecient.
        class App
        {
            public string AppName { get; }
            public string Commit { get; set; }
            public Dictionary<string, string> Overrides { get; } = new Dictionary<string, string>();
        }
    }
}