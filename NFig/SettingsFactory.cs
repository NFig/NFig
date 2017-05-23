using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using NFig.Converters;
using NFig.Encryption;

namespace NFig
{
    class SettingsFactory<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        readonly Type _settingsType;
        readonly Type _tierType;
        readonly Type _dataCenterType;
        readonly ISettingEncryptor _encryptor;
        readonly ReflectionCache _reflectionCache;

        internal AppInternalInfo AppInfo { get; }
        internal TTier Tier { get; }
        internal TDataCenter DataCenter { get; }

        internal SettingsFactory(AppInternalInfo appInfo, TTier tier, TDataCenter dataCenter)
        {
            _settingsType = typeof(TSettings);
            _tierType = typeof(TTier);
            _dataCenterType = typeof(TDataCenter);
            _encryptor = appInfo.Encryptor;

            AppInfo = appInfo;
            Tier = tier;
            DataCenter = dataCenter;

            AssertEncryptorIsNullOrValid();

            _reflectionCache = CreateReflectionCache();

            var tree = GetSettingsTree();
            // todo: build initializer(s)

            _reflectionCache = null;
        }

        void AssertEncryptorIsNullOrValid()
        {
            var encryptor = _encryptor;

            // null is perfectly valid if they're not using encrypted settings (validated later)
            if (encryptor == null)
                return;

            if (!encryptor.CanDecrypt)
                throw new NFigException($"Encryptor for app \"{AppInfo.AppName}\" is not capable of decrypting.");

            // make sure a string can round trip correctly
            var original = "This is a random guid: " + Guid.NewGuid();
            string roundTrip;

            try
            {
                var encrypted = encryptor.Encrypt(original);
                roundTrip = encryptor.Decrypt(encrypted);
            }
            catch (Exception ex)
            {
                var nex = new NFigException("ISettingEncryptor threw an exception during the test encryption/decryption", ex);
                nex.Data["original"] = original;
                throw nex;
            }

            if (original != roundTrip)
            {
                var nex = new NFigException("The provided ISettingEncryptor did not pass the round-trip encryption/decryption test");
                nex.Data["original"] = original;
                nex.Data["roundTrip"] = roundTrip;
                throw nex;
            }
        }

        ReflectionCache CreateReflectionCache()
        {
            var cache = new ReflectionCache();
            var thisType = GetType();

            cache.ThisType = thisType;
//            cache.SettingsField = thisType.GetField(nameof(_settings), BindingFlags.NonPublic | BindingFlags.Instance);
//            cache.ValueCacheField = thisType.GetField(nameof(_valueCache), BindingFlags.NonPublic | BindingFlags.Instance);
//            cache.GetSettingItemMethod = _settings.GetType().GetProperty("Item").GetMethod;
            cache.PropertyToSettingMethod = thisType.GetMethod(nameof(PropertyToSetting), BindingFlags.NonPublic | BindingFlags.Instance);
            cache.PropertyToSettingDelegates = new Dictionary<Type, PropertyToSettingDelegate>();

            return cache;
        }

        PropertyToSettingDelegate GetPropertyToSettingDelegate(Type type)
        {
            PropertyToSettingDelegate del;
            if (_reflectionCache.PropertyToSettingDelegates.TryGetValue(type, out del))
                return del;

            var thisType = _reflectionCache.ThisType;
            var methodInfo = _reflectionCache.PropertyToSettingMethod.MakeGenericMethod(type);

            // I'd prefer to use Delegate.CreateDelegate(), but that isn't supported on all platforms at the moment.
            var methodArgTypes = new[] {thisType, typeof(PropertyInfo), typeof(SettingAttribute), typeof(SettingsGroup)};
            var dm = new DynamicMethod($"PropertyToSetting<{type.FullName}>+Delegate", typeof(Setting), methodArgTypes, restrictedSkipVisibility: true);
            var il = dm.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);           // [this]
            il.Emit(OpCodes.Ldarg_1);           // [this] [propertyInfo]
            il.Emit(OpCodes.Ldarg_2);           // [this] [propertyInfo] [settingAttribute]
            il.Emit(OpCodes.Ldarg_3);           // [this] [propertyInfo] [settingAttribute] [settingGroup]
            il.Emit(OpCodes.Call, methodInfo);  // [setting]
            il.Emit(OpCodes.Ret);

