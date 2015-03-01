using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
                    throw new SettingConversionException(
                        string.Format("Cannot use {0} as setting converter for type {1}. The converter must implement SettingConverter<{1}>.",
                            kvp.Value.GetType().Name, kvp.Key.Name));
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
                SettingValue<TTier, TDataCenter> over;
                if (overridesBySetting != null && overridesBySetting.TryGetValue(setting.Name, out over))
                {
                    setting.SetValueFromString(s, over.Value);
                }
                else
                {
                    setting.SetValueFromString(s, SettingInfo<TTier, TDataCenter>.GetBestValueFor(setting.Defaults, tier, dataCenter).Value);
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
                    throw new SettingConversionException("More than one SettingConverterAttribute was specified for " + name);
                }

                convObj = converterAttribute.Converter;
            }
            else
            {
                // use the default converter
                if (!_defaultConverters.TryGetValue(pi.PropertyType, out convObj))
                {
                    throw new SettingConversionException("No default converter is available for setting " + name + " of type " + pi.PropertyType.Name);
                }
            }

            // verify the converter is good
            var converter = convObj as ISettingConverter<TValue>;
            if (converter == null)
            {
                throw new SettingConversionException(
                    string.Format("Cannot use {0} as setting converter for {1}. The converter must implement SettingConverter<{2}>.", 
                        convObj.GetType().Name, name, pi.PropertyType.Name));
            }

            // description
            var da = pi.GetCustomAttribute<DescriptionAttribute>();
            var description = da == null ? "" : da.Description;

            // see if there are any default value attributes
            var defaults = new List<SettingValue<TTier, TDataCenter>>();
            var defaultStringValue = GetStringFromDefault(converter, sa.DefaultValue);
            defaults.Add(new SettingValue<TTier, TDataCenter>(name, defaultStringValue, default(TTier), default(TDataCenter), true));
            
            foreach (var dsva in pi.GetCustomAttributes<DefaultSettingValueAttribute>())
            {
                TTier? tier = null;
                TDataCenter? dc = null;

                // type check the "object" properties of the attribute
                if (dsva.Tier != null)
                {
                    if (!(dsva.Tier is TTier))
                        throw new NFigException("The tier argument was not of type " + TTierType.Name + " on setting " + name);

                    tier = (TTier)dsva.Tier;
                }

                if (dsva.DataCenter != null)
                {
                    if (!(dsva.DataCenter is TDataCenter))
                        throw new NFigException("The dataCenter argument was not of type " + TDataCenterType.Name + " on setting " + name);

                    dc = (TDataCenter)dsva.DataCenter;
                }

                // create default
                var d = new SettingValue<TTier, TDataCenter>(
                    name,
                    GetStringFromDefault(converter, dsva.DefaultValue),
                    tier ?? default(TTier),
                    dc ?? default(TDataCenter),
                    true
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

            return new Setting<TValue>(name, description, pi, sa, defaults.ToArray(), setter, converter);
        }

        private static string GetStringFromDefault<TValue>(ISettingConverter<TValue> converter, object value)
        {
            var str = value as string;
            if (str != null)
                return str;

            TValue tval = value is TValue ? (TValue)value : (TValue)Convert.ChangeType(value, typeof(TValue));
            return converter.GetString(tval);
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

        private LocalBuilder WriteInstantiationIL(ILGenerator il, Type type)
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

            public Setting(
                string name,
                string description,
                PropertyInfo propertyInfo,
                SettingAttribute settingAttribute,
                SettingValue<TTier, TDataCenter>[] defaults,
                SettingSetterDelegate<TValue> setter,
                ISettingConverter<TValue> converter
            )
            {
                Name = name;
                Description = description;
                PropertyInfo = propertyInfo;
                SettingAttribute = settingAttribute;
                Defaults = defaults;

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
