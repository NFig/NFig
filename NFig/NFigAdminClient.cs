using System;
using System.Threading.Tasks;
using NFig.Converters;
using NFig.Infrastructure;
using NFig.Metadata;

namespace NFig
{
    /// <summary>
    /// Contains all methods for administering the settings for an application, such as setting or clearing overrides.
    /// </summary>
    public class NFigAdminClient<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        readonly AppInternalInfo<TTier, TDataCenter> _appInfo;

        /// <summary>
        /// The backing store for NFig overrides and metadata.
        /// </summary>
        public NFigStore<TTier, TDataCenter> Store { get; }
        /// <summary>
        /// The name of the application to administer.
        /// </summary>
        public string AppName => _appInfo.AppName;
        /// <summary>
        /// The deployment tier of the application.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// Metadata about the application, not including default values.
        /// </summary>
        public AppMetadata AppMetadata => _appInfo.AppMetadata;
        /// <summary>
        /// True if this admin client is capable of encrypting settings for the application.
        /// </summary>
        public bool CanEncrypt => _appInfo.CanEncrypt;
        /// <summary>
        /// True if this admin client is capable of decrypting the application's encrypted settings.
        /// </summary>
        public bool CanDecrypt => _appInfo.CanDecrypt;

        /// <summary>
        /// Gets the current overrides-snapshot for the app.
        /// </summary>
        public OverridesSnapshot<TTier, TDataCenter> Snapshot => _appInfo.Snapshot;
        /// <summary>
        /// Returns the current snapshot commit for the app.
        /// </summary>
        public string Commit => Snapshot.Commit;

        internal NFigAdminClient(NFigStore<TTier, TDataCenter> store, AppInternalInfo<TTier, TDataCenter> appInfo)
        {
            _appInfo = appInfo;

            Store = store;
            Tier = store.Tier;
        }

        /// <summary>
        /// Gets the default values for a sub-app, or the root app if <paramref name="subAppId"/> is null.
        /// </summary>
        /// <param name="subAppId">The ID of the sub-app, or null for the root app.</param>
        public Defaults<TTier, TDataCenter> GetDefaults(int? subAppId)
        {
            if (subAppId == null)
            {
                var rootDefaults = _appInfo.RootDefaults;
                if (rootDefaults == null)
                    throw new NFigException("Could not find defaults for root application.");

                return rootDefaults;
            }

            if (_appInfo.SubAppDefaults.TryGetValue(subAppId.Value, out var subAppDefaults))
                return subAppDefaults;

            throw new NFigException($"Could not find defaults for sub-app {subAppId}. It may not be in use.");
        }

        /// <summary>
        /// Checks the backing store for changes to the app's metadata, including sub-apps, and updates as necessary.
        /// </summary>
        /// <param name="forceReload">If true, the metadata will be reloaded, even if no change was detected.</param>
        public void RefreshMetadata(bool forceReload) => Store.RefreshAppMetadataInternal(AppName, forceReload);

        /// <summary>
        /// Checks the backing store for changes to the app's metadata, including sub-apps, and updates as necessary.
        /// </summary>
        /// <param name="forceReload">If true, the metadata will be reloaded, even if no change was detected.</param>
        public Task RefreshMetadataAsync(bool forceReload) => Store.RefreshAppMetadataAsyncInternal(AppName, forceReload);

        /// <summary>
        /// Checks the backing store for changes to the app's overrides, including sub-apps, and updates the snapshot as necessary.
        /// </summary>
        /// <param name="forceReload">If true, the snapshot will be reloaded, even if no change was detected.</param>
        public void RefreshSnapshot(bool forceReload) => Store.RefreshSnapshotInternal(AppName, forceReload);

        /// <summary>
        /// Checks the backing store for changes to the app's overrides, including sub-apps, and updates the snapshot as necessary.
        /// </summary>
        /// <param name="forceReload">If true, the snapshot will be reloaded, even if no change was detected.</param>
        public Task RefreshSnapshotAsync(bool forceReload) => Store.RefreshSnapshotAsyncInternal(AppName, forceReload);

        /// <summary>
        /// Sets an override for the specified setting name and data center combination. If an existing override shares that exact combination, it will be
        /// replaced.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override should be applicable to.</param>
        /// <param name="value">The string-value of the setting. If the setting is an encrypted setting, this value must be pre-encrypted.</param>
        /// <param name="user">The user who is setting the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">(optional) The sub-app which the override should apply to.</param>
        /// <param name="commit">(optional) If non-null, the override will only be applied if this is the current commit ID.</param>
        /// <param name="expirationTime">(optional) The time when the override should be automatically cleared.</param>
        /// <returns>
        /// A snapshot of the state immediately after the override is applied. If the override is not applied because the current commit didn't match the
        /// commit parameter, the return value will be null.
        /// </returns>
        public OverridesSnapshot<TTier, TDataCenter> SetOverride(
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId = null,
            string commit = null,
            DateTimeOffset? expirationTime = null)
        {
            // todo: validate, if possible

            var ov = new OverrideValue<TTier, TDataCenter>(settingName, value, subAppId, dataCenter, expirationTime);
            return Store.SetOverrideInternal(AppName, ov, user, commit);
        }

