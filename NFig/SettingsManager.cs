using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NFig
{
    public class SettingsManager<TSettings, TTier, TDataCenter>
        where TSettings : class, new()
        where TTier : struct
        where TDataCenter : struct
    {
        private readonly Setting[] _settings;
        private readonly InitializeSettingsDelegate _initializer;
        private readonly Type TSettingsType;
        private readonly Type TTierType;
        private readonly Type TDataCenterType;
        private readonly TTier _tier;
        private readonly TDataCenter _dataCenter;

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

        public TTier Tier { get { return _tier; } }
        public TDataCenter DataCenter { get { return _dataCenter; } }

        public SettingsManager(TTier tier, TDataCenter dataCenter, Dictionary<Type, SettingConverterAttribute> additionalDefaultConverters = null)
        {
            TSettingsType = typeof(TSettings);
            TTierType = typeof(TTier);
            TDataCenterType = typeof(TDataCenter);

            if (!TTierType.IsEnum || !TDataCenterType.IsEnum)
                throw new InvalidOperationException("TTier and TDataCenter must be enum types.");

            _tier = tier;
            _dataCenter = dataCenter;

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
            _initializer = GetInitializer();
        }

        public TSettings GetAppSettings(IEnumerable<SettingOverride<TTier, TDataCenter>> overrides = null)
        {
            // pick the right overrides
            Dictionary<string, SettingOverride<TTier, TDataCenter>> overridesBySetting = null;

            if (overrides != null)
            {
                overridesBySetting = new Dictionary<string, SettingOverride<TTier, TDataCenter>>();
                foreach (var over in overrides)
                {
                    if (over.IsValidFor(Tier, DataCenter))
                    {
                        SettingOverride<TTier, TDataCenter> prev;
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
            foreach (var setting in _settings)
            {
                SettingOverride<TTier, TDataCenter> over;
                if (overridesBySetting != null && overridesBySetting.TryGetValue(setting.Name, out over))
                {
                    setting.SetValueFromString(s, over.Value);
                }
                else
                {
                    setting.SetValueFromString(s, setting.ActiveDefault);
                }
            }

            return s;
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
                var toSetting = GetPropertyToSettingMethod(pi.PropertyType);
                var setting = (Setting)toSetting.Invoke(this, new object[] { pi, parent, sa, prefix });
                return SelectSingle(setting);
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
            var defaults = new List<SettingDefault>();
            defaults.Add(new SettingDefault { Value = converter.GetString((TValue)sa.DefaultValue) });
            
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
                var d = new SettingDefault
                {
                    Value = converter.GetString((TValue)dsva.DefaultValue),
                    DataCenter = dc,
                    Tier = tier
                };

                // make sure there isn't a conflicting default value
                foreach (var existing in defaults)
                {
                    if (existing.HasSameTierAndDataCenter(d))
                        throw new NFigException("Multiple defaults were specified for the same environment on settings property: " + pi.PropertyType.FullName + "." + pi.Name);
                }

                defaults.Add(d);
            }

            var activeDefault = SettingDefault.GetActiveDefaultString(defaults, Tier, DataCenter);

            // create setter method
            var setter = CreateSetterMethod<TValue>(pi, parent, name);

            return new Setting<TValue>(name, description, pi, sa, defaults.ToArray(), activeDefault, setter, converter);
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

            var dm = new DynamicMethod("AssignSetting_" + name, null, new[] { TSettingsType, typeof(string), converterType }, GetType().Module);
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
            var dm = new DynamicMethod("TSettings_Instantiate", TSettingsType, Type.EmptyTypes, TSettingsType.Module);
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
            return pi.PropertyType.IsClass && typeof(ISettingsGroup).IsAssignableFrom(pi.PropertyType);
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
            public SettingDefault[] SettingDefault { get; protected set; }
            public string ActiveDefault { get; protected set; }

            public abstract void SetValueFromString(TSettings settings, string str);
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
                SettingDefault[] settingDefault,
                string activeDefault,
                SettingSetterDelegate<TValue> setter,
                ISettingConverter<TValue> converter
            )
            {
                Name = name;
                Description = description;
                PropertyInfo = propertyInfo;
                SettingAttribute = settingAttribute;
                SettingDefault = settingDefault;
                ActiveDefault = activeDefault;

                _setter = setter;
                _converter = converter;
            }

            public override void SetValueFromString(TSettings settings, string str)
            {
                _setter(settings, str, _converter);
            }
        }

        [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "Yes, I know it's a static member in a generic type. The fields are also generic.")]
        private class SettingDefault : SettingOverride<TTier, TDataCenter>
        {
            public static string GetActiveDefaultString(IList<SettingDefault> defaults, TTier tier, TDataCenter dataCenter)
            {
                // first item in the list is always the base default, so start with that
                var defaultValue = defaults[0];
                for (var i = 1; i < defaults.Count; i++)
                {
                    if (defaults[i].IsValidFor(tier, dataCenter) && defaults[i].IsMoreSpecificThan(defaultValue))
                        defaultValue = defaults[i];
                }

                return defaultValue.Value;
            }
        }
    }
}
