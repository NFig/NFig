using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace NFig
{
    public class SettingsFactory<TSettings, TTier, TDataCenter>
        where TSettings : class, INFigSettings<TTier, TDataCenter>, new()
        where TTier : struct
        where TDataCenter : struct
    {
        private readonly Setting[] _settings;
        private readonly Dictionary<string, Setting> _settingsByName;
        private readonly InitializeSettingsDelegate _initializer;
        private readonly Type TSettingsType;
        private readonly Type TTierType;
        private readonly Type TDataCenterType;

        private readonly Dictionary<Type, object> _defaultConverters = new Dictionary<Type, object>
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

        public SettingsFactory(Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null)
        {
            TSettingsType = typeof(TSettings);
            TTierType = typeof(TTier);
            TDataCenterType = typeof(TDataCenter);

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

            _settings = BuildSettings(TSettingsType);
            _settingsByName = _settings.ToDictionary(s => s.Name);
            _initializer = GetInitializer();
        }

        public TSettings GetAppSettings(TTier tier, TDataCenter dataCenter, IEnumerable<SettingValue<TTier, TDataCenter>> overrides = null)
        {
            // pick the right overrides
            Dictionary<string, SettingValue<TTier, TDataCenter>> overridesBySetting = null;

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
            s.Tier = tier;
            s.DataCenter = dataCenter;

            foreach (var setting in _settings)
            {
                var settingValue = SettingInfo<TTier, TDataCenter>.GetBestValueFor(setting.Defaults, tier, dataCenter);

                SettingValue<TTier, TDataCenter> over;
                if (settingValue.AllowsOverrides && overridesBySetting != null && overridesBySetting.TryGetValue(setting.Name, out over))
                {
                    try
                    {
                        setting.SetValueFromString(s, over.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidSettingValueException<TTier, TDataCenter>(
                            $"Invalid override value for setting \"{setting.Name}\". Cannot convert the string override to a real value.",
                            setting.Name,
                            over.Value,
                            false,
                            over.Tier,
                            over.DataCenter,
                            ex);
                    }
                }
                else
                {
                    setting.SetValueFromString(s, settingValue.Value);
                }
            }

            return s;
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

                infos[i] = new SettingInfo<TTier, TDataCenter>(s.Name, s.Description, s.PropertyInfo, s.Defaults, overList);
            }

            return infos;
        }

        public bool IsValidStringForSetting(string settingName, string str)
        {
            object o;
            return TryConvertStringToValue(settingName, str, out o);
        }

        public bool TryConvertStringToValue(string settingName, string str, out object value)
        {
            var setting = _settingsByName[settingName];
            return setting.TryGetValueFromString(str, out value);
        }

        public bool TryConvertValueToString(string settingName, object value, out string str)
        {
            var setting = _settingsByName[settingName];
            return setting.TryGetStringFromValue(value, out str);
        }

        public bool SettingExists(string settingName)
        {
            return _settingsByName.ContainsKey(settingName);
        }

        public static string NewCommit()
        {
            return Guid.NewGuid().ToString();
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

        private Setting[] BuildSettings(Type type)
        {
            // parallize the top-level class. Call ToList() at the end to get out of the parallel query.
            return type.GetProperties().AsParallel().Select(pi => GetSettingsFromProperty(pi, null, "")).SelectMany(s => s).ToArray();
        }

        private IEnumerable<Setting> GetSubSettings(Type type, PropertyAndParent parent, string prefix)
        {
            return type.GetProperties().Select(pi => GetSettingsFromProperty(pi, parent, prefix)).SelectMany(s => s);
        }

        private IEnumerable<Setting> GetSettingsFromProperty(PropertyInfo pi, PropertyAndParent parent, string prefix)
        {
            var sa = pi.GetCustomAttribute<SettingAttribute>();
            if (sa != null)
            {
                try
                {
                    var toSetting = GetPropertyToSettingMethod(pi.PropertyType);
                    var setting = (Setting)toSetting.Invoke(this, new object[] { pi, parent, sa, prefix });
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

        private MethodInfo GetPropertyToSettingMethod(Type type)
        {
            // todo: cache results
            return GetType().GetMethod("PropertyToSetting", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(type);
        }

        // ReSharper disable once UnusedMember.Local
        private Setting PropertyToSetting<TValue>(PropertyInfo pi, PropertyAndParent parent, SettingAttribute sa, string prefix)
        {
            var name = prefix + pi.Name;

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

                        convObj = EnumConverters.GetConverterFor(tValueType);
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
                    $"Cannot use {convObj.GetType().Name} as setting converter for \"{name}\". The converter must implement SettingConverter<{pi.PropertyType.Name}>.", pi.PropertyType);
            }

            // description
            var da = pi.GetCustomAttribute<DescriptionAttribute>();
            var description = da == null ? "" : da.Description;

            // see if there are any default value attributes
            var defaults = new List<SettingValue<TTier, TDataCenter>>();
            var defaultStringValue = GetStringFromDefaultAndValidate(name, sa.DefaultValue, default(TTier), default(TDataCenter), converter);
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

                defaultStringValue = GetStringFromDefaultAndValidate(name, dsva.DefaultValue, tier.Value, dc.Value, converter);

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
                        throw new NFigException("Multiple defaults were specified for the same environment on settings property: " + pi.DeclaringType.FullName + "." + pi.Name);
                }

                defaults.Add(d);
            }

            // create setter method
            var setter = CreateSetterMethod<TValue>(pi, parent, name);
            var getter = CreateGetterMethod<TValue>(pi, parent, name);

            return new Setting<TValue>(name, description, pi, sa, defaults.ToArray(), setter, converter, getter);
        }

        private static string GetStringFromDefaultAndValidate<TValue>(string name, object value, TTier tier, TDataCenter dataCenter, ISettingConverter<TValue> converter)
        {
            string stringValue;

            if (value is string && typeof (TValue) != typeof (string))
            {
                // Don't need to convert to a string if value is already a string and TValue is not.
                // We expect that the human essentially already did the conversion.
                stringValue = (string) value;
            }
            else
            {
                try
                {
                    // try convert the real value into its string representation
                    TValue tval = value is TValue ? (TValue)value : (TValue)Convert.ChangeType(value, typeof(TValue));
                    stringValue = converter.GetString(tval);
                }
                catch (Exception ex)
                {
                    throw new InvalidSettingValueException<TTier, TDataCenter>(
                        $"Invalid default for setting \"{name}\". Cannot convert to a string representation.",
                        name,
                        value,
                        true,
                        tier,
                        dataCenter,
                        ex);
                }
            }

            // now make sure we can also convert the string value back into a real value
            try
            {
                converter.GetValue(stringValue);
            }
            catch (Exception ex)
            {
                throw new InvalidSettingValueException<TTier, TDataCenter>(
                    $"Invalid default value for setting \"{name}\". Cannot convert string representation back into a real value.",
                    name,
                    value,
                    true,
                    tier,
                    dataCenter,
                    ex);
            }

            return stringValue;
        }
        
        private SettingSetterDelegate<TValue> CreateSetterMethod<TValue>(PropertyInfo pi, PropertyAndParent parent, string name)
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

        private SettingGetterDelegate<TValue> CreateGetterMethod<TValue>(PropertyInfo pi, PropertyAndParent parent, string name)
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

        private static bool IsConverterOfType(object converter, Type type)
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

        private InitializeSettingsDelegate GetInitializer()
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

        private static LocalBuilder WriteInstantiationIL(ILGenerator il, Type type)
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

        private static IEnumerable<T> SelectSingle<T>(T item)
        {
            yield return item;
        }

        private static bool IsSettingsGroup(PropertyInfo pi)
        {
            return pi.PropertyType.IsClass && pi.GetCustomAttribute<SettingsGroupAttribute>() != null;
        }


        /**************************************************************************************
         * Helper Classes and Delegates
         *************************************************************************************/

        private delegate TSettings InitializeSettingsDelegate();

        private class PropertyAndParent
        {
            public PropertyAndParent Parent { get; set; }
            public PropertyInfo PropertyInfo { get; set; }
        }

        private delegate void SettingSetterDelegate<TValue>(TSettings settings, string str, ISettingConverter<TValue> converter);
        private delegate TValue SettingGetterDelegate<TValue>(TSettings settings);

        private abstract class Setting
        {
            public string Name { get; protected set; }
            public string Description { get; protected set; }
            public PropertyInfo PropertyInfo { get; protected set; }
            public SettingAttribute SettingAttribute { get; protected set; }
            public SettingValue<TTier, TDataCenter>[] Defaults { get; protected set; }

            public abstract void SetValueFromString(TSettings settings, string str);
            public abstract bool TryGetValueFromString(string str, out object value);
            public abstract bool TryGetStringFromValue(object value, out string str);
        }

        private class Setting<TValue> : Setting
        {
            private readonly ISettingConverter<TValue> _converter;
            private readonly SettingSetterDelegate<TValue> _setter;

            public readonly SettingGetterDelegate<TValue> Getter;


            public Setting(
                string name,
                string description,
                PropertyInfo propertyInfo,
                SettingAttribute settingAttribute,
                SettingValue<TTier, TDataCenter>[] defaults,
                SettingSetterDelegate<TValue> setter,
                ISettingConverter<TValue> converter,
                SettingGetterDelegate<TValue> getter
            )
            {
                Name = name;
                Description = description;
                PropertyInfo = propertyInfo;
                SettingAttribute = settingAttribute;
                Defaults = defaults;
                Getter = getter;

                _setter = setter;
                _converter = converter;
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
