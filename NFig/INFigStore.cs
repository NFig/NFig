using System.Collections.Generic;

namespace NFig
{
    public interface INFigStore<TSettings>
    {
        IReadOnlyList<string> SettingKeys { get; }

        TSettings GetAppSettings();
        TSettings GetSubAppSettings(string subAppName);
        void SetAppSetting(string key, string value);
        void SetSubAppSetting(string subAppName, string key, string value);

        IEnumerable<SettingInfo> GetAppSettingsInfo();
        IEnumerable<SettingInfo> GetSubAppSettingsInfo(string subAppName);
    }
}