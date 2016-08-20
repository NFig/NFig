using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NFig.Encryption;

namespace NFig
{
    class SettingsFactory<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        public string ApplicationName { get; }
        public TTier Tier { get; }
        public TDataCenter DataCenter { get; }

        readonly Setting[] _settings;
        readonly Dictionary<string, Setting> _settingsByName;
        readonly InitializeSettingsDelegate _initializer;
        readonly Type TSettingsType;
        readonly Type TTierType;
        readonly Type TDataCenterType;

        readonly ISettingEncryptor _encryptor;

        readonly Dictionary<Type, PropertyToSettingDelegate> _propertyToSettingDelegatesCache;
        readonly object _delegatesCacheLock;

        delegate Setting PropertyToSettingDelegate(PropertyInfo pi, PropertyAndParent parent, SettingAttribute sa, string prefix);

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

        public bool HasEncryptor => _encryptor != null;

        public SettingsFactory(
            string appName,
            TTier tier,
            TDataCenter dataCenter,
            ISettingEncryptor encryptor,
            Dictionary<Type, object> additionalDefaultConverters)
        {
            TSettingsType = typeof(TSettings);
            TTierType = typeof(TTier);
            TDataCenterType = typeof(TDataCenter);

            ApplicationName = appName;
            Tier = tier;
            DataCenter = dataCenter;

            AssertEncryptorIsNullOrValid(encryptor);
            _encryptor = encryptor;

            if (!TTierType.IsEnum || !TDataCenterType.IsEnum)
                throw new InvalidOperationException("TTier and TDataCenter must be enum types.");

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

            _propertyToSettingDelegatesCache = new Dictionary<Type, PropertyToSettingDelegate>();
            _delegatesCacheLock = new object();

            _settings = BuildSettings(TSettingsType);
            _settingsByName = _settings.ToDictionary(s => s.Name);
            _initializer = GetInitializer();

            // don't need this cache anymore
            _propertyToSettingDelegatesCache = null;
            _delegatesCacheLock = null;
        }

        public TSettings GetAppSettings(IEnumerable<SettingValue<TTier, TDataCenter>> overrides = null)
        {
            TSettings settings;
            var ex = TryGetAppSettings(out settings, overrides);
            if (ex != null)
                throw ex;

            return settings;
        }

        public InvalidSettingOverridesException TryGetAppSettings
            (out TSettings settings, IEnumerable<SettingValue<TTier, TDataCenter>> overrides = null)
        {
            var tier = Tier;
            var dataCenter = DataCenter;

            // pick the right overrides
            Dictionary<string, SettingValue<TTier, TDataCenter>> overridesBySetting = null;
            List<InvalidSettingValueException> exceptions = null;

            if (overrides != null)
            {
                overridesBySetting = new Dictionary<string, SettingValue<TTier, TDataCenter>>();
                foreach (var over in overrides)
                {
                    if (over.IsValidFor(tier, dataCenter))
                    {
                        SettingValue<TTier, TDataCenter> prev;
                        if (overridesBySetting.TryGetValue(over.Name, out prev))
                        {
                            if (over.IsMoreSpecificThan(prev))
                                overridesBySetting[over.Name] = over;
                        }
                        else
                        {
                            overridesBySetting[over.Name] = over;
                        }
                    }
                }
            }

            var s = _initializer();
            settings = s;
            s.Tier = tier;
            s.DataCenter = dataCenter;

            foreach (var setting in _settings)
            {
                var settingValue = SettingInfo<TTier, TDataCenter>.GetBestValueFor(setting.Defaults, tier, dataCenter);

                string stringValue;
                SettingValue<TTier, TDataCenter> over;
                if (settingValue.AllowsOverrides && overridesBySetting != null && overridesBySetting.TryGetValue(setting.Name, out over))
                {
                    try
                    {
                        stringValue = setting.IsEncrypted ? Decrypt(over.Value) : over.Value;
                        setting.SetValueFromString(s, stringValue);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        var invalidEx = new InvalidSettingValueException(
                            $"Invalid override value for setting \"{setting.Name}\". Cannot convert the string override to a real value.",
                            setting.Name,
                            over.Value,
                            false,
                            over.DataCenter.ToString(),
                            ex);

                        invalidEx.UnthrownStackTrace = new StackTrace(true).ToString();

                        if (exceptions == null)
                            exceptions = new List<InvalidSettingValueException>();

                        exceptions.Add(invalidEx);
                    }
                }

                stringValue = setting.IsEncrypted ? Decrypt(settingValue.Value) : settingValue.Value;
                setting.SetValueFromString(s, stringValue);
            }

            if (exceptions != null)
                return new InvalidSettingOverridesException(exceptions, new StackTrace(true).ToString());

            return null;
        }

