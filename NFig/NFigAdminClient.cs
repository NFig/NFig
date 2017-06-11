using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NFig.Converters;
using NFig.Encryption;
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
        readonly ISettingEncryptor _encryptor;

        /// <summary>
        /// The backing store for NFig overrides and metadata.
        /// </summary>
        public NFigStore<TTier, TDataCenter> Store { get; }
        /// <summary>
        /// The name of the application to administer.
        /// </summary>
        public string AppName { get; }
        /// <summary>
        /// The deployment tier of the application.
        /// </summary>
        public TTier Tier { get; }
        /// <summary>
        /// True if this admin client is capable of encrypting settings for the application.
        /// </summary>
        public bool CanEncrypt { get; }
        /// <summary>
        /// True if this admin client is capable of decrypting the application's encrypted settings.
        /// </summary>
        public bool CanDecrypt { get; }

        /// <summary>
        /// Initializes the admin client.
        /// </summary>
        protected internal NFigAdminClient(NFigStore<TTier, TDataCenter> store, string appName, TTier tier, ISettingEncryptor encryptor)
        {
            _encryptor = encryptor;

            Store = store;
            AppName = appName;
            Tier = tier;
            CanEncrypt = encryptor != null;
            CanDecrypt = encryptor?.CanDecrypt ?? false;
        }

        /// <summary>
        /// Gets the name and ID of every sub-app that has been added to this application.
        /// </summary>
        public IEnumerable<SubApp> GetSubApps() // todo: use a concrete type rather than IEnumerable
        {
            return Store.GetSubAppsInternal(AppName);
        }

        /// <summary>
        /// Gets the name and ID of every sub-app that has been added to this application.
        /// </summary>
        public Task<IEnumerable<SubApp>> GetSubAppsAsync() // todo: use a concrete type rather than IEnumerable
        {
            return Store.GetSubAppsAsyncInternal(AppName);
        }

        // todo: GetMetadata

        /// <summary>
        /// Gets the current commit ID of the application. This changes every time overrides are updated.
        /// </summary>
        public string GetCurrentCommit()
        {
            var snapshot = Store.GetSnapshotInternal(AppName);
            return snapshot.Commit;
        }

        /// <summary>
        /// Asynchronously gets the current commit ID of the application. This changes every time overrides are updated.
        /// </summary>
        public async Task<string> GetCurrentCommitAsync()
        {
            var snapshot = await Store.GetSnapshotAsyncInternal(AppName);
            return snapshot.Commit;
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
        public string Encrypt(string plainText)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a plain-text (decrypted) version of <paramref name="encrypted"/>. If this admin client is not able to decrypt settings for the application, 
        /// then <see cref="CanDecrypt"/> will be false, and this method will always throw an exception. Null are considered to be unencrypted to begin with,
        /// and will result in a null return value.
        /// </summary>
        public string Decrypt(string encrypted)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a snapshot of all current overrides which can be used to restore the current state at a later date.
        /// </summary>
        public OverridesSnapshot<TTier, TDataCenter> GetSnapshot() => Store.GetSnapshotInternal(AppName);

        /// <summary>
        /// Returns a snapshot of all current overrides which can be used to restore the current state at a later date.
        /// </summary>
        public Task<OverridesSnapshot<TTier, TDataCenter>> GetSnapshotAsync() => Store.GetSnapshotAsyncInternal(AppName);

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
    }
}