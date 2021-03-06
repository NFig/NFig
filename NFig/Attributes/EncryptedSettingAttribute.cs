﻿using System;

namespace NFig
{
    /// <summary>
    /// Used to mark a property as an encrypted setting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EncryptedSettingAttribute : SettingAttribute
    {
        /// <summary>
        /// Marks a property as an encrypted NFig Setting. For an unencrypted setting, use <see cref="SettingAttribute"/> instead. Encrypted settings do not
        /// allow a (SubApp=All, Tier=Any, DataCenter=Any) default, other than the default value for the setting's type (default(T)).
        /// </summary>
        public EncryptedSettingAttribute() : base(null)
        {
            IsEncrypted = true;
        }
    }
}