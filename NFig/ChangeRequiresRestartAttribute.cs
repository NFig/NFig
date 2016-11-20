using System;

namespace NFig
{
    /// <summary>
    /// Use this attribute to indicate that changing a setting (setting or clearing an override) at runtime will not have any effect until the application is
    /// restarted. This attribute has no effect on NFig itself, and is only provided to help capture metadata which is useful to display in an admin UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ChangeRequiresRestartAttribute : Attribute
    {
    }
}