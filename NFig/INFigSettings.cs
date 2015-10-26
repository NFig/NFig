using System.Diagnostics.CodeAnalysis;


namespace NFig
{
    [SuppressMessage("ReSharper", "TypeParameterCanBeVariant")]
    public interface INFigSettings<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        string ApplicationName { get; }
        string Commit { get; set; }
        TTier Tier { get; set; }
        TDataCenter DataCenter { get; set; }
    }
}