        public SettingInfo<TTier, TDataCenter>[] GetAllSettingInfos(IEnumerable<SettingValue<TTier, TDataCenter>> overrides = null)
        {
            Dictionary<string, List<SettingValue<TTier, TDataCenter>>> overrideListBySetting = null;

            if (overrides != null)
            {
                overrideListBySetting = new Dictionary<string, List<SettingValue<TTier, TDataCenter>>>();
                foreach (var over in overrides)
                {
                    List<SettingValue<TTier, TDataCenter>> overList;
                    if (!overrideListBySetting.TryGetValue(over.Name, out overList))
                        overrideListBySetting[over.Name] = overList = new List<SettingValue<TTier, TDataCenter>>();

                    overList.Add(over);
                }
            }

            var infos = new SettingInfo<TTier, TDataCenter>[_settings.Length];
            for (var i = 0; i < _settings.Length; i++)
            {
                var s = _settings[i];

                List<SettingValue<TTier, TDataCenter>> overList;
                if (overrideListBySetting == null || !overrideListBySetting.TryGetValue(s.Name, out overList))
                    overList = new List<SettingValue<TTier, TDataCenter>>();

                infos[i] = new SettingInfo<TTier, TDataCenter>(s.Name, s.Description, s.ChangeRequiresRestart, s.IsEncrypted, s.PropertyInfo, s.Defaults, overList);
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
            return _settingsByName[settingName].PropertyInfo.PropertyType;
        }

        public object GetSettingValue(TSettings obj, string settingName)
        {
            Setting setting;
            if (!_settingsByName.TryGetValue(settingName, out setting))
                throw new ArgumentException($"No setting named \"{settingName}\" exists on type {TSettingsType.FullName}");

            return setting.GetValue(obj);
        }

        public TValue GetSettingValue<TValue>(TSettings obj, string settingName)
        {
            Setting setting;
            if (!_settingsByName.TryGetValue(settingName, out setting))
                throw new ArgumentException($"No setting named \"{settingName}\" exists on type {TSettingsType.FullName}");

            var typedSetting = setting as Setting<TValue>;
            if (typedSetting == null)
                throw new ArgumentException($"Setting \"{settingName}\" is not of the requested type {typeof (TValue)}");

            return typedSetting.Getter(obj);
        }

        Setting[] BuildSettings(Type type)
        {
            return type.GetProperties().Select(pi => GetSettingsFromProperty(pi, null, "")).SelectMany(s => s).ToArray();
        }

        IEnumerable<Setting> GetSubSettings(Type type, PropertyAndParent parent, string prefix)
        {
            return type.GetProperties().Select(pi => GetSettingsFromProperty(pi, parent, prefix)).SelectMany(s => s);
        }

        IEnumerable<Setting> GetSettingsFromProperty(PropertyInfo pi, PropertyAndParent parent, string prefix)
        {
            var settingAttributes = pi.GetCustomAttributes<SettingAttribute>().ToList();
            if (settingAttributes.Count > 0)
            {
                if (settingAttributes.Count > 1)
                    throw new NFigException($"Only one Setting or EncryptedSetting attributes may be applied to a property. {prefix}{pi.Name} has {settingAttributes.Count}");

                try
                {
                    var toSetting = GetPropertyToSettingDelegate(pi.PropertyType);
                    var setting = toSetting(pi, parent, settingAttributes[0], prefix);
                    return SelectSingle(setting);
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

            if (IsSettingsGroup(pi))
            {
                parent = new PropertyAndParent { Parent = parent, PropertyInfo = pi };
                return GetSubSettings(pi.PropertyType, parent, pi.Name + ".");
            }

            return Enumerable.Empty<Setting>();
        }

        PropertyToSettingDelegate GetPropertyToSettingDelegate(Type type)
        {
            PropertyToSettingDelegate del;
            if (_propertyToSettingDelegatesCache.TryGetValue(type, out del))
                return del;

            lock (_delegatesCacheLock)
            {
                if (_propertyToSettingDelegatesCache.TryGetValue(type, out del))
                    return del;

                var methodInfo = GetType().GetMethod(nameof(PropertyToSetting), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(type);
                del = (PropertyToSettingDelegate)Delegate.CreateDelegate(typeof(PropertyToSettingDelegate), this, methodInfo);
                _propertyToSettingDelegatesCache[type] = del;

                return del;
            }
        }

        Setting PropertyToSetting<TValue>(PropertyInfo pi, PropertyAndParent parent, SettingAttribute sa, string prefix)
        {
            var name = prefix + pi.Name;

            var isEncrypted = sa.IsEncrypted;
            if (isEncrypted)
            {
                if (_encryptor == null)
                    throw new NFigException($"Setting {name} is marked as encrypted, but no ISettingEncryptor was provided to the NFigStore.");

                if (sa.DefaultValue != null)
                    throw new NFigException($"Encrypted setting {name} has a non-null default value for Any/Any (tier/data center)");
            }

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
                    var tValueType = typeof (TValue);
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

            // description
            var da = pi.GetCustomAttribute<DescriptionAttribute>();
            var description = da == null ? "" : da.Description;

            // change requires restart
            var changeRequiresRestart = pi.GetCustomAttribute<ChangeRequiresRestartAttribute>() != null;

            // see if there are any default value attributes
            var defaults = new List<SettingValue<TTier, TDataCenter>>();
            var anyAnyValue = isEncrypted ? default(TValue) : sa.DefaultValue;
            var defaultStringValue = GetStringFromDefaultAndValidate(name, anyAnyValue, default(TTier), default(TDataCenter), converter, isEncrypted);
            defaults.Add(new SettingValue<TTier, TDataCenter>(name, defaultStringValue, default(TTier), default(TDataCenter), true, true));
            
            foreach (var dsva in pi.GetCustomAttributes<DefaultSettingValueAttribute>())
            {
                TTier? tier = null;
                TDataCenter? dc = null;

                // type check the "object" properties of the attribute
                if (dsva.Tier != null)
                {
                    if (!(dsva.Tier is TTier))
                        throw new NFigException($"The tier argument was not of type {TTierType.Name} on setting \"{name}\"");

                    tier = (TTier)dsva.Tier;
                }

                if (dsva.DataCenter != null)
                {
                    if (!(dsva.DataCenter is TDataCenter))
                        throw new NFigException($"The dataCenter argument was not of type {TDataCenterType.Name} on setting \"{name}\"");

                    dc = (TDataCenter)dsva.DataCenter;
                }
                
                if (tier == null)
                    tier = default(TTier);

                if (dc == null)
                    dc = default(TDataCenter);

                var dsvaDefault = dsva.DefaultValue;

                if (isEncrypted)
                {
                    if (Compare.IsDefault(tier.Value))
                        throw new NFigException($"{name} has a default without a tier. Additional default values for encrypted settings must include a non-\"Any\" tier.");

                    if (dsvaDefault != null && !(dsvaDefault is string))
                        throw new NFigException($"{name} has a non-string default. Encrypted defaults must be in string representation.");
                }

                // if it's not the Any tier, and not the current tier, then we don't care about this default
                var skip = !Compare.IsDefault(tier.Value) && !Compare.AreEqual(tier.Value, Tier);

                // Even if we're skipping this default, performing validation for all tiers is useful.
                // However, if the value is encrypted, we only want to perform the validation for the current tier.
                if (!skip || !isEncrypted)
                {
                    defaultStringValue = GetStringFromDefaultAndValidate(name, dsvaDefault, tier.Value, dc.Value, converter, isEncrypted);

                    // create default
                    var d = new SettingValue<TTier, TDataCenter>(
                        name,
                        defaultStringValue,
                        tier.Value,
                        dc.Value,
                        true,
                        dsva.AllowOverrides
                        );

                    // make sure there isn't a conflicting default value
                    foreach (var existing in defaults)
                    {
                        if (existing.HasSameTierAndDataCenter(d))
                            throw new NFigException("Multiple defaults were specified for the same environment on settings property: " +
                                                    pi.DeclaringType.FullName + "." + pi.Name);
                    }

                    if (!skip)
                        defaults.Add(d);
                }
            }

            // create setter method
            var setter = CreateSetterMethod<TValue>(pi, parent, name);
            var getter = CreateGetterMethod<TValue>(pi, parent, name);

            return new Setting<TValue>(name, description, changeRequiresRestart, isEncrypted, pi, defaults.ToArray(), setter, converter, getter);
        }

        string GetStringFromDefaultAndValidate<TValue>(
            string name,
            object value,
            TTier tier,
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
                    TValue tval = value is TValue ? (TValue)value : (TValue)Convert.ChangeType(value, typeof(TValue));
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
                    dataCenter.ToString(),
                    ex);
            }

            return stringValue;
        }

        SettingSetterDelegate<TValue> CreateSetterMethod<TValue>(PropertyInfo pi, PropertyAndParent parent, string name)
        {
            var list = new List<PropertyInfo>();
            while (parent != null)
            {
                list.Add(parent.PropertyInfo);
                parent = parent.Parent;
            }

            var converterType = typeof(ISettingConverter<TValue>);
            var getValue = converterType.GetMethod("GetValue");

            var dm = new DynamicMethod("AssignSetting_" + name, null, new[] { TSettingsType, typeof(string), converterType }, GetType().Module, true);
            var il = dm.GetILGenerator();

            // arg 0 = TSettings settings
            // arg 1 = string str
            // arg 2 = SettingConverter<TValue> converter
            
            // start with the TSettings object
            il.Emit(OpCodes.Ldarg_0); // [settings]

            // loop through any levels of nesting
            // the list is in bottom-to-top order, so we have to iterate in reverse
            for (var i = list.Count - 1; i >= 0; i--)
            {
                il.Emit(OpCodes.Callvirt, list[i].GetMethod); // [nested-property-obj]
            }

            // stack should now be [parent-obj-of-setting]

            // convert string to value
            il.Emit(OpCodes.Ldarg_2); // [parent][converter]
            il.Emit(OpCodes.Ldarg_1); // [parent][converter][str]
            il.Emit(OpCodes.Callvirt, getValue); // [parent][TValue value]

            // call property setter
            il.Emit(OpCodes.Callvirt, pi.SetMethod); // stack empty
            il.Emit(OpCodes.Ret);

            return (SettingSetterDelegate<TValue>) dm.CreateDelegate(typeof(SettingSetterDelegate<TValue>));
        }

        SettingGetterDelegate<TValue> CreateGetterMethod<TValue>(PropertyInfo pi, PropertyAndParent parent, string name)
        {
            var list = new List<PropertyInfo>();
            while (parent != null)
            {
                list.Add(parent.PropertyInfo);
                parent = parent.Parent;
            }

            var dm = new DynamicMethod("RetrieveSetting_" + name, typeof(TValue), new[] { TSettingsType }, GetType().Module, true);
            var il = dm.GetILGenerator();

            // arg 0 = TSettings settings

            // start with the TSettings object
            il.Emit(OpCodes.Ldarg_0); // [settings]

            // loop through any levels of nesting
            // the list is in bottom-to-top order, so we have to iterate in reverse
            for (var i = list.Count - 1; i >= 0; i--)
            {
                il.Emit(OpCodes.Callvirt, list[i].GetMethod); // [nested-property-obj]
            }

            // stack should now be [parent-obj-of-setting]
            il.Emit(OpCodes.Callvirt, pi.GetMethod); // [setting-value]
            il.Emit(OpCodes.Ret);

            return (SettingGetterDelegate<TValue>) dm.CreateDelegate(typeof(SettingGetterDelegate<TValue>));
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

        InitializeSettingsDelegate GetInitializer()
        {
            // todo: it'd be nice to combine this with the GetSettings reflection so we don't have to do it twice
            // Also don't want to make that code uglier than it already is... need to think about this more.

            // build a dynamic method which instantiates a TSettings object
            var dm = new DynamicMethod("TSettings_Instantiate", TSettingsType, Type.EmptyTypes, TSettingsType.Module, true);
            var il = dm.GetILGenerator();

            var settingsLocal = WriteInstantiationIL(il, TSettingsType);
            il.Emit(OpCodes.Ldloc, settingsLocal); // [settings]
            il.Emit(OpCodes.Ret);

            return (InitializeSettingsDelegate)dm.CreateDelegate(typeof(InitializeSettingsDelegate));
        }

        static LocalBuilder WriteInstantiationIL(ILGenerator il, Type type)
        {
            var local = il.DeclareLocal(type);

            // new up object
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
                throw new NFigException("Cannot use type " + type.Name + " for settings groups. It does not have a parameterless constructor.");

            il.Emit(OpCodes.Newobj, ctor); // [type obj]
            il.Emit(OpCodes.Stloc, local); // empty

            // check for any setting group properties which also need to be newed up
            foreach (var pi in type.GetProperties())
            {
                if (IsSettingsGroup(pi))
                {
                    var sub = WriteInstantiationIL(il, pi.PropertyType);

                    // assign new sub object to property
                    il.Emit(OpCodes.Ldloc, local); // [local]
                    il.Emit(OpCodes.Ldloc, sub); // [local][sub]
                    il.Emit(OpCodes.Callvirt, pi.SetMethod); // empty
                }
            }

            return local;
        }

        static IEnumerable<T> SelectSingle<T>(T item)
        {
            yield return item;
        }

        static bool IsSettingsGroup(PropertyInfo pi)
        {
            return pi.PropertyType.IsClass && pi.GetCustomAttribute<SettingsGroupAttribute>() != null;
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


        /**************************************************************************************
         * Helper Classes and Delegates
         *************************************************************************************/

        delegate TSettings InitializeSettingsDelegate();

        class PropertyAndParent
        {
            public PropertyAndParent Parent { get; set; }
            public PropertyInfo PropertyInfo { get; set; }
        }

        delegate void SettingSetterDelegate<TValue>(TSettings settings, string str, ISettingConverter<TValue> converter);

        delegate TValue SettingGetterDelegate<TValue>(TSettings settings);

        abstract class Setting
        {
            public string Name { get; protected set; }
            public string Description { get; protected set; }
            public bool ChangeRequiresRestart { get; protected set; }
            public bool IsEncrypted { get; protected set; }
            public PropertyInfo PropertyInfo { get; protected set; }
            public SettingValue<TTier, TDataCenter>[] Defaults { get; protected set; }

            public abstract object GetValue(TSettings settings);
            public abstract void SetValueFromString(TSettings settings, string str);
            public abstract bool TryGetValueFromString(string str, out object value);
            public abstract bool TryGetStringFromValue(object value, out string str);
        }

        class Setting<TValue> : Setting
        {
            readonly ISettingConverter<TValue> _converter;
            readonly SettingSetterDelegate<TValue> _setter;

            public readonly SettingGetterDelegate<TValue> Getter;


            public Setting(
                string name,
                string description,
                bool changeRequiresRestart,
                bool isEncrypted,
                PropertyInfo propertyInfo,
                SettingValue<TTier, TDataCenter>[] defaults,
                SettingSetterDelegate<TValue> setter,
                ISettingConverter<TValue> converter,
                SettingGetterDelegate<TValue> getter
            )
            {
                Name = name;
                Description = description;
                ChangeRequiresRestart = changeRequiresRestart;
                IsEncrypted = isEncrypted;
                PropertyInfo = propertyInfo;
                Defaults = defaults;
                Getter = getter;

                _setter = setter;
                _converter = converter;
            }

            public override object GetValue(TSettings settings)
            {
                return Getter(settings);
            }

            public override void SetValueFromString(TSettings settings, string str)
            {
                _setter(settings, str, _converter);
            }

            public override bool TryGetValueFromString(string str, out object value)
            {
                value = null;
                try
                {
                    value = _converter.GetValue(str);
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
                    str = _converter.GetString((TValue)value);
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
