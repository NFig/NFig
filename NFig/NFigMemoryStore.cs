using System;
using System.Threading.Tasks;
using NFig.Metadata;

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
        const string ROOT_KEY = "$root";

        NFigMemoryStore(TTier tier, TDataCenter dataCenter, Action<Exception> backgroundExceptionHandler)
            : base(tier, dataCenter, backgroundExceptionHandler)
        {
        }

        /// <summary>
        /// Creates an in-memory NFig store. This store is primarily intended for testing and sample apps, but could be used for an app with no persistent
        /// backing store.
        /// </summary>
        /// <param name="tier">The deployment tier of the store.</param>
        /// <param name="dataCenter">The current data center.</param>
        /// <param name="backgroundExceptionHandler">Used to log exceptions which occur on a background thread.</param>
        public NFigMemoryStore<TTier, TDataCenter> Create(TTier tier, TDataCenter dataCenter, Action<Exception> backgroundExceptionHandler)
        {
            var store = new NFigMemoryStore<TTier, TDataCenter>(tier, dataCenter, backgroundExceptionHandler);
            store.RefreshAppNames();
            return store;
        }

        // If this were a real store, there should be an async version of Create, because it needs to load all of the app names from the backing store.

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        protected override string[] GetAppNames()
        {
            throw new NotImplementedException();
        }

        protected override Task<string[]> GetAppNamesAsync()
        {
            throw new NotImplementedException();
        }

        protected override void RefreshAppMetadata(string appName, bool forceReload)
        {
            throw new NotImplementedException();
        }

        protected override Task RefreshAppMetadataAsync(string appName, bool forceReload)
        {
            throw new NotImplementedException();
        }

        protected override void RefreshSnapshot(string appName, bool forceReload)
        {
            throw new NotImplementedException();
        }

        protected override Task RefreshSnapshotAsync(string appName, bool forceReload)
        {
            throw new NotImplementedException();
        }

        protected override OverridesSnapshot<TTier, TDataCenter> RestoreSnapshot(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> RestoreSnapshotAsync(string appName, OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            throw new NotImplementedException();
        }

        protected override OverridesSnapshot<TTier, TDataCenter> SetOverride(string appName, OverrideValue<TTier, TDataCenter> ov, string user, string commit)
        {
            throw new NotImplementedException();
        }

        protected override Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsync(string appName, OverrideValue<TTier, TDataCenter> ov, string user, string commit)
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

        protected override void SetMetadata(string appName, BySetting<SettingMetadata> metadata)
        {
            throw new NotImplementedException();
        }

        protected override Task SetMetadataAsync(string appName, BySetting<SettingMetadata> metadata)
        {
            throw new NotImplementedException();
        }

        protected override void UpdateSubAppMetadata(string appName, SubAppMetadata<TTier, TDataCenter>[] subAppsMetadata)
        {
            throw new NotImplementedException();
        }

        protected override Task UpdateSubAppMetadataAsync(string appName, SubAppMetadata<TTier, TDataCenter>[] subAppsMetadata)
        {
            throw new NotImplementedException();
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}