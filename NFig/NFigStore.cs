using System.Threading.Tasks;

namespace NFig
{
    public abstract class NFigStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        protected SettingsFactory<TSettings, TTier, TDataCenter> Factory { get; }

        protected NFigStore(SettingsFactory<TSettings, TTier, TDataCenter> factory)
        {
            Factory = factory;
        }

        public abstract TSettings GetApplicationSettings(string appName, TTier tier, TDataCenter dataCenter);
        public abstract void SetOverride(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter);
        public abstract void ClearOverride(string appName, string settingName, TTier tier, TDataCenter dataCenter);
        public abstract string GetCurrentCommit(string appName);
        public abstract SettingInfo<TTier, TDataCenter>[] GetAllSettingInfos(string appName);
        public abstract SettingInfo<TTier, TDataCenter> GetSettingInfo(string appName, string settingName);

        public virtual bool IsCurrent(TSettings settings)
        {
            var commit = GetCurrentCommit(settings.ApplicationName);
            return commit == settings.Commit;
        }

        public virtual bool SettingExists(string settingName)
        {
            return Factory.SettingExists(settingName);
        }

        public virtual bool IsValidStringForSetting(string settingName, string str)
        {
            return Factory.IsValidStringForSetting(settingName, str);
        }
    }

    public abstract class NFigAsyncStore<TSettings, TTier, TDataCenter> : NFigStore<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        protected NFigAsyncStore(SettingsFactory<TSettings, TTier, TDataCenter> factory) : base(factory) { }

        public abstract Task<TSettings> GetApplicationSettingsAsync(string appName, TTier tier, TDataCenter dataCenter);
        public abstract Task SetOverrideAsync(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter);
        public abstract Task ClearOverrideAsync(string appName, string settingName, TTier tier, TDataCenter dataCenter);
        public abstract Task<string> GetCurrentCommitAsync(string appName);
        public abstract Task<SettingInfo<TTier, TDataCenter>[]> GetAllSettingInfosAsync(string appName);
        public abstract Task<SettingInfo<TTier, TDataCenter>> GetSettingInfoAsync(string appName, string settingName);

        public override TSettings GetApplicationSettings(string appName, TTier tier, TDataCenter dataCenter)
        {
            return Task.Run(async () => { return await GetApplicationSettingsAsync(appName, tier, dataCenter); }).Result;
        }

        public override void SetOverride(string appName, string settingName, string value, TTier tier, TDataCenter dataCenter)
        {
            Task.Run(async () => { await SetOverrideAsync(appName, settingName, value, tier, dataCenter); }).Wait();
        }

        public override void ClearOverride(string appName, string settingName, TTier tier, TDataCenter dataCenter)
        {
            Task.Run(async () => { await ClearOverrideAsync(appName, settingName, tier, dataCenter); }).Wait();
        }

        public override string GetCurrentCommit(string appName)
        {
            return Task.Run(async () => { return await GetCurrentCommitAsync(appName); }).Result;
        }

        public override SettingInfo<TTier, TDataCenter>[] GetAllSettingInfos(string appName)
        {
            return Task.Run(async () => { return await GetAllSettingInfosAsync(appName); }).Result;
        }

        public override SettingInfo<TTier, TDataCenter> GetSettingInfo(string appName, string settingName)
        {
            return Task.Run(async () => { return await GetSettingInfoAsync(appName, settingName); }).Result;
        }

        public virtual async Task<bool> IsCurrentAsync(TSettings settings)
        {
            var commit = await GetCurrentCommitAsync(settings.ApplicationName).ConfigureAwait(false);
            return commit == settings.Commit;
        }
    }
}