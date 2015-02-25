namespace NFig.Redis
{
    public interface INFigRedisSettings<TTier, TDataCenter> : INFigSettings<TTier, TDataCenter>
        where TTier : struct
        where TDataCenter : struct
    {
        string ApplicationName { get; set; }
        string SettingsCommit { get; set; }
    }
}