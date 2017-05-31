using NFig.Encryption;

namespace NFig.Tests.Common
{
    static class Utils
    {
        public const string GLOBAL_APP_1 = "GLOBAL_APP_1";
        public const string GLOBAL_APP_2 = "GLOBAL_APP_2";

        public static SettingsFactory<TSettings, Tier, DataCenter> CreateFactory<TSettings>(
            string globalAppName = GLOBAL_APP_1,
            Tier tier = Tier.Local,
            DataCenter dataCenter = DataCenter.Local,
            ISettingEncryptor encryptor = null)
            where TSettings : class, INFigSettings<Tier, DataCenter>, new()
        {
            var appInfo = new AppInternalInfo(GLOBAL_APP_1, typeof(TSettings));
            appInfo.Encryptor = encryptor;
            return new SettingsFactory<TSettings, Tier, DataCenter>(appInfo, tier, dataCenter);
        }

        public static OverridesSnapshot<Tier, DataCenter> CreateSnapshot(
            string globalAppName = GLOBAL_APP_1,
            string commit = "00", // todo: use real initial commit
            ListBySetting<OverrideValue<Tier, DataCenter>> overrides = null)
        {
            return new OverridesSnapshot<Tier, DataCenter>(globalAppName, commit, overrides);
        }

        public static TSettings GetSettings<TSettings>(
            this SettingsFactory<TSettings, Tier, DataCenter> factory,
            int? subAppId = null,
            string subAppName = null,
            OverridesSnapshot<Tier, DataCenter> snapshot = null)
            where TSettings : class, INFigSettings<Tier, DataCenter>, new()
        {
            if (snapshot == null)
                snapshot = CreateSnapshot(factory.AppInfo.AppName);

            var ex = factory.TryGetSettings(subAppId, subAppName, snapshot, out var settings);
            if (ex != null)
                throw ex;

            return settings;
        }
    }
}