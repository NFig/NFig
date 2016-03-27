using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace NFig
{
    public abstract class NFigStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        private readonly SettingsFactory<TSettings, TTier, TDataCenter> _factory;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="additionalDefaultConverters">
        /// Allows you to specify additional (or replacement) default converters for types. Each key/value pair must be in the form of (typeof(T), ISettingConverter&lt;T&gt;).
        /// </param>
        protected NFigStore(Dictionary<Type, object> additionalDefaultConverters = null)
        {
            _factory = new SettingsFactory<TSettings, TTier, TDataCenter>(additionalDefaultConverters);
        }

// === Async methods ===

        /// <summary>
        /// Gets a hydrated TSettings object with the correct values for the specified application, tier, and data center combination.
        /// </summary>
        public abstract Task<TSettings> GetAppSettingsAsync(string appName, TTier tier, TDataCenter dataCenter);

        /// <summary>
        /// Sets an override for the specified application, setting name, tier, and data center combination. If an existing override shares that exact
        /// combination, it will be replaced.
        /// </summary>
        public abstract Task SetOverrideAsync(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter);

        /// <summary>
        /// Clears an override with the specified application, setting name, tier, and data center combination. Even if the override does not exist, this
        /// operation may result in a change of the current commit, depending on the store's implementation.
        /// </summary>
        public abstract Task ClearOverrideAsync(string appName, string settingName, TTier tier, TDataCenter dataCenter);

        /// <summary>
        /// Returns the most current Commit ID for a given application. The commit may be null if there are no overrides set.
        /// </summary>
        public abstract Task<string> GetCurrentCommitAsync(string appName);

        /// <summary>
        /// Returns a SettingInfo object for each setting. The SettingInfo contains meta data about the setting, as well as lists of the defaults values and current overrides.
        /// </summary>
        public abstract Task<SettingInfo<TTier, TDataCenter>[]> GetAllSettingInfosAsync(string appName);

        /// <summary>
        /// Returns true if the commit ID on the settings object matches the most current commit ID.
        /// </summary>
        public virtual async Task<bool> IsCurrentAsync(TSettings settings)
        {
            var commit = await GetCurrentCommitAsync(settings.ApplicationName).ConfigureAwait(false);
            return commit == settings.Commit;
        }

// === Possibly unsafe, but sometimes necessary, synchronous methods ===

        /// <summary>
        /// Synchronous version of GetAppSettingsAsync. You should use the async version if possible because it has a lower risk of deadlocking in some circumstances.
        /// </summary>
        public virtual TSettings GetAppSettings(string appName, TTier tier, TDataCenter dataCenter)
        {
            return Task.Run(async () => await GetAppSettingsAsync(appName, tier, dataCenter)).Result;
        }

        /// <summary>
        /// Synchronous version of SetOverrideAsync. You should use the async version if possible because it has a lower risk of deadlocking in some circumstances.
        /// </summary>
        public virtual void SetOverride(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter)
        {
            Task.Run(async () => { await SetOverrideAsync(appName, settingName, value, tier, dataCenter); }).Wait();
        }

        /// <summary>
        /// Synchronous version of ClearOverrideAsync. You should use the async version if possible because it has a lower risk of deadlocking in some circumstances.
        /// </summary>
        public virtual void ClearOverride(string appName, string settingName, TTier tier, TDataCenter dataCenter)
        {
            Task.Run(async () => { await ClearOverrideAsync(appName, settingName, tier, dataCenter); }).Wait();
        }

        /// <summary>
        /// Synchronous version of GetCurrentCommitAsync. You should use the async version if possible because it has a lower risk of deadlocking in some circumstances.
        /// </summary>
        public virtual string GetCurrentCommit(string appName)
        {
            return Task.Run(async () => await GetCurrentCommitAsync(appName)).Result;
        }

        /// <summary>
        /// Synchronous version of GetAllSettingInfosAsync. You should use the async version if possible because it has a lower risk of deadlocking in some circumstances.
        /// </summary>
        public virtual SettingInfo<TTier, TDataCenter>[] GetAllSettingInfos(string appName)
        {
            return Task.Run(async () => await GetAllSettingInfosAsync(appName)).Result;
        }

        /// <summary>
        /// Synchronous version of IsCurrentAsync. You should use the async version if possible because it has a lower risk of deadlocking in some circumstances.
        /// </summary>
        public virtual bool IsCurrent(TSettings settings)
        {
            return Task.Run(async () => await IsCurrentAsync(settings)).Result;
        }

// === Safe synchronous, and non-virtual, methods ===

        /// <summary>
        /// Returns true if a setting by that name exists. For nested settings, use dot notation (i.e. "Some.Setting").
        /// </summary>
        public bool SettingExists(string settingName)
        {
            return _factory.SettingExists(settingName);
        }

        /// <summary>
        /// Returns the property type 
        /// </summary>
        /// <param name="settingName"></param>
        /// <returns></returns>
        public Type GetSettingType(string settingName)
        {
            return _factory.GetSettingType(settingName);
        }

        /// <summary>
        /// Extracts the value of a setting by name from an existing TSettings object.
        /// </summary>
        public object GetSettingValue(TSettings obj, string settingName)
        {
            return _factory.GetSettingValue(obj, settingName);
        }

        /// <summary>
        /// Extracts the value of a setting by name from an existing TSettings object.
        /// </summary>
        public TValue GetSettingValue<TValue>(TSettings obj, string settingName)
        {
            return _factory.GetSettingValue<TValue>(obj, settingName);
        }

        /// <summary>
        /// Returns true if the given string can be converted into a valid value 
        /// </summary>
        public bool IsValidStringForSetting(string settingName, string str)
        {
            return _factory.IsValidStringForSetting(settingName, str);
        }

        protected static string NewCommit()
        {
            return Guid.NewGuid().ToString();
        }

        protected SettingInfo<TTier, TDataCenter>[] GetAllSettingInfosImpl(IEnumerable<SettingValue<TTier, TDataCenter>> overrides)
        {
            return _factory.GetAllSettingInfos(overrides);
        }
    }
}