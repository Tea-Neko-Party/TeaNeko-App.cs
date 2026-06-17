using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TeaNeko.Core.DependencyInjection;

/// <summary>
/// TeaNeko 自动扫描 DI 注册扩展方法。
/// 使用 [Service]/[Configuration]/[Bean] 等注解自动发现并注册服务，
/// 无需手动配置 DI。
/// </summary>
public static class TeaNekoAutoScanExtensions
{
    /// <summary>
    /// 自动扫描入口程序集，注册所有带注解的服务。
    /// 等价于 Spring Boot 的 @ComponentScan。
    /// </summary>
    /// <param name="services">DI 服务集合</param>
    /// <param name="loggerFactory">日志工厂（可选），用于输出注册警告</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddTeaNekoAutoScan(
        this IServiceCollection services,
        ILoggerFactory? loggerFactory = null)
    {
        var assembly = Assembly.GetEntryAssembly()
                       ?? Assembly.GetCallingAssembly();
        return AddTeaNekoAutoScan(services, new[] { assembly }, loggerFactory);
    }

    /// <summary>
    /// 扫描指定程序集，注册所有带注解的服务。
    /// </summary>
    /// <param name="services">DI 服务集合</param>
    /// <param name="assemblies">要扫描的程序集列表</param>
    /// <param name="loggerFactory">日志工厂（可选），用于输出注册警告</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddTeaNekoAutoScan(
        this IServiceCollection services,
        Assembly[] assemblies,
        ILoggerFactory? loggerFactory = null)
    {
        return AutoServiceScanner.AddAutoScannedServices(services, assemblies, loggerFactory);
    }

    /// <summary>
    /// 扫描包含指定类型的所有程序集，注册所有带注解的服务。
    /// </summary>
    /// <typeparam name="T">标记类型，其所在程序集将被扫描</typeparam>
    /// <param name="services">DI 服务集合</param>
    /// <param name="loggerFactory">日志工厂（可选）</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddTeaNekoAutoScan<T>(
        this IServiceCollection services,
        ILoggerFactory? loggerFactory = null)
    {
        return AddTeaNekoAutoScan(services, new[] { typeof(T).Assembly }, loggerFactory);
    }
}
