using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NFig.Converters;
using NFig.Encryption;

namespace NFig
{
    class AppInternalInfo
    {
        internal string AppName { get; }
        [CanBeNull]
        internal Type SettingsType { get; set; }
        [CanBeNull]
        internal Dictionary<Type, ISettingConverter> Converters { get; set; }
        [CanBeNull]
        internal ISettingEncryptor Encryptor { get; set; }

        internal AppInternalInfo(string appName, Type settingsType)
        {
            AppName = appName;
            SettingsType = settingsType;
        }
    }
}