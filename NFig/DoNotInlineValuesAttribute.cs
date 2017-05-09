using System;

namespace NFig
{
    /// <summary>
    /// Forces NFig to call the converter and generate a new value every time a settings object is created. Normally, NFig will only call the converter once
    /// (at startup) for default values, and will reuse the result for each new setting object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DoNotInlineValuesAttribute : Attribute
    {
    }
}
