using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using NFig.Encryption;

namespace NFig
{
    class SettingsFactory<TSettings, TSubApp, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TSubApp, TTier, TDataCenter>, new()
        where TSubApp : struct
        where TTier : struct
        where TDataCenter : struct
    {
        readonly List<Setting> _settings = new List<Setting>();
        readonly Dictionary<string, Setting> _settingsByName = new Dictionary<string, Setting>();

        readonly InitializeSettingsDelegate _initializer;
        object[] _valueCache;
        int _valueCacheCount;

        readonly Type _settingsType;
        readonly Type _subAppType;
        readonly Type _tierType;
        readonly Type _dataCenterType;

        readonly ISettingEncryptor _encryptor;
        
        readonly ReflectionCache _reflectionCache;

        delegate Setting PropertyToSettingDelegate(PropertyInfo pi, SettingAttribute sa, SettingGroup group);

        readonly Dictionary<Type, object> _defaultConverters = new Dictionary<Type, object>
        {
            {typeof(bool), new BooleanSettingConverter()},
            {typeof(byte), new ByteSettingConverter()},
            {typeof(short), new ShortSettingConverter()},
            {typeof(ushort), new UShortSettingConverter()},
            {typeof(int), new IntSettingConverter()},
            {typeof(uint), new UIntSettingConverter()},
            {typeof(long), new LongSettingConverter()},
            {typeof(ulong), new ULongSettingConverter()},
            {typeof(float), new FloatSettingConverter()},
            {typeof(double), new DoubleSettingConverter()},
            {typeof(string), new StringSettingConverter()},
            {typeof(char), new CharSettingConverter()},
            {typeof(decimal), new DecimalSettingConverter()},
        };

        public string GlobalAppName { get; }
        public TTier Tier { get; }
        public TDataCenter DataCenter { get; }

        public bool HasEncryptor => _encryptor != null;

        public SettingsFactory(
            string globalAppName,
            TTier tier,
            TDataCenter dataCenter,
            ISettingEncryptor encryptor,
            Dictionary<Type, object> additionalDefaultConverters)
        {
            _settingsType = typeof(TSettings);
            _subAppType = typeof(TSubApp);
            _tierType = typeof(TTier);
            _dataCenterType = typeof(TDataCenter);
            AssertGenericTypesAreValid();

            GlobalAppName = globalAppName;
            Tier = tier;
            DataCenter = dataCenter;

            AssertEncryptorIsNullOrValid(encryptor);
            _encryptor = encryptor;

            if (additionalDefaultConverters != null)
            {
                foreach (var kvp in additionalDefaultConverters)
                {
                    _defaultConverters[kvp.Key] = kvp.Value;
                }
            }

            // make sure all of the converters are valid
            foreach (var kvp in _defaultConverters)
            {
                if (!IsConverterOfType(kvp.Value, kvp.Key))
                {
                    throw new InvalidSettingConverterException(
                        $"Cannot use {kvp.Value.GetType().Name} as setting converter for type {kvp.Key.Name}. The converter must implement SettingConverter<{kvp.Key.Name}>.", kvp.Key);
                }
            }

            _reflectionCache = CreateReflectionCache();

            var tree = GetSettingsTree();
            _initializer = BuildInitializer(tree);

            // don't need this cache anymore
            _reflectionCache = null;
        }

        // todo - might want to look for ways to refactor more of the common pieces between this method and TryGetSettingsBySubApp
        public InvalidSettingOverridesException TryGetSettingsForGlobalApp(out TSettings settings, [NotNull] AppSnapshot<TSubApp, TTier, TDataCenter> snapshot)
        {
            var overrides = snapshot.Overrides;
            var overridesBySubApp = overrides == null ? null : OrganizeSettingValues(overrides, defaultOnly: true);
            List<InvalidSettingValueException> exceptions = null;

            settings = _initializer(this, default(TSubApp));
            settings.SetBasicInformation(GlobalAppName, snapshot.Commit, default(TSubApp), Tier, DataCenter);

            if (overridesBySubApp != null)
            {
                Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>> globalOverrides;
                overridesBySubApp.TryGetValue(default(TSubApp), out globalOverrides);

                SetOverrides(settings, globalOverrides, null, ref exceptions);
            }

            if (exceptions != null)
                return new InvalidSettingOverridesException(exceptions, new StackTrace(true).ToString());

            return null;
        }

        public InvalidSettingOverridesException TryGetSettingsBySubApp(
            out Dictionary<TSubApp, TSettings> settingsBySubApp,
            [NotNull] TSubApp[] subApps,
            [NotNull] AppSnapshot<TSubApp, TTier, TDataCenter> snapshot)
        {
            var overrides = snapshot.Overrides;
            var overridesBySubApp = overrides == null ? null : OrganizeSettingValues(overrides, defaultOnly: false);
            settingsBySubApp = new Dictionary<TSubApp, TSettings>();
            List<InvalidSettingValueException> exceptions = null;

            foreach (var subApp in subApps)
            {
                var settings = _initializer(this, subApp);
                settings.SetBasicInformation(GlobalAppName, snapshot.Commit, subApp, Tier, DataCenter);

                if (overridesBySubApp != null)
                {
                    Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>> globalOverrides, specificSubApp;
                    overridesBySubApp.TryGetValue(default(TSubApp), out globalOverrides);

                    if (!Compare.IsDefault(subApp))
                        overridesBySubApp.TryGetValue(subApp, out specificSubApp);
                    else
                        specificSubApp = null;

                    SetOverrides(settings, globalOverrides, specificSubApp, ref exceptions);
                    SetOverrides(settings, specificSubApp, null, ref exceptions);
                }

                settingsBySubApp[subApp] = settings;
            }
            
            if (exceptions != null)
                return new InvalidSettingOverridesException(exceptions, new StackTrace(true).ToString());

            return null;
        }

        void SetOverrides(
            TSettings settings,
            [CanBeNull] Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>> overrides,
            [CanBeNull] Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>> moreSpecificOverrides,
            ref List<InvalidSettingValueException> exceptions)
        {
            if (overrides == null)
                return;

            foreach (var over in overrides.Values)
            {
                // check if there's a more specific override that will be set in the next call to this method
                if (moreSpecificOverrides != null && moreSpecificOverrides.ContainsKey(over.Name))
                    continue;

                Setting setting;
                if (!_settingsByName.TryGetValue(over.Name, out setting))
                    continue; // probably an orphaned override that should be deleted, but we'll just ignore it for now

                var strValue = setting.IsEncrypted ? Decrypt(over.Value) : over.Value;
                var ex = setting.TryApplyOverride(settings, over, strValue);

                if (ex != null)
                {
                    if (exceptions == null)
                        exceptions = new List<InvalidSettingValueException>();

                    exceptions.Add(ex);
                }
            }
        }

        public SettingInfo<TSubApp, TTier, TDataCenter>[] GetAllSettingInfos(IEnumerable<SettingValue<TSubApp, TTier, TDataCenter>> overrides = null)
        {
            Dictionary<string, List<SettingValue<TSubApp, TTier, TDataCenter>>> overrideListBySetting = null;

            if (overrides != null)
            {
                overrideListBySetting = new Dictionary<string, List<SettingValue<TSubApp, TTier, TDataCenter>>>();
                foreach (var over in overrides)
                {
                    List<SettingValue<TSubApp, TTier, TDataCenter>> overList;
                    if (!overrideListBySetting.TryGetValue(over.Name, out overList))
                        overrideListBySetting[over.Name] = overList = new List<SettingValue<TSubApp, TTier, TDataCenter>>();

                    overList.Add(over);
                }
            }

            var infos = new SettingInfo<TSubApp, TTier, TDataCenter>[_settings.Count];
            for (var i = 0; i < _settings.Count; i++)
            {
                var s = _settings[i];

                List<SettingValue<TSubApp, TTier, TDataCenter>> overList;
                if (overrideListBySetting == null || !overrideListBySetting.TryGetValue(s.Name, out overList))
                    overList = new List<SettingValue<TSubApp, TTier, TDataCenter>>();

                infos[i] = new SettingInfo<TSubApp, TTier, TDataCenter>(s.Name, s.Description, s.ChangeRequiresRestart, s.IsEncrypted, s.PropertyInfo, s.Defaults, overList);
            }

            return infos;
        }

        public string Encrypt(string plainText)
        {
            if (plainText == null)
                return null;

            return _encryptor.Encrypt(plainText);
        }

        public string Decrypt(string encrypted)
        {
            if (encrypted == null)
                return null;

            return _encryptor.Decrypt(encrypted);
        }

        public bool IsValidStringForSetting(string settingName, string str)
        {
            object o;
            return TryConvertStringToValue(settingName, str, out o);
        }

        public bool TryConvertStringToValue(string settingName, string str, out object value)
        {
            var setting = _settingsByName[settingName];

            if (setting.IsEncrypted)
            {
                try
                {
                    str = Decrypt(str);
                }
                catch
                {
                    value = null;
                    return false;
                }
            }

            return setting.TryGetValueFromString(str, out value);
        }

        public bool TryConvertValueToString(string settingName, object value, out string str)
        {
            var setting = _settingsByName[settingName];
            if (setting.TryGetStringFromValue(value, out str))
            {
                if (setting.IsEncrypted)
                {
                    try
                    {
                        str = Encrypt(str);
                    }
                    catch
                    {
                        str = null;
                        return false;
                    }
                }
            }

            return false;
        }

        public bool SettingExists(string settingName)
        {
            return _settingsByName.ContainsKey(settingName);
        }

        public bool IsEncrypted(string settingName)
        {
            return _settingsByName[settingName].IsEncrypted;
        }

        public Type GetSettingType(string settingName)
        {
            return _settingsByName[settingName].TypeOfValue;
        }

        public object GetSettingValue(TSettings obj, string settingName)
        {
            Setting setting;
            if (!_settingsByName.TryGetValue(settingName, out setting))
                throw new ArgumentException($"No setting named \"{settingName}\" exists on type {_settingsType.FullName}");

            return setting.GetValueAsObject(obj);
        }

        public TValue GetSettingValue<TValue>(TSettings obj, string settingName)
        {
            Setting setting;
            if (!_settingsByName.TryGetValue(settingName, out setting))
                throw new ArgumentException($"No setting named \"{settingName}\" exists on type {_settingsType.FullName}");

            var typedSetting = setting as Setting<TValue>;
            if (typedSetting == null)
                throw new ArgumentException($"Setting \"{settingName}\" is not of the requested type {typeof (TValue)}");

            return typedSetting.GetValue(obj);
        }

        SettingGroup GetSettingsTree()
        {
            var group = new SettingGroup(_settingsType, "", null, null);
            PopulateSettingsGroup(group);
            return group;
        }

        void PopulateSettingsGroup(SettingGroup group)
        {
            foreach (var pi in group.Type.GetProperties())
            {
                var name = group.Prefix + pi.Name;
                var propType = pi.PropertyType;

                var hasGroupAttribute = pi.GetCustomAttribute<SettingsGroupAttribute>() != null;

                var first = true;
                foreach (var sa in pi.GetCustomAttributes<SettingAttribute>())
                {
                    if (!first)
                        throw new NFigException($"Property {name} has more than one Setting or EncryptedSetting attributes.");

                    first = false;

                    if (hasGroupAttribute)
                        throw new NFigException($"Property {name} is marked as both a Setting and a SettingGroup.");

                    try
                    {
                        var toSetting = GetPropertyToSettingDelegate(pi.PropertyType);
                        var setting = toSetting(pi, sa, group);

                        group.Settings.Add(setting);
                        _settings.Add(setting);
                        _settingsByName.Add(setting.Name, setting);
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

                if (hasGroupAttribute)
                {
                    if (!propType.IsClass)
                        throw new NFigException($"Property {name} is marked with [SettingGroup], but is not a class type.");

                    var subGroup = new SettingGroup(propType, name + ".", group, pi);
                    PopulateSettingsGroup(subGroup);
                    group.SettingGroups.Add(subGroup);
                }

                // this property isn't marked with any NFig attributes, so we just ignore it
            }
        }

        Setting PropertyToSetting<TValue>(PropertyInfo pi, SettingAttribute sa, SettingGroup group)
        {
            var name = group.Prefix + pi.Name;

            var isEncrypted = sa.IsEncrypted;
            if (isEncrypted)
                AssertValidEncryptedSettingAttribute(name, sa);

            var converter = GetConverterForProperty<TValue>(name, pi);

            // meta
            var da = pi.GetCustomAttribute<DescriptionAttribute>();
            var description = da == null ? "" : da.Description;

            var changeRequiresRestart = pi.GetCustomAttribute<ChangeRequiresRestartAttribute>() != null;

            var noInline = pi.GetCustomAttribute<DoNotInlineValuesAttribute>() != null;

            // default values
            var allDefaults = new List<SettingValue<TSubApp, TTier, TDataCenter>>();
            var applicableDefaults = new List<SettingValue<TSubApp, TTier, TDataCenter>>();

            {
                // see if there are any default value attributes
                var rootDefault = isEncrypted ? default(TValue) : sa.DefaultValue;
                var defaultStringValue = GetStringFromDefaultAndValidate(name, rootDefault, default(TSubApp), default(TDataCenter), converter, isEncrypted);

                var d = new SettingValue<TSubApp, TTier, TDataCenter>(
                    name,
                    defaultStringValue,
                    default(TSubApp),
                    default(TTier),
                    default(TDataCenter),
                    true,
                    true
                );

                allDefaults.Add(d);
                applicableDefaults.Add(d);
            }

            foreach (var dsva in pi.GetCustomAttributes<DefaultSettingValueAttribute>())
            {
                TSubApp subApp;
                TTier tier;
                TDataCenter dc;
                GetSubAppTierDataCenterFromAttribute(name, dsva, out subApp, out tier, out dc);

                var dsvaDefault = dsva.DefaultValue;

                if (isEncrypted)
                {
                    if (Compare.IsDefault(tier))
                        throw new NFigException($"{name} has a default without a tier. Additional default values for encrypted settings must include a non-\"Any\" tier.");

                    if (dsvaDefault != null && !(dsvaDefault is string))
                        throw new NFigException($"{name} has a non-string default. Encrypted defaults must be in string representation.");
                }

                // if it's not the Any tier, and not the current tier, then we don't care about this default
                var skip = !Compare.IsDefault(tier) && !Compare.AreEqual(tier, Tier);

                // Even if we're skipping this default, performing validation for all tiers is useful.
                // However, if the value is encrypted, we only want to perform the validation for the current tier.
                if (!skip || !isEncrypted)
                {
                    var defaultStringValue = GetStringFromDefaultAndValidate(name, dsvaDefault, subApp, dc, converter, isEncrypted);

                    // create default
                    var d = new SettingValue<TSubApp, TTier, TDataCenter>(
                        name,
                        defaultStringValue,
                        subApp,
                        tier,
                        dc,
                        true,
                        dsva.AllowOverrides
                    );

                    // make sure there isn't a conflicting default value
                    foreach (var existing in allDefaults)
                    {
                        if (existing.HasSameSubAppTierDataCenter(d))
                            throw new NFigException($"Multiple defaults were specified for the same environment ({subApp}/{tier}/{dc}) on setting {name}");
                    }

                    allDefaults.Add(d);

                    if (!skip)
                        applicableDefaults.Add(d);
                }
            }

            return new Setting<TValue>(name, description, changeRequiresRestart, isEncrypted, pi, group, applicableDefaults.ToArray(), converter, noInline);
        }

        ISettingConverter<TValue> GetConverterForProperty<TValue>(string name, PropertyInfo pi)
        {
            // see if there is a converter specified
            var converterAttribute = pi.GetCustomAttribute<SettingConverterAttribute>();
            object convObj;

            if (converterAttribute != null)
            {
                if (pi.GetCustomAttributes<SettingConverterAttribute>().Count() != 1)
                {
                    throw new NFigException($"More than one SettingConverterAttribute was specified for \"{name}\"");
                }

                convObj = converterAttribute.Converter;
            }
            else
            {
                // use the default converter
                if (!_defaultConverters.TryGetValue(pi.PropertyType, out convObj))
                {
                    var tValueType = typeof(TValue);
                    if (tValueType.IsEnum)
                    {
                        if (!tValueType.IsPublic && !tValueType.IsNestedPublic)
                            throw new InvalidSettingConverterException($"Cannot create converter for enum type \"{tValueType.Name}\" because it is not public.", tValueType);

                        convObj = EnumConverters.GetConverter<TValue>();
                    }
                    else
                    {
                        throw new InvalidSettingConverterException($"No default converter is available for setting \"{name}\" of type {pi.PropertyType.Name}", pi.PropertyType);
                    }
                }
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

        void AssertValidEncryptedSettingAttribute(string name, SettingAttribute sa)
        {
            if (_encryptor == null)
                throw new NFigException($"Setting {name} is marked as encrypted, but no ISettingEncryptor was provided to the NFigStore.");

            if (sa.DefaultValue != null)
                throw new NFigException($"The SettingAttribute for {name} assigns a default value and is marked as encrypted. It cannot have both. " +
                                        $"This error is probably due to a class inheriting from SettingAttribute without obeying this rule.");
        }

        void GetSubAppTierDataCenterFromAttribute(string name, DefaultSettingValueAttribute dsva, out TSubApp subApp, out TTier tier, out TDataCenter dataCenter)
        {
            if (dsva.SubApp != null)
            {
                if (!(dsva.SubApp is TSubApp))
                    throw new NFigException($"The subApp argument was not of type {_subAppType.Name} on setting \"{name}\"");

                subApp = (TSubApp)dsva.SubApp;
            }
            else
            {
                subApp = default(TSubApp);
            }

            if (dsva.Tier != null)
            {
                if (!(dsva.Tier is TTier))
                    throw new NFigException($"The tier argument was not of type {_tierType.Name} on setting \"{name}\"");

                tier = (TTier)dsva.Tier;
            }
            else
            {
                tier = default(TTier);
            }

            if (dsva.DataCenter != null)
            {
                if (!(dsva.DataCenter is TDataCenter))
                    throw new NFigException($"The dataCenter argument was not of type {_dataCenterType.Name} on setting \"{name}\"");

                dataCenter = (TDataCenter)dsva.DataCenter;
            }
            else
            {
                dataCenter = default(TDataCenter);
            }
        }

        string GetStringFromDefaultAndValidate<TValue>(
            string name,
            object value,
            TSubApp subApp,
            TDataCenter dataCenter,
            ISettingConverter<TValue> converter,
            bool isEncrypted)
        {
            string stringValue;

            if (value is string && (isEncrypted || typeof (TValue) != typeof (string)))
            {
                // Don't need to convert to a string if value is already a string and TValue is not.
                // We expect that the human essentially already did the conversion.
                // Also, if setting is encrypted, then we always expect the string representation to be encrypted.
                stringValue = (string) value;
            }
            else
            {
                try
                {
                    // try convert the real value into its string representation
                    var tval = value is TValue ? (TValue)value : (TValue)Convert.ChangeType(value, typeof(TValue));
                    stringValue = converter.GetString(tval);

                    if (isEncrypted)
                        stringValue = Encrypt(stringValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidSettingValueException(
                        $"Invalid default for setting \"{name}\". Cannot convert to a string representation.",
                        name,
                        value,
                        true,
                        subApp.ToString(),
                        dataCenter.ToString(),
                        ex);
                }
            }

            // now make sure we can also convert the string value back into a real value
            try
            {
                var decrypted = isEncrypted ? Decrypt(stringValue) : stringValue;
                converter.GetValue(decrypted);
            }
            catch (Exception ex)
            {
                throw new InvalidSettingValueException(
                    $"Invalid default value for setting \"{name}\". Cannot convert string representation back into a real value.",
                    name,
                    value,
                    true,
                    subApp.ToString(),
                    dataCenter.ToString(),
                    ex);
            }

            return stringValue;
        }

        /// <summary>
        /// Builds a dynamic method which, when called, will create a new TSettings object with all of its defaults values properly set.
        /// </summary>
        InitializeSettingsDelegate BuildInitializer(SettingGroup group)
        {
            var dm = new DynamicMethod("TSettings_Instantiate", _settingsType, new [] { GetType(), _subAppType }, _settingsType.Module, true);
            var il = dm.GetILGenerator();

            EmitNewGroupObject(il, group); // [TSettings s]
            EmitBestDefaults(il);          // [TSettings s]
            il.Emit(OpCodes.Ret);

            return (InitializeSettingsDelegate)dm.CreateDelegate(typeof(InitializeSettingsDelegate));
        }

        /// <summary>
        /// Emits IL for each setting group object that needs to be initialized, and properly assigns them to properties on parent groups as applicable.
        /// </summary>
        static void EmitNewGroupObject(ILGenerator il, SettingGroup group)
        {
            // Initial stack: ...
            // End stack:     ... [group]

            // Create a new instance of the group's class.
            var ctor = group.Type.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
                throw new NFigException($"Cannot use type {group.Type.Name} for settings groups. It does not have a parameterless constructor.");

            il.Emit(OpCodes.Newobj, ctor); // [group]

            foreach (var subGroup in group.SettingGroups)
            {
                il.Emit(OpCodes.Dup);                                       // [group] [group]
                EmitNewGroupObject(il, subGroup);                           // [group] [group] [sub]
                il.Emit(OpCodes.Callvirt, subGroup.PropertyInfo.SetMethod); // [group]
            }
        }

        /// <summary>
        /// Sets up the proper constructs for apply the correct defaults based on the selected sub app.
        /// </summary>
        void EmitBestDefaults(ILGenerator il)
        {
            // Initial stack: [TSettings s]
            // End stack:     [TSettings s]

            var loadSubAppArg = OpCodes.Ldarg_1;

            var bestDefaults = GetBestDefaults();

            // first thing we want to do is emit the list of defaults for the default sub app
            EmitDefaultList(il, bestDefaults[default(TSubApp)]); // [s]

            // check if there are any known sub apps
            var subAppCount = bestDefaults.Count - 1;
            if (subAppCount < 1)
                return;

            var subApps = new TSubApp[subAppCount];
            var subAppValues = new int[subAppCount];

            {
                var i = 0;
                foreach (var subApp in bestDefaults.Keys)
                {
                    if (!Compare.IsDefault(subApp))
                    {
                        subApps[i] = subApp;
                        subAppValues[i] = Convert.ToInt32(subApp);
                        i++;
                    }
                }
            }

            Array.Sort(subAppValues, subApps);

            // Decide if we can/should use a jump table or not. Basic heuristic is to use a jump list if all of the following criteria is met:
            // - A count of at least five sub apps.
            // - The number of gaps between the lowest and highest value is not greater than the count.
            // - There are no negative values.

            var endOfSubApps = il.DefineLabel();

            var firstValue = subAppValues[0];
            var lastValue = subAppValues[subAppCount - 1];
            if (subAppCount >= 5 && firstValue > 0 && subAppCount >= (lastValue - firstValue) / 2)
            {
                // set up jump table
                var labelCount = lastValue - firstValue + 1;
                var jumpLabels = new Label[labelCount];
                var subAppLabels = new Label[subAppCount];

                var expectedValue = subAppValues[0];
                var si = 0;
                for (var li = 0; li < labelCount; li++, expectedValue++)
                {
                    if (si < subAppCount && subAppValues[si] == expectedValue)
                    {
                        var label = new Label();
                        subAppLabels[si] = label;
                        jumpLabels[li] = label;
                        si++;
                    }
                    else
                    {
                        jumpLabels[li] = endOfSubApps;
                    }
                }

                // emit switch
                il.Emit(loadSubAppArg);              // [s] [subApp]
                il.Emit(OpCodes.Ldc_I4, firstValue); // [s] [subApp] [firstValue]
                il.Emit(OpCodes.Sub);                // [s] [subApp - firstValue]
                il.Emit(OpCodes.Switch, jumpLabels); // [s]
                il.Emit(OpCodes.Br, endOfSubApps);   // [s] -- this is the fallthrough case

                // emit the case statements
                for (var i = 0; i < subAppCount; i++)
                {
                    il.MarkLabel(subAppLabels[i]);

                    var list = bestDefaults[subApps[i]];
                    EmitDefaultList(il, list);         // [s]
                    il.Emit(OpCodes.Br, endOfSubApps); // [s]
                }
            }
            else
            {
                // use if/else style
                for (var i = 0; i < subAppCount; i++)
                {
                    var endIfLabel = il.DefineLabel();

                    // if (subApp == knownSubApp)
                    il.Emit(loadSubAppArg);                   // [s] [subApp]
                    il.Emit(OpCodes.Ldc_I4, subAppValues[i]); // [s] [subApp] [known subApp]
                    il.Emit(OpCodes.Ceq);                     // [s] [are equal]
                    il.Emit(OpCodes.Brfalse, endIfLabel);     // [s]

                    // body of if
                    var list = bestDefaults[subApps[i]];
                    EmitDefaultList(il, list);         // [s]
                    il.Emit(OpCodes.Br, endOfSubApps); // [s]

                    il.MarkLabel(endIfLabel);
                }
            }

            il.MarkLabel(endOfSubApps);
        }

        void EmitDefaultList(ILGenerator il, List<BestDefault> list)
        {
            // Initial stack: [TSettings s]
            // End stack:     [TSettings s]

            SettingGroup group = null;
            var settings = _settings;

            il.Emit(OpCodes.Dup); // [s] [s]

            foreach (var best in list)
            {
                var setting = settings[best.SettingIndex];
                var def = setting.Defaults[best.DefaultIndex];

                if (setting.Group != group)
                {
                    group = setting.Group;
                    il.Emit(OpCodes.Pop);     // [s]
                    il.Emit(OpCodes.Dup);     // [s] [s]
                    EmitLoadGroup(il, group); // [s] [group]
                }

                EmitSetting(il, setting, best.SettingIndex, def); // [s] [group]
            }

            il.Emit(OpCodes.Pop); // [s]
        }

        static void EmitLoadGroup(ILGenerator il, SettingGroup group)
        {
            // Initial stack: ... [s]
            // End stack:     ... [group]

            if (group.Parent == null) // there is no parent, so the TSettings object is actually the group object we want, which is what's on the stack already
                return;

            var methodList = new List<MethodInfo>();
            var g = group;
            do
            {
                methodList.Add(g.PropertyInfo.GetMethod);
                g = g.Parent;

            } while (g != null && g.PropertyInfo != null);
            
            // the list was built bottom up, but we need to emit top down, so we go in reverse
            for (var i = methodList.Count - 1; i >= 0; i--)
            {
                il.Emit(OpCodes.Callvirt, methodList[i]); // ... [group]
            }
        }

        void EmitSetting(ILGenerator il, Setting setting, int settingIndex, SettingValue<TSubApp, TTier, TDataCenter> sv)
        {
            // Initial stack: [s] [group]
            // End stack:     [s] [group]

            var loadFactoryArg = OpCodes.Ldarg_0;
            var typeOfValue = setting.TypeOfValue;
            var strValue = setting.IsEncrypted ? Decrypt(sv.Value) : sv.Value;

            il.Emit(OpCodes.Dup); // [s] [group] [group]

            if (setting.DoNotInlineValues) // we have to call the converter each time
            {
                var converterType = typeof(ISettingConverter<>).MakeGenericType(typeOfValue);
                var getValue = converterType.GetMethod(nameof(ISettingConverter<bool>.GetValue)); // the <bool> here really doesn't matter - any type would work

                var settingType = typeof(Setting<>).MakeGenericType(typeOfValue);
                var converterProp = settingType.GetProperty(nameof(Setting<bool>.Converter)); // the <bool> here really doesn't matter - any type would work

                var settingsField = _reflectionCache.SettingsField;
                var getSettingItem = _reflectionCache.GetSettingItemMethod;
                
                il.Emit(loadFactoryArg);                            // [s] [group] [group] [this]
                il.Emit(OpCodes.Ldfld, settingsField);              // [s] [group] [group] [_settings]
                il.Emit(OpCodes.Ldc_I4, settingIndex);              // [s] [group] [group] [_settings] [index]
                il.Emit(OpCodes.Callvirt, getSettingItem);          // [s] [group] [group] [setting]
                il.Emit(OpCodes.Callvirt, converterProp.GetMethod); // [s] [group] [group] [Converter]
                il.Emit(OpCodes.Ldstr, strValue);                   // [s] [group] [group] [Converter] [strValue]
                il.Emit(OpCodes.Callvirt, getValue);                // [s] [group] [group] [valueToSet]
            }
            else // we can use a cached value each time
            {
                var objValue = setting.GetValueFromString(strValue);

                if (objValue == null)
                {
                    il.Emit(OpCodes.Ldnull); // [s] [group] [group] [null valueToSet]
                }
                else
                {
                    var strategy = GetInlineStrategyForType(typeOfValue);

                    switch (strategy)
                    {
                        case InlineStrategy.Cache:
                            var index = AddToValueCache(objValue);
                            var cacheField = _reflectionCache.ValueCacheField;
                            il.Emit(loadFactoryArg);            // [s] [group] [group] [this]
                            il.Emit(OpCodes.Ldfld, cacheField); // [s] [group] [group] [_valueCache]
                            il.Emit(OpCodes.Ldc_I4, index);     // [s] [group] [group] [_valueCache] [index]
                            il.Emit(OpCodes.Ldelem_Ref);        // [s] [group] [group] [valueToSet]
                            if (typeOfValue.IsValueType)
                                il.Emit(OpCodes.Unbox);         // [s] [group] [group] [unboxed valueToSet]
                            break;
                        case InlineStrategy.String:
                            il.Emit(OpCodes.Ldstr, (string)objValue); // [s] [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Int32:
                            il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(objValue)); // [s] [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Int64:
                            il.Emit(OpCodes.Ldc_I8, Convert.ToInt64(objValue)); // [s] [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Float32:
                            il.Emit(OpCodes.Ldc_R4, Convert.ToSingle(objValue)); // [s] [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Float64:
                            il.Emit(OpCodes.Ldc_R8, Convert.ToDouble(objValue)); // [s] [group] [group] [valueToSet]
                            break;
                        case InlineStrategy.Char:
                            il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(objValue)); // [s] [group] [group] [valueToSet]
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            // stack should be: [s] [group] [group] [valueToSet]

            il.Emit(OpCodes.Callvirt, setting.PropertyInfo.SetMethod); // [s] [group]
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

            if (type.IsEnum)
                type = type.GetEnumUnderlyingType();

            if (!type.IsPrimitive)
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

        Dictionary<TSubApp, List<BestDefault>> GetBestDefaults()
        {
            var best = new Dictionary<TSubApp, List<BestDefault>>();
            best[default(TSubApp)] = new List<BestDefault>();

            var foundSubApps = new HashSet<TSubApp>();

            var settings = _settings;
            var tier = Tier;
            var dataCenter = DataCenter;

            for (var i = 0; i < settings.Count; i++)
            {
                var s = settings[i];
                var defaults = s.Defaults;

                foreach (var def in defaults)
                {
                    if (def.IsValidFor(def.SubApp, tier, dataCenter))
                        foundSubApps.Add(def.SubApp);
                }

                if (!foundSubApps.Contains(default(TSubApp)))
                    throw new NFigException($"Setting {s.Name} has no default value for the default sub app. This indicates a bug in NFig.");

                foreach (var subApp in foundSubApps)
                {
                    List<BestDefault> list;
                    if (!best.TryGetValue(subApp, out list))
                    {
                        list = new List<BestDefault>();
                        best[subApp] = list;
                    }

                    SettingValue<TSubApp, TTier, TDataCenter> _;
                    var defIndex = defaults.GetBestValueFor(subApp, tier, dataCenter, out _);
                    list.Add(new BestDefault(i, defIndex));
                }

                foundSubApps.Clear();
            }

            return best;
        }

        static bool IsConverterOfType(object converter, Type type)
        {
            if (converter == null)
                return false;

            // make sure type implements SettingsConverter<>
            var genericType = typeof(ISettingConverter<>);
            foreach (var iface in converter.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericType)
                {
                    return iface.GenericTypeArguments[0] == type;
                }
            }

            return false;
        }

        Dictionary<TSubApp, Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>>> OrganizeSettingValues(
            IEnumerable<SettingValue<TSubApp, TTier, TDataCenter>> values,
            bool defaultOnly)
        {
            var bySubApp = new Dictionary<TSubApp, Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>>>();

            var tier = Tier;
            var dataCenter = DataCenter;

            foreach (var value in values)
            {
                var subApp = value.SubApp;

                if (defaultOnly && !Compare.IsDefault(subApp))
                    continue;

                if (!value.IsValidFor(subApp, tier, dataCenter))
                    continue;

                Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>> bySettingName;
                if (!bySubApp.TryGetValue(subApp, out bySettingName))
                {
                    bySettingName = new Dictionary<string, SettingValue<TSubApp, TTier, TDataCenter>>();
                    bySubApp[subApp] = bySettingName;
                }

                SettingValue<TSubApp, TTier, TDataCenter> existingValue;
                if (bySettingName.TryGetValue(value.Name, out existingValue))
                {
                    if (!value.IsMoreSpecificThan(existingValue))
                        continue;
                }

                bySettingName[value.Name] = value;
            }

            return bySubApp;
        }

        static void AssertEncryptorIsNullOrValid(ISettingEncryptor encryptor)
        {
            // null is perfectly valid if they're not using encrypted settings (validated later)
            if (encryptor == null)
                return;

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

        void AssertGenericTypesAreValid()
        {
            var subType = _subAppType;
            if (subType.IsEnum)
                subType = subType.GetEnumUnderlyingType();

            if (!IsValidSubAppType(subType))
                throw new InvalidOperationException("TSubApp must be an enum or integer (32-bits or smaller).");

            if (!_tierType.IsEnum || !_dataCenterType.IsEnum)
                throw new InvalidOperationException("TTier and TDataCenter must be enum types.");
        }

        static bool IsValidSubAppType(Type type)
        {
            return type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint);
        }

        ReflectionCache CreateReflectionCache()
        {
            var cache = new ReflectionCache();
            var thisType = GetType();

            cache.SettingsField = thisType.GetField(nameof(_settings), BindingFlags.NonPublic | BindingFlags.Instance);
            cache.ValueCacheField = thisType.GetField(nameof(_valueCache), BindingFlags.NonPublic | BindingFlags.Instance);
            cache.GetSettingItemMethod = _settings.GetType().GetProperty("Item").GetMethod;
            cache.PropertyToSettingMethod = thisType.GetMethod(nameof(PropertyToSetting), BindingFlags.NonPublic | BindingFlags.Instance);
            cache.PropertyToSettingDelegates = new Dictionary<Type, PropertyToSettingDelegate>();

            return cache;
        }

        PropertyToSettingDelegate GetPropertyToSettingDelegate(Type type)
        {
            PropertyToSettingDelegate del;
            if (_reflectionCache.PropertyToSettingDelegates.TryGetValue(type, out del))
                return del;

            var methodInfo = _reflectionCache.PropertyToSettingMethod.MakeGenericMethod(type);
            del = (PropertyToSettingDelegate)Delegate.CreateDelegate(typeof(PropertyToSettingDelegate), this, methodInfo);
            _reflectionCache.PropertyToSettingDelegates[type] = del;

            return del;
        }


        /**************************************************************************************
         * Helper Classes and Delegates
         *************************************************************************************/

        delegate TSettings InitializeSettingsDelegate(SettingsFactory<TSettings, TSubApp, TTier, TDataCenter> factory, TSubApp subApp);

        delegate void SettingSetterDelegate<TValue>(TSettings settings, TValue value);

        delegate TValue SettingGetterDelegate<TValue>(TSettings settings);

        class ReflectionCache
        {
            public FieldInfo SettingsField;
            public FieldInfo ValueCacheField;
            public MethodInfo GetSettingItemMethod;
            public MethodInfo PropertyToSettingMethod;
            public Dictionary<Type, PropertyToSettingDelegate> PropertyToSettingDelegates;

        }

        struct BestDefault
        {
            public int SettingIndex { get; }
            public int DefaultIndex { get; }

            public BestDefault(int settingIndex, int defaultIndex)
            {
                SettingIndex = settingIndex;
                DefaultIndex = defaultIndex;
            }
        }

        class SettingGroup
        {
            public Type Type { get; }
            public string Prefix { get; }
            public SettingGroup Parent { get; }
            public PropertyInfo PropertyInfo { get; }
            public List<SettingGroup> SettingGroups { get; }
            public List<Setting> Settings { get; }

            public SettingGroup(Type type, string prefix, SettingGroup parent, PropertyInfo pi)
            {
                Type = type;
                Prefix = prefix;
                Parent = parent;
                PropertyInfo = pi;

                SettingGroups = new List<SettingGroup>();
                Settings = new List<Setting>();
            }
        }

        abstract class Setting
        {
            public string Name { get; protected set; }
            public string Description { get; protected set; }
            public bool ChangeRequiresRestart { get; protected set; }
            public bool IsEncrypted { get; protected set; }
            public PropertyInfo PropertyInfo { get; protected set; }
            public SettingValue<TSubApp, TTier, TDataCenter>[] Defaults { get; protected set; }
            public Type TypeOfValue { get; protected set; }
            public SettingGroup Group { get; protected set; }
            public bool DoNotInlineValues { get; protected set; }

            public abstract object GetValueAsObject(TSettings settings);
            public abstract InvalidSettingValueException TryApplyOverride(TSettings settings, SettingValue<TSubApp, TTier, TDataCenter> over, string strValue);
            public abstract object GetValueFromString(string str);
            public abstract bool TryGetValueFromString(string str, out object value);
            public abstract bool TryGetStringFromValue(object value, out string str);
        }

        class Setting<TValue> : Setting
        {
            SettingGetterDelegate<TValue> _getter;
            SettingSetterDelegate<TValue> _setter;

            public ISettingConverter<TValue> Converter { get; }

            public Setting(
                string name,
                string description,
                bool changeRequiresRestart,
                bool isEncrypted,
                PropertyInfo propertyInfo,
                SettingGroup group,
                SettingValue<TSubApp, TTier, TDataCenter>[] defaults,
                ISettingConverter<TValue> converter,
                bool noInline
            )
            {
                Name = name;
                Description = description;
                ChangeRequiresRestart = changeRequiresRestart;
                IsEncrypted = isEncrypted;
                PropertyInfo = propertyInfo;
                Group = group;
                Defaults = defaults;
                Converter = converter;
                DoNotInlineValues = noInline;

                TypeOfValue = typeof(TValue);
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

            public override InvalidSettingValueException TryApplyOverride(TSettings settings, SettingValue<TSubApp, TTier, TDataCenter> over, string strValue)
            {
                TValue value;

                try
                {
                    value = Converter.GetValue(strValue);
                }
                catch (Exception ex)
                {
                    var invalidEx = new InvalidSettingValueException(
                        $"Invalid override value for setting \"{Name}\". Cannot convert the string override to a real value.",
                        Name,
                        over.Value, // intentionally using over.Value here instead of strValue since strValue could be a decrypted value that people don't want in their logs
                        false,
                        over.SubApp.ToString(),
                        over.DataCenter.ToString(),
                        ex);

                    invalidEx.UnthrownStackTrace = new StackTrace(true).ToString();

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

            SettingSetterDelegate<TValue> CreateSetterMethod()
            {
                var dm = new DynamicMethod("AssignSetting_" + Name, null, new[] { typeof(TSettings), TypeOfValue }, GetType().Module, true);
                var il = dm.GetILGenerator();

                // arg 0 = TSettings settings
                // arg 1 = TValue value
                
                il.Emit(OpCodes.Ldarg_0);                          // [settings]
                EmitLoadGroup(il, Group);                          // [group]
                il.Emit(OpCodes.Ldarg_1);                          // [group] [value]
                il.Emit(OpCodes.Callvirt, PropertyInfo.SetMethod); // empty
                il.Emit(OpCodes.Ret);

                return (SettingSetterDelegate<TValue>)dm.CreateDelegate(typeof(SettingSetterDelegate<TValue>));
            }

            SettingGetterDelegate<TValue> CreateGetterMethod()
            {
                var dm = new DynamicMethod("RetrieveSetting_" + Name, typeof(TValue), new[] { typeof(TSettings) }, GetType().Module, true);
                var il = dm.GetILGenerator();

                // arg 0 = TSettings settings
                
                il.Emit(OpCodes.Ldarg_0);                          // [settings]
                EmitLoadGroup(il, Group);                          // [group]
                il.Emit(OpCodes.Callvirt, PropertyInfo.GetMethod); // [value]
                il.Emit(OpCodes.Ret);

                return (SettingGetterDelegate<TValue>)dm.CreateDelegate(typeof(SettingGetterDelegate<TValue>));
            }
        }
    }
}