        /// <summary>
        /// Sets an override for the specified setting name and data center combination. If an existing override shares that exact combination, it will be
        /// replaced.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override should be applicable to.</param>
        /// <param name="value">The string-value of the setting. If the setting is an encrypted setting, this value must be pre-encrypted.</param>
        /// <param name="user">The user who is setting the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">(optional) The sub-app which the override should apply to.</param>
        /// <param name="commit">(optional) If non-null, the override will only be applied if this is the current commit ID.</param>
        /// <param name="expirationTime">(optional) The time when the override should be automatically cleared.</param>
        /// <returns>
        /// A snapshot of the state immediately after the override is applied. If the override is not applied because the current commit didn't match the
        /// commit parameter, the return value will be null.
        /// </returns>
        public Task<OverridesSnapshot<TTier, TDataCenter>> SetOverrideAsync(
            string settingName,
            TDataCenter dataCenter,
            string value,
            string user,
            int? subAppId = null,
            string commit = null,
            DateTimeOffset? expirationTime = null)
        {
            // todo: validate, if possible

            var ov = new OverrideValue<TTier, TDataCenter>(settingName, value, subAppId, dataCenter, expirationTime);
            return Store.SetOverrideAsyncInternal(AppName, ov, user, commit);
        }

        /// <summary>
        /// Clears an override with the specified setting name and data center combination. Even if the override does not exist, this operation may result in a
        /// change of the current commit, depending on the store's implementation.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override is applied to.</param>
        /// <param name="user">The user who is clearing the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">The sub-app which the override is applied to.</param>
        /// <param name="commit">(optional) If non-null, the override will only be cleared if this is the current commit ID.</param>
        /// <returns>
        /// A snapshot of the state immediately after the override is cleared. If the override is not applied, either because it didn't exist, or because the
        /// current commit didn't match the commit parameter, the return value will be null.
        /// </returns>
        public OverridesSnapshot<TTier, TDataCenter> ClearOverride(
            string settingName,
            TDataCenter dataCenter,
            string user,
            int? subAppId = null,
            string commit = null)
        {
            // todo: validate setting name

            return Store.ClearOverrideInternal(AppName, settingName, dataCenter, user, subAppId, commit);
        }

        /// <summary>
        /// Clears an override with the specified setting name and data center combination. Even if the override does not exist, this operation may result in a
        /// change of the current commit, depending on the store's implementation.
        /// </summary>
        /// <param name="settingName">Name of the setting.</param>
        /// <param name="dataCenter">Data center which the override is applied to.</param>
        /// <param name="user">The user who is clearing the override (used for logging purposes). This can be null if you don't care about logging.</param>
        /// <param name="subAppId">The sub-app which the override is applied to.</param>
        /// <param name="commit">(optional) If non-null, the override will only be cleared if this is the current commit ID.</param>
        /// <returns>
        /// A snapshot of the state immediately after the override is cleared. If the override is not applied, either because it didn't exist, or because the
        /// current commit didn't match the commit parameter, the return value will be null.
        /// </returns>
        public Task<OverridesSnapshot<TTier, TDataCenter>> ClearOverrideAsync(
            string settingName,
            TDataCenter dataCenter,
            string user,
            int? subAppId = null,
            string commit = null)
        {
            // todo: validate setting name

            return Store.ClearOverrideAsyncInternal(AppName, settingName, dataCenter, user, subAppId, commit);
        }

        /// <summary>
        /// Returns an encrypted version of <paramref name="plainText"/>. If this admin client was not provided with an encryptor for the application, then
        /// <see cref="CanEncrypt"/> will be false, and this method will always throw an exception. Null values are not encrypted, and are simply returned as
        /// null.
        /// </summary>
        public string Encrypt(string plainText) => _appInfo.Encrypt(plainText);

        /// <summary>
        /// Returns a plain-text (decrypted) version of <paramref name="encrypted"/>. If this admin client is not able to decrypt settings for the application, 
        /// then <see cref="CanDecrypt"/> will be false, and this method will always throw an exception. Null are considered to be unencrypted to begin with,
        /// and will result in a null return value.
        /// </summary>
        public string Decrypt(string encrypted) => _appInfo.Decrypt(encrypted);

        /// <summary>
        /// Restores all overrides to a previous state.
        /// </summary>
        /// <param name="snapshot">The snapshot to restore.</param>
        /// <param name="user">The user performing the restore (for logging purposes).</param>
        /// <returns>A snapshot of the new current state (after restoring).</returns>
        public OverridesSnapshot<TTier, TDataCenter> RestoreSnapshot(OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            return Store.RestoreSnapshotInternal(AppName, snapshot, user);
        }

        /// <summary>
        /// Restores all overrides to a previous state.
        /// </summary>
        /// <param name="snapshot">The snapshot to restore.</param>
        /// <param name="user">The user performing the restore (for logging purposes).</param>
        /// <returns>A snapshot of the new current state (after restoring).</returns>
        public Task<OverridesSnapshot<TTier, TDataCenter>> RestoreSnapshotAsync(OverridesSnapshot<TTier, TDataCenter> snapshot, string user)
        {
            return Store.RestoreSnapshotAsyncInternal(AppName, snapshot, user);
        }

        /// <summary>
        /// Returns true if this admin client is capable of validating values for the setting. This may be false if the setting uses a custom
        /// <see cref="ISettingConverter{TValue}"/>, and no <see cref="NFigAppClient{TSettings,TTier,TDataCenter}"/> has been instantiated for the application.
        /// </summary>
        public bool CanValidate(string settingName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns true if <paramref name="value"/> can be converted into a valid value for the setting. If the setting is encrypted, then
        /// <paramref name="value"/> must be encrypted. If <see cref="CanValidate"/> returns false for this setting name, then this method will always throw an
        /// exception.
        /// </summary>
        public bool IsValidForSetting(string settingName, string value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns true if the setting exists. Setting names are case-sensitive.
        /// </summary>
        public bool SettingExists(string settingName)
        {
            return _appInfo.AppMetadata.SettingsMetadata.ContainsKey(settingName);
        }
    }
}