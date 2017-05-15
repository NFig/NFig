using System;

namespace NFig
{
    /// <summary>
    /// Marks a property as a settings group. This means that the property is not an individual setting, but rather is the parent of multiple settings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SettingsGroupAttribute : Attribute
    {
    }
}
