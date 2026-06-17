namespace TeaNeko.Core.DependencyInjection;

/// <summary>
/// TeaNeko Core 全局配置选项。
/// 通过 <c>AddTeaNekoCore(options => { ... })</c> 进行配置，
/// 随后可通过 <see cref="Microsoft.Extensions.Options.IOptions{TeaNekoCoreOptions}"/> 注入到任意服务中。
/// </summary>
public class TeaNekoCoreOptions
{
    /// <summary>
    /// 缓存通用清理间隔（毫秒），默认 1000（1 秒）。
    /// IMemoryCache 使用此值控制后台自动清理周期。
    /// </summary>
    public int CacheGeneralCleanRateMs { get; set; } = 1000;

    /// <summary>
    /// 定时器更新间隔（毫秒），默认 10（10 毫秒）。
    /// <see cref="Actuator.Timer.TimerService"/> 使用此值控制定时器轮询精度。
    /// </summary>
    public int TimerUpdateDelayMs { get; set; } = 10;

    /// <summary>
    /// 文件配置根目录，默认 "config"。
    /// <see cref="FileConfig.FileConfigService"/> 将在此目录下查找和写入配置文件。
    /// </summary>
    public string FileConfigRoot { get; set; } = "config";
}
