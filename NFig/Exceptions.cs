using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NFig.Converters;

namespace NFig
{
    /// <summary>
    /// The base class for all NFig exceptions.
    /// </summary>
    public class NFigException : Exception
    {
        /// <summary>
        /// Initializes a new NFigException. This should only be called by NFig libraries.
        /// </summary>
        protected internal NFigException (string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Used to indicate that a setting does not have a valid <see cref="ISettingConverter{TValue}"/>.
    /// </summary>
    public class InvalidSettingConverterException : NFigException
    {
        /// <summary>
        /// The type of the setting which triggered the exception.
        /// </summary>
        public Type SettingType { get; }

        internal InvalidSettingConverterException(string message, Type settingType, Exception innerException = null) : base(message, innerException)
        {
            SettingType = settingType;
        }
    }

    /// <summary>
    /// Used to indicate when a value cannot be assigned to a setting.
    /// </summary>
    public class InvalidDefaultValueException : NFigException
    {
        /// <summary>
        /// The name of the setting which the value was attempting to be assigned to.
        /// </summary>
        public string SettingName { get; }
        /// <summary>
        /// The value which was attempting to be assigned.
        /// </summary>
        public object Value { get; }
        /// <summary>
        /// The sub-app, if applicable.
        /// </summary>
        public int? SubAppId { get; }
        /// <summary>
        /// The string representation of the data center.
        /// </summary>
        public string DataCenter { get; }

        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
        internal InvalidDefaultValueException(
            string message,
            string settingName,
            object value,
            int? subAppId,
            string dataCenter,
            Exception innerException = null)
            : base(message, innerException)
        {
            Data["SettingName"] = SettingName = settingName;
            Data["Value"] = Value = value;
            Data["SubAppId"] = SubAppId = subAppId;
            Data["DataCenter"] = DataCenter = dataCenter;
        }
    }

    /// <summary>
    /// Used to indicate when a value cannot be assigned to a setting.
    /// </summary>
    public class InvalidOverrideValueException : NFigException
    {
        /// <summary>
        /// The name of the setting which the value was attempting to be assigned to.
        /// </summary>
        public string SettingName { get; }
        /// <summary>
        /// The value which was attempting to be assigned.
        /// </summary>
        public object Value { get; }
        /// <summary>
        /// The sub-app, if applicable.
        /// </summary>
        public int? SubAppId { get; }
        /// <summary>
        /// The string representation of the data center.
        /// </summary>
        public string DataCenter { get; }

        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
        internal InvalidOverrideValueException(
            string message,
            string settingName,
            object value,
            int? subAppId,
            string dataCenter,
            Exception innerException = null)
            : base(message, innerException)
        {
            Data["SettingName"] = SettingName = settingName;
            Data["Value"] = Value = value;
            Data["SubAppId"] = SubAppId = subAppId;
            Data["DataCenter"] = DataCenter = dataCenter;
        }
    }

    /// <summary>
    /// This exception is used when one or more overrides (which already exist) cannot be applied to their settings. This could happen if the setting type or
    /// converter was changed since the override was set. This exception is never thrown. It is returned from TryGet methods on NFigStoreOld and passed as the
    /// first parameter to subscription callbacks.
    /// </summary>
    public class InvalidOverridesException : NFigException
    {
        /// <summary>
        /// A list of nested exceptions. The number of exceptions in this list will be the same as the number of overrides which were unable to be applied.
        /// </summary>
        public List<InvalidOverrideValueException> Exceptions { get; }

        internal InvalidOverridesException(List<InvalidOverrideValueException> exceptions) : base(GetMessage(exceptions))
        {
            Exceptions = exceptions;
        }

        static string GetMessage(List<InvalidOverrideValueException> exceptions)
        {
            return $"{exceptions.Count} invalid setting overrides were not applied. See the {nameof(Exceptions)} property for details. You should edit or clear these overrides.";
        }
    }

    /// <summary>
    /// Used to indicate that data in the persistent store is in an illogical state.
    /// </summary>
    public abstract class NFigCorruptException : NFigException
    {
        /// <summary>
        /// Used to indicate that data in the persistent store is in an illogical state.
        /// </summary>
        protected NFigCorruptException(string message, Exception innerException = null) : base(message, innerException)
        {
        }
    }
}