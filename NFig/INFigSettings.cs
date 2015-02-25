namespace NFig
{
    public interface INFigSettings<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        TTier Tier { get; set; }
        TDataCenter DataCenter { get; set; }
    }
}