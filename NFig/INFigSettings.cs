namespace NFig
{
    public interface INFigSettings<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        string ApplicationName { get; set; }
        string Commit { get; set; }
        TTier Tier { get; set; }
        TDataCenter DataCenter { get; set; }
    }
}