            del = (PropertyToSettingDelegate)dm.CreateDelegate(typeof(PropertyToSettingDelegate), this);

            _reflectionCache.PropertyToSettingDelegates[type] = del;
            return del;
        }

        // This method simply treats TSettings as the top-level settings group, and kicks off the recursive process of detecting all child settings and
        // settings groups.
        SettingsGroup GetSettingsTree()
        {
            var group = new SettingsGroup(_settingsType, "", null, null);
            PopulateSettingsGroup(group);
            return group;
        }

        // A SettingsGroup is a property on a settings class which is marked with the [SettingsGroup] attribute. This means that the property is not a single
        // setting, but rather the parent container of child settings (and/or child groups).
        //
        // This method learns everything about the setting group. It detects any custom converters applied to it, and 
        void PopulateSettingsGroup(SettingsGroup group)
        {
            // We could enforce that people must put converters on either the property or the class, but I'd rather people didn't have to remember which one
            // was correct. So we're going to look in both places.
            ApplyConverterAttributesToGroup(group, group.PropertyInfo.GetCustomAttributes<SettingConverterAttribute>());
            ApplyConverterAttributesToGroup(group, group.Type.GetTypeInfo().GetCustomAttributes<SettingConverterAttribute>());

            foreach (var pi in group.Type.GetProperties())
            {
                var name = group.Prefix + pi.Name;
                var propType = pi.PropertyType;

                var hasGroupAttribute = pi.GetCustomAttribute<SettingsGroupAttribute>() != null;
                var settingAttributes = pi.GetCustomAttributes<SettingAttribute>().ToArray();

                if (settingAttributes.Length > 0)
                {
                    if (settingAttributes.Length > 1)
                        throw new NFigException($"Property {name} has more than one Setting or EncryptedSetting attributes.");

                    if (hasGroupAttribute)
                        throw new NFigException($"Property {name} is marked as both a Setting and a SettingsGroup.");

                    try
                    {
                        var settingAttr = settingAttributes[0];
                        var toSetting = GetPropertyToSettingDelegate(pi.PropertyType);
                        var setting = toSetting(pi, settingAttr, group);

                        group.Settings.Add(setting);

                        // todo
//                        _settings.Add(setting);
//                        _settingsByName.Add(setting.Name, setting);
                    }
                    catch (TargetInvocationException ex)
                    {
                        // don't care about the fact that there's a target invocation exception
                        // what we want is the inner exception
                        if (ex.InnerException != null)
                            throw ex.InnerException;

                        throw;
                    }
                }
                else if (hasGroupAttribute)
                {
                    if (!propType.IsClass())
                        throw new NFigException($"Property {name} is marked with [SettingGroup], but is not a class type.");

                    var subGroup = new SettingsGroup(propType, name + ".", group, pi);
                    PopulateSettingsGroup(subGroup);
                    group.SettingGroups.Add(subGroup);
                }

            }
        }

        void ApplyConverterAttributesToGroup(SettingsGroup group, IEnumerable<SettingConverterAttribute> attributes)
        {
            foreach (var attr in attributes)
            {
                group.SetCustomConverter(attr.SettingType, attr.Converter);
            }
        }

        Setting PropertyToSetting<TValue>(PropertyInfo pi, SettingAttribute settingAttr, SettingsGroup group)
        {
            var name = group.Prefix + pi.Name;

            var isEncrypted = settingAttr.IsEncrypted;
            if (isEncrypted)
                AssertValidEncryptedSettingAttribute(name, settingAttr);

            var converter = GetConverterForProperty<TValue>(name, pi, group, out var isDefaultConverter);

            var description = pi.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            var changeRequiresRestart = pi.GetCustomAttribute<ChangeRequiresRestartAttribute>() != null;
            var allowInline = pi.GetCustomAttribute<DoNotInlineValuesAttribute>() == null;

            var meta = new SettingMetadata(name, description, pi.PropertyType, isEncrypted, converter, isDefaultConverter, changeRequiresRestart);

            // get the root default value
            var rootValue = isEncrypted ? default(TValue) : settingAttr.DefaultValue;
            var rootStringValue = GetStringFromDefaultAndValidate(name, rootValue, null, default(TDataCenter), converter, isEncrypted);
            var rootDefault = new DefaultValue<TTier, TDataCenter>(name, rootStringValue, null, default(TTier), default(TDataCenter), true);

            // Additional default values will come from these DefaultValueBase attributes, but we'll extract the actual values later.
            var defaultAttributes = pi.GetCustomAttributes<DefaultValueBaseAttribute>().ToArray();

            // create the setting
            return new Setting<TValue>(meta, group, rootDefault, defaultAttributes, allowInline);
        }

        void AssertValidEncryptedSettingAttribute(string name, SettingAttribute sa)
        {
            if (_encryptor == null)
                throw new NFigException($"Setting {name} is marked as encrypted, but no ISettingEncryptor was provided to the NFigStore.");

            if (sa.DefaultValue != null)
                throw new NFigException($"The SettingAttribute for {name} assigns a default value and is marked as encrypted. It cannot have both. " +
                                        $"This error is probably due to a class inheriting from SettingAttribute without obeying this rule.");
        }

        ISettingConverter<TValue> GetConverterForProperty<TValue>(string name, PropertyInfo pi, SettingsGroup group, out bool isDefault)
        {
            ISettingConverter convObj;
            var tValueType = typeof(TValue);
            isDefault = false;

            // see if there is a converter specified
            var converterAttributes = pi.GetCustomAttributes<SettingConverterAttribute>().ToArray();
            if (converterAttributes.Length == 0)
            {
                // check if the group has a custom converter
                convObj = group.GetCustomConverter(tValueType);

                if (convObj == null)
                {
                    // use the default converter, if there is one
                    convObj = DefaultConverters.Get(tValueType);
                    isDefault = true;

                    if (convObj == null)
                        throw new InvalidSettingConverterException($"No default converter is available for setting \"{name}\" of type {pi.PropertyType.Name}", pi.PropertyType);
                }
            }
            else if (converterAttributes.Length == 1)
            {
                convObj = converterAttributes[0].Converter;
            }
            else
            {
                throw new NFigException($"More than one SettingConverterAttribute was specified for \"{name}\"");
            }

            // verify the converter is good
            var converter = convObj as ISettingConverter<TValue>;
            if (converter == null)
            {
                throw new InvalidSettingConverterException(
                    $"Cannot use {convObj.GetType().Name} as setting converter for \"{name}\". The converter must implement ISettingConverter<{pi.PropertyType.Name}>.", pi.PropertyType);
            }

            return converter;
        }

        string GetStringFromDefaultAndValidate<TValue>(
            string name,
            object value,
            int? subAppId,
            TDataCenter dataCenter,
            ISettingConverter<TValue> converter,
            bool isEncrypted)
        {
            string stringValue;

            if (value is string && (isEncrypted || typeof(TValue) != typeof(string)))
            {
                // Don't need to convert to a string if value is already a string and TValue is not.
                // We expect that the human essentially already did the conversion.
                // Also, if setting is encrypted, then we always expect the string representation to be encrypted.
                stringValue = (string)value;
            }
            else
            {
                try
                {
                    // try convert the real value into its string representation
                    var tval = value is TValue ? (TValue)value : (TValue)Convert.ChangeType(value, typeof(TValue));
                    stringValue = converter.GetString(tval);

                    if (isEncrypted)
                        stringValue = AppInfo.Encrypt(stringValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidDefaultValueException(
                        $"Invalid default for setting \"{name}\". Cannot convert to a string representation.",
                        name,
                        value,
                        subAppId,
                        dataCenter.ToString(),
                        ex);
                }
            }

            // now make sure we can also convert the string value back into a real value
            try
            {
                var decrypted = isEncrypted ? AppInfo.Decrypt(stringValue) : stringValue;
                converter.GetValue(decrypted);
            }
            catch (Exception ex)
            {
                throw new InvalidDefaultValueException(
                    $"Invalid default value for setting \"{name}\". Cannot convert string representation back into a real value.",
                    name,
                    value,
                    subAppId,
                    dataCenter.ToString(),
                    ex);
            }

            return stringValue;
        }

        /******************************************************************************************************************************************************
         * Helper Classes and Delegates
         *****************************************************************************************************************************************************/

        delegate Setting PropertyToSettingDelegate(PropertyInfo pi, SettingAttribute sa, SettingsGroup group);

        class ReflectionCache
        {
            public Type ThisType;
//            public FieldInfo SettingsField;
//            public FieldInfo ValueCacheField;
//            public MethodInfo GetSettingItemMethod;
            public MethodInfo PropertyToSettingMethod;
            public Dictionary<Type, PropertyToSettingDelegate> PropertyToSettingDelegates;
        }

        class SettingsGroup
        {
            [CanBeNull]
            Dictionary<Type, ISettingConverter> _converters;

            public Type Type { get; }
            public string Prefix { get; }
            [CanBeNull]
            public SettingsGroup Parent { get; }
            public PropertyInfo PropertyInfo { get; }
            public List<SettingsGroup> SettingGroups { get; }
            public List<Setting> Settings { get; }

            public SettingsGroup(Type type, string prefix, SettingsGroup parent, PropertyInfo pi)
            {
                Type = type;
                Prefix = prefix;
                Parent = parent;
                PropertyInfo = pi;

                SettingGroups = new List<SettingsGroup>();
                Settings = new List<Setting>();
            }

            public void SetCustomConverter(Type settingType, ISettingConverter converter)
            {
                if (_converters == null)
                    _converters = new Dictionary<Type, ISettingConverter>();
                else if (_converters.ContainsKey(settingType))
                    throw new NFigException($"More than one ISettingConverter was specified for type {settingType.FullName} on settings group {Prefix}{PropertyInfo.Name}");

                // the converter should already be validated at this point
                _converters[settingType] = converter;
            }

            [CanBeNull]
            public ISettingConverter GetCustomConverter(Type settingType)
            {
                var group = this;
                do
                {
                    if (group._converters != null && group._converters.TryGetValue(settingType, out var converter))
                        return converter;

                } while ((group = group.Parent) != null);

                return null;
            }
        }

        abstract class Setting : IBySettingItem
        {
            public string Name { get; }
            public SettingMetadata Metadata { get; }
            public SettingsGroup Group { get; }
            public DefaultValue<TTier, TDataCenter> RootDefault { get; }
            public DefaultValueBaseAttribute[] DefaultValueAttributes { get; }
            public bool AllowInline { get; }

            protected Setting(
                SettingMetadata metadata,
                SettingsGroup group,
                DefaultValue<TTier, TDataCenter> rootDefault,
                DefaultValueBaseAttribute[] defaultValueAttributes,
                bool allowInline)
            {
                Name = metadata.Name;
                Metadata = metadata;
                Group = group;
                RootDefault = rootDefault;
                DefaultValueAttributes = defaultValueAttributes;
                AllowInline = allowInline;
            }
        }

        class Setting<TValue> : Setting
        {
            internal Setting(
                SettingMetadata metadata,
                SettingsGroup group,
                DefaultValue<TTier, TDataCenter> rootDefault,
                DefaultValueBaseAttribute[] defaultValueAttributes,
                bool allowInline)
                : base(metadata, group, rootDefault, defaultValueAttributes, allowInline)
            {
            }
        }
    }
}