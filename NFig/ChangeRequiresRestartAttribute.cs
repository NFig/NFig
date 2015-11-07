using System;

namespace NFig
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ChangeRequiresRestartAttribute : Attribute
    {
    }
}