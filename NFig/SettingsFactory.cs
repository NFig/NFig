using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using JetBrains.Annotations;
using NFig.Converters;
using NFig.Encryption;
using NFig.Metadata;

namespace NFig
{
    class SettingsFactory<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        readonly SettingsGroup _tree;
        readonly List<Setting> _settings = new List<Setting>();
        readonly BySetting<Setting> _settingsByName;
        readonly Type _settingsType;
        readonly Type _tierType;
        readonly Type _dataCenterType;
        readonly ISettingEncryptor _encryptor;
        readonly ReflectionCache _reflectionCache;
        readonly SubAppCache _rootCache = new SubAppCache();
        Dictionary<int, SubAppCache> _cacheBySubAppId;
        List<SubApp> _subAppList;
        object[] _valueCache;
        int _valueCacheCount;

        internal AppInternalInfo<TTier, TDataCenter> AppInfo { get; }
        internal TTier Tier { get; }
        internal TDataCenter DataCenter { get; }

        internal SettingsFactory(AppInternalInfo<TTier, TDataCenter> appInfo, TTier tier, TDataCenter dataCenter)
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

            _tree = GetSettingsTree();
            _settingsByName = new BySetting<Setting>(_settings);
            _rootCache.Defaults = CollectDefaultsForSubApp(null, null);
            appInfo.GeneratedSettingsMetadata = GetMetadataBySetting();
        }

        internal bool SettingExists(string settingName) => _settingsByName.ContainsKey(settingName);

        internal Type GetSettingType(string settingName) => _settingsByName[settingName].Type;

        internal object GetSettingValue(TSettings settings, string settingName)
        {
            return _settingsByName[settingName].GetValueAsObject(settings);
        }

        internal TValue GetSettingValue<TValue>(TSettings settings, string settingName)
        {
            var setting = _settingsByName[settingName];

            if (setting is Setting<TValue> s)
            {
                return s.GetValue(settings);
            }

            var ex = new NFigException($"{nameof(GetSettingValue)} called with the incorrect type for setting {settingName}");
            ex.Data["TValue"] = typeof(TValue).FullName;
            ex.Data["SettingType"] = setting.Type.FullName;
            throw ex;
        }

        internal void TryGetSettings(
            int? subAppId,
            OverridesSnapshot<TTier, TDataCenter> snapshot,
            out TSettings settings,
            ref List<InvalidOverrideValueException> exceptions)
        {
            var cache = GetSubAppCache(subAppId);
            if (cache?.IsInitialized != true)
                throw new NFigException($"Cannot get settings for sub-app {subAppId}. It has not been registered.");

            var initializer = cache.Initializer;
            settings = initializer();
            settings.SetBasicInformation(AppInfo.AppName, snapshot.Commit, subAppId, cache.SubAppName, Tier, DataCenter);

            TryApplyOverrides(settings, subAppId, snapshot.Overrides, ref exceptions);
        }

        internal ListBySetting<DefaultValue<TTier, TDataCenter>> RegisterRootApp()
        {
            InitializeSubAppCache(_rootCache, null);
            return _rootCache.Defaults;
        }

        [NotNull]
        internal SubApp[] GetRegisteredSubApps()
        {
            if (_subAppList == null)
                return Array.Empty<SubApp>();

            lock (_rootCache)
            {
                return _subAppList.ToArray();
            }
        }

        internal ListBySetting<DefaultValue<TTier, TDataCenter>> RegisterSubApp(int subAppId, string subAppName)
        {
            if (subAppName == null)
                throw new ArgumentNullException(nameof(subAppName));

            SubAppCache cache;
            lock (_rootCache)
            {
                cache = GetSubAppCache(subAppId);

                if (cache == null)
                {
                    cache = new SubAppCache();
                    cache.SubAppId = subAppId;

                    if (_cacheBySubAppId == null)
                        _cacheBySubAppId = new Dictionary<int, SubAppCache>();

                    _cacheBySubAppId[subAppId] = cache;
                }
                else if (cache.SubAppName != subAppName)
                {
                    var ex = new NFigException($"A Sub-App with ID {subAppId} has already been registered with a different name");
                    ex.Data["ExistingSubAppName"] = cache.SubAppName;
                    ex.Data["NewSubAppName"] = subAppName;
                    throw ex;
                }
            }

            InitializeSubAppCache(cache, subAppName);
            return cache.Defaults;
        }

        [CanBeNull]
        SubAppCache GetSubAppCache(int? subAppId)
        {
            lock (_rootCache)
            {
                if (subAppId.HasValue)
                {
                    if (_cacheBySubAppId == null)
                        return null;

                    return _cacheBySubAppId.TryGetValue(subAppId.Value, out var cache) ? cache : null;
                }

                InitializeSubAppCache(_rootCache, null);
                return _rootCache;
            }

        }

        void InitializeSubAppCache(SubAppCache cache, string subAppName)
        {
            if (cache.IsInitialized)
                return;

            lock (cache)
            {
                if (!cache.IsInitialized)
                {
                    cache.SubAppName = subAppName;

                    // the defaults might already be set if this is the root app
                    if (cache.Defaults == null)
                        cache.Defaults = CollectDefaultsForSubApp(cache.SubAppId, subAppName);

                    // check if we can reuse the root app's initializer
                    if (cache != _rootCache && cache.Defaults == _rootCache.Defaults)
                    {
                        // there are no sub-app specific defaults for this sub-app, so we don't need to create a unique initializer
                        InitializeSubAppCache(_rootCache, null);
                        cache.Initializer = _rootCache.Initializer;
                    }
                    else
                    {
                        cache.Initializer = CreateInitializer(cache.SubAppId, cache.Defaults);
                    }

                    if (cache.SubAppId.HasValue)
                    {
                        if (_subAppList == null)
                            _subAppList = new List<SubApp>();

                        _subAppList.Add(new SubApp(cache.SubAppId.Value, subAppName));
                    }

                    Interlocked.MemoryBarrier(); // ensure IsInitialized doesn't get set to true before all the other properties have been updated
                    cache.IsInitialized = true;
                }
            }
        }

        ListBySetting<DefaultValue<TTier, TDataCenter>> CollectDefaultsForSubApp(int? subAppId, string subAppName)
        {
            var isRoot = subAppId == null;
            var allDefaults = new List<DefaultValue<TTier, TDataCenter>>();
            var defaults = new List<DefaultValue<TTier, TDataCenter>>();

            foreach (var setting in _settings)
            {
                if (isRoot)
                    allDefaults.Add(setting.RootAnyAnyDefault);

                if (setting.DefaultValueAttributes == null || setting.DefaultValueAttributes.Length == 0)
                    continue;

                foreach (var attr in setting.DefaultValueAttributes)
                {
                    foreach (var obj in attr.GetDefaults(AppInfo.AppName, setting.Name, setting.Type, Tier, subAppId, subAppName))
                    {
                        if (obj == null)
                            throw new NFigException($"{attr.GetType().Name} on setting {setting.Name} returned a null reference from GetDefaults()");

                        // make sure the object is actually a default value
                        var creationInfo = obj as DefaultCreationInfo<TTier, TDataCenter>;
                        if (creationInfo == null)
                        {
                            throw new NFigException(
                                $"Object returned from {attr.GetType().Name}.GetSettings() was not a DefaultValue<{_tierType.Name},{_dataCenterType.Name}> on setting {setting.Name}");
                        }

                        // check if we care about this default
                        if (creationInfo.SubAppId != subAppId)
                            continue;

                        if (!Compare.IsDefault(creationInfo.Tier) && !Compare.AreEqual(creationInfo.Tier, Tier))
                            continue;

                        // If we've gotten to here, then this is a default we care about. We need to create a DefaultValue object.
                        var defaultValue = setting.CreateDefaultValueFromCreationInfo(this, creationInfo);

                        // make sure there isn't a conflicting default
                        foreach (var existing in defaults)
                        {
                            if (existing.HasSameSubAppTierDataCenter(defaultValue))
                            {
                                var ex = new NFigException($"Multiple defaults were specified for the same environment on setting {setting.Name}");
                                ex.Data["Tier"] = defaultValue.Tier;
                                ex.Data["DataCenter"] = defaultValue.DataCenter;

                                if (defaultValue.SubAppId.HasValue)
                                    ex.Data["SubAppId"] = defaultValue.SubAppId;

                                throw ex;
                            }
                        }

                        defaults.Add(defaultValue);
                        allDefaults.Add(defaultValue);
                    }
                }

                defaults.Clear();
            }

            if (isRoot)
                return new ListBySetting<DefaultValue<TTier, TDataCenter>>(allDefaults);

            // for sub-apps, we need to merge their defaults with the root defaults

            if (allDefaults.Count == 0) // we can reuse the root defaults if there are no sub-app specific defaults
                return _rootCache.Defaults;

            return new ListBySetting<DefaultValue<TTier, TDataCenter>>(allDefaults, _rootCache.Defaults);
        }

        BySetting<SettingMetadata> GetMetadataBySetting()
        {
            var settings = _settings;
            var metas = new SettingMetadata[settings.Count];
            for (var i = 0; i < metas.Length; i++)
            {
                metas[i] = settings[i].Metadata;
            }

            return new BySetting<SettingMetadata>(metas);
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
            cache.SettingsField = thisType.GetField(nameof(_settings), BindingFlags.NonPublic | BindingFlags.Instance);
            cache.GetSettingItemMethod = _settings.GetType().GetProperty("Item").GetMethod;
            cache.ValueCacheField = thisType.GetField(nameof(_valueCache), BindingFlags.NonPublic | BindingFlags.Instance);
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
            var dm = new DynamicMethod(
                $"PropertyToSetting<{type.FullName}>+Delegate",
                typeof(Setting),
                methodArgTypes,
                _reflectionCache.ThisType.Module(),
                true);

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
        // This method learns everything about the setting group. It detects any custom converters applied to it, and learns about the group's child settings.
        void PopulateSettingsGroup(SettingsGroup group)
        {
            // We could enforce that people must put converters on either the property or the class, but I'd rather people didn't have to remember which one
            // was correct. So we're going to look in both places.
            if (group.PropertyInfo != null)
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
                        setting.Index = _settings.Count;
                        _settings.Add(setting);
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

            var typeName = pi.PropertyType.FullName;
            var isEnum = pi.PropertyType.IsEnum();
            var converterTypeName = converter.GetType().FullName;
            var meta = new SettingMetadata(name, description, typeName, isEncrypted, isEnum, converterTypeName, isDefaultConverter, changeRequiresRestart);

            // get the root default value
            var rootValue = isEncrypted ? default(TValue) : settingAttr.DefaultValue;
            var rootStringValue = GetStringFromDefaultAndValidate(name, rootValue, null, default(TDataCenter), converter, isEncrypted);
            var rootDefault = new DefaultValue<TTier, TDataCenter>(name, rootStringValue, null, default(TTier), default(TDataCenter), true);

            // Additional default values will come from these DefaultValueBase attributes, but we'll extract the actual values later.
            var defaultAttributes = pi.GetCustomAttributes<DefaultValueBaseAttribute>().ToArray();

            // create the setting
            return new Setting<TValue>(pi, meta, converter, group, rootDefault, defaultAttributes, allowInline);
        }

        void AssertValidEncryptedSettingAttribute(string name, SettingAttribute sa)
        {
            if (_encryptor == null)
                throw new NFigException($"Setting {name} is marked as encrypted, but no ISettingEncryptor was provided to the NFigStore.");

            if (sa.DefaultValue != null)
                throw new NFigException($"The SettingAttribute for {name} assigns a default value and is marked as encrypted. It cannot have both. " +
                                        "This error is probably due to a class inheriting from SettingAttribute without obeying this rule.");
        }

        static ISettingConverter<TValue> GetConverterForProperty<TValue>(string name, PropertyInfo pi, SettingsGroup group, out bool isDefault)
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

            if (value is string s && (isEncrypted || typeof(TValue) != typeof(string)))
            {
                // Don't need to convert to a string if value is already a string and TValue is not.
                // We expect that the human essentially already did the conversion.
                // Also, if setting is encrypted, then we always expect the string representation to be encrypted.
                stringValue = s;
            }
            else if (isEncrypted)
            {
                throw new InvalidDefaultValueException(
                    $"Invalid default for setting \"{name}\". Defaults for encrypted settings must be in the form of an encrypted string.",
                    name,
                    value,
                    subAppId,
                    dataCenter.ToString());
            }
            else
            {
                try
                {
                    // try convert the real value into its string representation
                    var tval = value is TValue ? (TValue)value : (TValue)Convert.ChangeType(value, typeof(TValue));
                    stringValue = converter.GetString(tval);
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

        SettingsInitializer CreateInitializer(int? subAppId, ListBySetting<DefaultValue<TTier, TDataCenter>> defaults)
        {
            var dmName = $"TSettings_Instantiate+{subAppId}+{Tier}+{DataCenter}";
            var dm = new DynamicMethod(dmName, _settingsType, new[] { _reflectionCache.ThisType }, _reflectionCache.ThisType.Module(), true);
            var il = dm.GetILGenerator();

            EmitSettingsGroup(il, _tree, subAppId, defaults); // [TSettings s]
            il.Emit(OpCodes.Ret);

            return (SettingsInitializer)dm.CreateDelegate(typeof(SettingsInitializer), this);
        }

        /// <summary>
        /// Emits IL for initializing TSettings and child objects. It calls EmitDefaults for each group.
        /// </summary>
        void EmitSettingsGroup(ILGenerator il, SettingsGroup group, int? subAppId, ListBySetting<DefaultValue<TTier, TDataCenter>> defaults)
        {
            // Initial stack: ...
            // End stack:     ... [group]

            // Create a new instance of the group's class.
            var ctor = group.Type.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
                throw new NFigException($"Cannot use type {group.Type.Name} for settings groups. It does not have a parameterless constructor.");

            il.Emit(OpCodes.Newobj, ctor);               // [group]
            EmitDefaults(il, group, subAppId, defaults); // [group]

            foreach (var childGroup in group.SettingGroups)
            {
                il.Emit(OpCodes.Dup);                                  // [group] [group]
                EmitSettingsGroup(il, childGroup, subAppId, defaults); // [group] [group] [childGroup]
                EmitSetProperty(il, childGroup.PropertyInfo);          // [group]
            }
        }

        /// <summary>
        /// Emits IL to assign the correct default values to all Setting properties in a group.
        /// </summary>
        void EmitDefaults(ILGenerator il, SettingsGroup group, int? subAppId, ListBySetting<DefaultValue<TTier, TDataCenter>> defaults)
        {
            // Initial stack: ... [group]
            // End stack:     ... [group]

            foreach (var setting in group.Settings)
            {
                var defaultValue = GetBestDefaultFor(setting.Name, subAppId, defaults);
                var typeOfValue = setting.Type;
                var strValue = setting.Metadata.IsEncrypted ? AppInfo.Decrypt(defaultValue.Value) : defaultValue.Value;

                il.Emit(OpCodes.Dup); // [group] [group]

                if (setting.AllowInline)
                {
                    var objValue = GetInlineValue(setting, defaultValue, strValue, out var strategy, out var cacheIndex);

                    switch (strategy)
                    {
                        case InlineStrategy.Null:
                            il.Emit(OpCodes.Ldnull); // [group] [group] [null]
                            break;
                        case InlineStrategy.Cache:
                            if (cacheIndex == null)
                                throw new NFigException("Bug in NFig: cacheIndex not set");

                            var cacheField = _reflectionCache.ValueCacheField;
                            il.Emit(OpCodes.Ldarg_0);                    // [group] [group] [factory this]
                            il.Emit(OpCodes.Ldfld, cacheField);          // [group] [group] [_valueCache]
                            il.Emit(OpCodes.Ldc_I4, cacheIndex.Value);   // [group] [group] [_valueCache] [index]
                            il.Emit(OpCodes.Ldelem_Ref);                 // [group] [group] [valueToSet]
                            if (typeOfValue.IsValueType())
                                il.Emit(OpCodes.Unbox_Any, typeOfValue); // [group] [group] [unboxed valueToSet]
                            break;
                        case InlineStrategy.String:
                            il.Emit(OpCodes.Ldstr, (string)objValue); // [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Int32:
                            il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(objValue)); // [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Int64:
                            il.Emit(OpCodes.Ldc_I8, Convert.ToInt64(objValue)); // [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Float32:
                            il.Emit(OpCodes.Ldc_R4, Convert.ToSingle(objValue)); // [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Float64:
                            il.Emit(OpCodes.Ldc_R8, Convert.ToDouble(objValue)); // [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Char:
                            il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(objValue)); // [group] [group] [valueToSet]
                            break;
                        default:
                            throw new NFigException($"Bug in NFig: InlineStrategy.{strategy} has not been implemented");
                    }
                }
                else
                {
                    // Inlining is disabled. We have to call the converter every time.
                    var converterType = typeof(ISettingConverter<>).MakeGenericType(typeOfValue);
                    var getValue = converterType.GetMethod(nameof(ISettingConverter<bool>.GetValue)); // the <bool> here really doesn't matter - any type would work

                    var settingType = typeof(Setting<>).MakeGenericType(typeOfValue);
                    var converterProp = settingType.GetProperty(nameof(Setting<bool>.Converter)); // the <bool> here really doesn't matter - any type would work

                    var settingsField = _reflectionCache.SettingsField;
                    var getSettingItem = _reflectionCache.GetSettingItemMethod;

                    var index = setting.Index;
                    if (index >= _settings.Count || _settings[index] != setting)
                        throw new NFigException("NFig Bug: Internal setting index is wrong");

                    il.Emit(OpCodes.Ldarg_0);                           // [group] [group] [factory this]
                    il.Emit(OpCodes.Ldfld, settingsField);              // [group] [group] [_settings]
                    il.Emit(OpCodes.Ldc_I4, index);                     // [group] [group] [_settings] [index]
                    il.Emit(OpCodes.Callvirt, getSettingItem);          // [group] [group] [setting]
                    il.Emit(OpCodes.Callvirt, converterProp.GetMethod); // [group] [group] [Converter]
                    il.Emit(OpCodes.Ldstr, strValue);                   // [group] [group] [Converter] [strValue]
                    il.Emit(OpCodes.Callvirt, getValue);                // [group] [group] [valueToSet]
                }

                // stack should be: [group] [group] [valueToSet]
                EmitSetProperty(il, setting.PropertyInfo); // [group]
            }
        }

        /// <summary>
        /// Assigns a property by either calling the setting, or by directly setting a backing field when no setter exists.
        /// Top of stack should be [obj] [value] before calling this method. Both are popped from the stack before returning.
        /// </summary>
        static void EmitSetProperty(ILGenerator il, PropertyInfo property)
        {
            if (property.SetMethod == null)
            {
                // possible a getter-only property
                var backingField = property.DeclaringType.GetField("<" + property.Name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (backingField == null)
                {
                    throw new NFigException($"Property {property.Name} does not have a setter.");
                }

                il.Emit(OpCodes.Stfld, backingField);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, property.SetMethod);
            }
        }

        object GetInlineValue(Setting setting, DefaultValue<TTier, TDataCenter> defaultValue, string strValue, out InlineStrategy strategy, out int? cacheIndex)
        {
            strategy = GetInlineStrategyForType(setting.Type);

            if (strategy == InlineStrategy.String || strategy == InlineStrategy.Cache)
            {
                if (setting.Metadata.IsEncrypted) // I don't think we want encrypted strings being interned
                    strategy = InlineStrategy.Cache;

                var defaultIsForRootApp = defaultValue.SubAppId == null;

                // check if we already have a cached value we can use
                if (defaultIsForRootApp && setting.RootDefaultCacheIndex.HasValue)
                {
                    if (setting.RootDefaultCacheIndex == -1) // -1 indicates a "cached" null value
                    {
                        cacheIndex = null;
                        return null;
                    }

                    cacheIndex = setting.RootDefaultCacheIndex;
                    return _valueCache[cacheIndex.Value];
                }

                // we need to cache this value
                var obj = setting.GetValueFromString(strValue);

                if (obj == null)
                {
                    cacheIndex = null;
                    strategy = InlineStrategy.Null;

                    if (defaultIsForRootApp)
                        setting.RootDefaultCacheIndex = -1; // mark this as a "cached" null value

                    return null;
                }

                // todo: when we move to .NET Standard 2.0 we should call "obj = string.Intern((string)obj)" here if the strategy == String.
                cacheIndex = AddToValueCache(obj);

                if (defaultIsForRootApp)
                    setting.RootDefaultCacheIndex = cacheIndex;

                return obj;
            }

            // primitive values are simple, and never cached
            cacheIndex = null;
            return setting.GetValueFromString(strValue);
        }

        int AddToValueCache(object value)
        {
            var cache = _valueCache;
            var count = _valueCacheCount;

            if (cache == null)
            {
                _valueCache = cache = new object[16];
            }
            else if (count == cache.Length)
            {
                var oldCache = _valueCache;
                var newCache = new object[oldCache.Length * 2];
                Array.Copy(oldCache, newCache, count);
                _valueCache = cache = newCache;
            }

            cache[count] = value;
            _valueCacheCount = count + 1;
            return count;
        }

        enum InlineStrategy
        {
            Null,
            Cache,
            String,
            Int32,
            Int64,
            Float32,
            Float64,
            Char,
        }

        static InlineStrategy GetInlineStrategyForType(Type type)
        {
            if (type == typeof(string))
                return InlineStrategy.String;

            if (type.IsEnum())
                type = type.GetEnumUnderlyingType();

            if (!type.IsPrimitive())
                return InlineStrategy.Cache;

            if (type == typeof(bool)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint))
            {
                return InlineStrategy.Int32;
            }

            if (type == typeof(long) || type == typeof(ulong))
                return InlineStrategy.Int64;

            if (type == typeof(double))
                return InlineStrategy.Float64;

            if (type == typeof(float))
                return InlineStrategy.Float32;

            if (type == typeof(char))
                return InlineStrategy.Char;

            return InlineStrategy.Cache; // only other primitive types are IntPtr and UIntPtr (never actually going to happen)
        }

        DefaultValue<TTier, TDataCenter> GetBestDefaultFor(string settingName, int? subAppId, ListBySetting<DefaultValue<TTier, TDataCenter>> defaults)
        {
            DefaultValue<TTier, TDataCenter> bestDefault = null;
            var tier = Tier;
            var dataCenter = DataCenter;

            foreach (var def in defaults[settingName])
            {
                if (def.IsValidFor(subAppId, tier, dataCenter) && (bestDefault == null || def.IsMoreSpecificThan(bestDefault)))
                {
                    bestDefault = def;
                }
            }

            if (bestDefault == null)
            {
                throw new NFigException(
                    $"Bug in NFig: Could not locate best default for setting {settingName}. " +
                    $"SubApp = {subAppId}, Tier = {tier}, DC = {dataCenter}");
            }

            return bestDefault;
        }

        void TryApplyOverrides(
            TSettings settingsObj,
            int? subAppId,
            ListBySetting<OverrideValue<TTier, TDataCenter>> overrides,
            ref List<InvalidOverrideValueException> exceptions)
        {
            if (overrides == null)
                return;

            foreach (var kvp in overrides)
            {
                Setting setting;
                if (!_settingsByName.TryGetValue(kvp.Key, out setting))
                    continue; // there is no setting by that name

                var bestOverride = GetBestOverrideFor(subAppId, kvp.Value);

                if (bestOverride == null)
                    continue;

                var ex = setting.TryApplyOverride(settingsObj, bestOverride, AppInfo);

                if (ex != null)
                {
                    if (exceptions == null)
                        exceptions = new List<InvalidOverrideValueException>();

                    exceptions.Add(ex);
                }
            }
        }

        [CanBeNull]
        OverrideValue<TTier, TDataCenter> GetBestOverrideFor(int? subAppId, ListBySetting<OverrideValue<TTier, TDataCenter>>.ValueList overrides)
        {
            OverrideValue<TTier, TDataCenter> bestOverride = null;
            var tier = Tier;
            var dataCenter = DataCenter;

            foreach (var ov in overrides)
            {
                if (ov.IsValidFor(subAppId, tier, dataCenter) && (bestOverride == null || ov.IsMoreSpecificThan(bestOverride)))
                {
                    bestOverride = ov;
                }
            }

            return bestOverride;
        }

        /******************************************************************************************************************************************************
         * Helper Classes and Delegates
         *****************************************************************************************************************************************************/

        delegate TSettings SettingsInitializer();
        delegate Setting PropertyToSettingDelegate(PropertyInfo pi, SettingAttribute sa, SettingsGroup group);
        delegate void SettingSetterDelegate<TValue>(TSettings settings, TValue value);
        delegate TValue SettingGetterDelegate<TValue>(TSettings settings);

        class ReflectionCache
        {
            public Type ThisType;
            public FieldInfo SettingsField;
            public MethodInfo GetSettingItemMethod;
            public FieldInfo ValueCacheField;
            public MethodInfo PropertyToSettingMethod;
            public Dictionary<Type, PropertyToSettingDelegate> PropertyToSettingDelegates;
        }

        class SubAppCache
        {
            public int? SubAppId { get; set; }
            public string SubAppName { get; set; }
            public ListBySetting<DefaultValue<TTier, TDataCenter>> Defaults { get; set; }
            public SettingsInitializer Initializer { get; set; }
            public bool IsInitialized { get; set; }
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
            public Type Type { get; }
            public PropertyInfo PropertyInfo { get; }
            public SettingMetadata Metadata { get; }
            public SettingsGroup Group { get; }
            public DefaultValue<TTier, TDataCenter> RootAnyAnyDefault { get; } // the default provided via the [Setting] attribute
            public DefaultValueBaseAttribute[] DefaultValueAttributes { get; }
            public bool AllowInline { get; }
            public int Index { get; set; } // the index into _settings where this Setting lives
            public int? RootDefaultCacheIndex { get; set; } // index into _valueCache where the root default is cached (if applicable)

            protected Setting(
                Type type,
                PropertyInfo propertyInfo,
                SettingMetadata metadata,
                SettingsGroup group,
                DefaultValue<TTier, TDataCenter> rootAnyAnyDefault,
                DefaultValueBaseAttribute[] defaultValueAttributes,
                bool allowInline)
            {
                Name = metadata.Name;
                Type = type;
                PropertyInfo = propertyInfo;
                Metadata = metadata;
                Group = group;
                RootAnyAnyDefault = rootAnyAnyDefault;
                DefaultValueAttributes = defaultValueAttributes;
                AllowInline = allowInline;
            }


            public abstract object GetValueAsObject(TSettings settings);
            public abstract InvalidOverrideValueException TryApplyOverride(TSettings settings, OverrideValue<TTier, TDataCenter> over, AppInternalInfo<TTier, TDataCenter> appInfo);
            public abstract object GetValueFromString(string str);
            public abstract bool TryGetValueFromString(string str, out object value);
            public abstract bool TryGetStringFromValue(object value, out string str);

            public abstract DefaultValue<TTier, TDataCenter> CreateDefaultValueFromCreationInfo(
                SettingsFactory<TSettings, TTier, TDataCenter> factory,
                DefaultCreationInfo<TTier, TDataCenter> info);
        }

        class Setting<TValue> : Setting
        {
            SettingGetterDelegate<TValue> _getter;
            SettingSetterDelegate<TValue> _setter;

            public ISettingConverter<TValue> Converter { get; }

            internal Setting(
                PropertyInfo propertyInfo,
                SettingMetadata metadata,
                ISettingConverter<TValue> converter,
                SettingsGroup group,
                DefaultValue<TTier, TDataCenter> rootAnyAnyDefault,
                DefaultValueBaseAttribute[] defaultValueAttributes,
                bool allowInline)
                : base(typeof(TValue), propertyInfo, metadata, group, rootAnyAnyDefault, defaultValueAttributes, allowInline)
            {
                Converter = converter;
            }

            public TValue GetValue(TSettings settings)
            {
                if (_getter == null)
                    _getter = CreateGetterMethod();

                return _getter(settings);
            }

            public override object GetValueAsObject(TSettings settings)
            {
                return GetValue(settings);
            }

            public override InvalidOverrideValueException TryApplyOverride(TSettings settings, OverrideValue<TTier, TDataCenter> over, AppInternalInfo<TTier, TDataCenter> appInfo)
            {
                TValue value;

                try
                {
                    var strValue = Metadata.IsEncrypted ? appInfo.Decrypt(over.Value) : over.Value;
                    value = Converter.GetValue(strValue);
                }
                catch (Exception ex)
                {
                    var invalidEx = new InvalidOverrideValueException(
                        $"Invalid override value for setting \"{Name}\". Cannot convert the string override to a real value.",
                        Name,
                        over.Value, // intentionally using over.Value here instead of strValue since strValue could be a decrypted value that people don't want in their logs
                        over.SubAppId,
                        over.DataCenter.ToString(),
                        ex);

                    return invalidEx;
                }

                if (_setter == null)
                    _setter = CreateSetterMethod();

                _setter(settings, value);
                return null;
            }

            public override object GetValueFromString(string str)
            {
                return Converter.GetValue(str);
            }

            public override bool TryGetValueFromString(string str, out object value)
            {
                value = null;
                try
                {
                    value = Converter.GetValue(str);
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }

            public override bool TryGetStringFromValue(object value, out string str)
            {
                str = null;
                try
                {
                    str = Converter.GetString((TValue)value);
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }

            public override DefaultValue<TTier, TDataCenter> CreateDefaultValueFromCreationInfo(
                SettingsFactory<TSettings, TTier, TDataCenter> factory,
                DefaultCreationInfo<TTier, TDataCenter> info)
            {
                var strValue = factory.GetStringFromDefaultAndValidate(Name, info.Value, info.SubAppId, info.DataCenter, Converter, Metadata.IsEncrypted);
                return new DefaultValue<TTier, TDataCenter>(Name, strValue, info.SubAppId, info.Tier, info.DataCenter, info.AllowsOverrides);
            }

            SettingSetterDelegate<TValue> CreateSetterMethod()
            {
                var dm = new DynamicMethod("AssignSetting_" + Name, null, new[] { typeof(TSettings), Type }, GetType().Module(), true);
                var il = dm.GetILGenerator();

                // arg 0 = TSettings settings
                // arg 1 = TValue value

                il.Emit(OpCodes.Ldarg_0);                          // [settings]
                EmitLoadGroup(il, Group);                          // [group]
                il.Emit(OpCodes.Ldarg_1);                          // [group] [value]
                EmitSetProperty(il, PropertyInfo);                 // empty
                il.Emit(OpCodes.Ret);

                return (SettingSetterDelegate<TValue>)dm.CreateDelegate(typeof(SettingSetterDelegate<TValue>));
            }

            SettingGetterDelegate<TValue> CreateGetterMethod()
            {
                var dm = new DynamicMethod("RetrieveSetting_" + Name, typeof(TValue), new[] { typeof(TSettings) }, GetType().Module(), true);
                var il = dm.GetILGenerator();

                // arg 0 = TSettings settings

                il.Emit(OpCodes.Ldarg_0);                          // [settings]
                EmitLoadGroup(il, Group);                          // [group]
                il.Emit(OpCodes.Callvirt, PropertyInfo.GetMethod); // [value]
                il.Emit(OpCodes.Ret);

                return (SettingGetterDelegate<TValue>)dm.CreateDelegate(typeof(SettingGetterDelegate<TValue>));
            }

            static void EmitLoadGroup(ILGenerator il, SettingsGroup group)
            {
                // initial stack: [settings]
                // end stack:     [group]

                if (group.Parent == null) // there is no parent, so the TSettings object is actually the group object we want, which is on the stack already
                    return;

                if (group.Parent.Parent == null)
                {
                    // The group we want is only one level below TSettings. This is the most common pattern, so we're providing a special case for it to improve
                    // performance and reduce allocations.
                    il.Emit(OpCodes.Callvirt, group.PropertyInfo.GetMethod); // [group]
                    return;
                }

                // The group we want is more than one level deep. We need to create a list of methods to call.
                var methodList = new List<MethodInfo>();
                var g = group;
                do
                {
                    methodList.Add(g.PropertyInfo.GetMethod);
                    g = g.Parent;

                } while (g?.PropertyInfo != null);

                // the list was built bottom up, but we need to emit top down, so we go in reverse
                for (var i = methodList.Count - 1; i >= 0; i--)
                {
                    il.Emit(OpCodes.Callvirt, methodList[i]); // [group]
                }
            }
        }
    }
}