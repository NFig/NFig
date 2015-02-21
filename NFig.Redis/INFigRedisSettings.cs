namespace NFig.Redis
{
    public interface INFigRedisSettings
    {
        string ApplicationName { get; set; }
        string SettingsCommit { get; set; } 
    }
}