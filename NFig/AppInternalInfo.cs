using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NFig.Converters;

namespace NFig
{
    class AppInternalInfo
    {
        static readonly Dictionary<string, AppInternalInfo> s_infoByApp = new Dictionary<string, AppInternalInfo>();

        internal string AppName { get; }
        [CanBeNull]
        internal Type SettingsType { get; private set; }
        [CanBeNull]
        internal Dictionary<Type, ISettingConverter> Converters { get; private set; }

        AppInternalInfo(string appName, Type settingsType)
        {
            AppName = appName;
            SettingsType = settingsType;
        }

        internal static AppInternalInfo GetAppInfo(string appName, Type settingsType = null)
        {
            if (appName == null)
                throw new ArgumentNullException(nameof(appName));

            lock (s_infoByApp)
            {
                AppInternalInfo info;
                if (s_infoByApp.TryGetValue(appName, out info))
                {
                    if (settingsType != null) // called from an AppClient
                    {
                        if (info.SettingsType == null)
                        {
                            // This is the first time an app client has been setup for this particular app. We'll just blindly trust that they picked the right TSettings.
                            info.SettingsType = settingsType;
                        }
                        else if (settingsType != info.SettingsType)
                        {
                            // there is a mismatch between 
                            var ex = new NFigException($"The TSettings of app \"{appName}\" does not match the TSettings used when the first NFigAppClient was initialized for the app");
                            ex.Data["OriginalTSettings"] = info.SettingsType.FullName;
                            ex.Data["NewTSettings"] = settingsType.FullName;
                            throw ex;
                        }
                    }

                    return info;
                }

                info = new AppInternalInfo(appName, settingsType);
                s_infoByApp[appName] = info;
                return info;
            }
        }

        internal void SetConverters(Dictionary<Type, ISettingConverter> converters)
        {
            Converters = converters;
        }
    }
}