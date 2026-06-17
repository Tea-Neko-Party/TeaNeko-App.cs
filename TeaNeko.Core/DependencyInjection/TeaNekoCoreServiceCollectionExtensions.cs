using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeaNeko.Core.Actuator.Task;
using TeaNeko.Core.Actuator.Task.Interfaces;
using TeaNeko.Core.Actuator.Timer;
using TeaNeko.Core.Actuator.Timer.Interfaces;
using TeaNeko.Core.ApiResponse;
using TeaNeko.Core.ApiResponse.Interfaces;
using TeaNeko.Core.Command;
using TeaNeko.Core.Command.Interfaces;
using TeaNeko.Core.Database.Base;
using TeaNeko.Core.Database.Base.Interfaces;
using TeaNeko.Core.Event;
using TeaNeko.Core.Event.Core;
using TeaNeko.Core.Event.Interfaces;
using TeaNeko.Core.FileConfig;
using TeaNeko.Core.FileConfig.Interfaces;
using TeaNeko.Core.Reload;
using TeaNeko.Core.Utils;
using TeaNeko.Core.Utils.Scanning;

namespace TeaNeko.Core.DependencyInjection;

/// <summary>
/// TeaNeko Core DI 注册扩展方法。
/// 将所有核心模块的服务以 Singleton 生命周期注册到 DI 容器中，
/// 并自动将扫描器和可重载组件接入 <see cref="IReloadService"/>。
/// </summary>
public static class TeaNekoCoreServiceCollectionExtensions
{
    /// <summary>
    /// 向 DI 容器注册所有 TeaNeko Core 服务。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">可选的配置回调，用于设置 <see cref="TeaNekoCoreOptions"/></param>
    /// <returns>服务集合（支持链式调用）</returns>
    public static IServiceCollection AddTeaNekoCore(
        this IServiceCollection services,
        Action<TeaNekoCoreOptions>? configure = null)
    {
        // ===== 配置选项 =====
        // 始终确保 IOptions<TeaNekoCoreOptions> 可用（即使未提供配置回调，也使用默认值）
        services.Configure<TeaNekoCoreOptions>(_ => { });
        if (configure != null)
            services.PostConfigure(configure);

        // ===== 内存缓存（替代原 ICacheService） =====
        services.AddMemoryCache();

        // ===== Utils 模块 =====
        services.TryAddSingleton<IBeanScanner, BeanScanner>();
        services.TryAddSingleton<IClassScanner, ClassScanner>();
        services.TryAddSingleton<JsonDescriptionUtil>();

        // ===== Actuator - Task 模块 =====
        services.TryAddSingleton<ITaskService, TaskService>();
        services.TryAddSingleton<ITaskExecuteService, TaskExecuteService>();
        services.TryAddSingleton<ITaskRetryService, TaskRetryService>();
        services.TryAddSingleton<TaskStageScanner>();

        // ===== Actuator - Timer 模块 =====
        // TimerService 同时作为 ITimerService 和 IHostedService，
        // 通过注册单一实例确保后台循环与定时器管理共享同一对象
        services.TryAddSingleton<TimerService>();
        services.TryAddSingleton<ITimerService>(sp => sp.GetRequiredService<TimerService>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService>(sp => sp.GetRequiredService<TimerService>()));

        // ===== Event 模块 =====
        services.TryAddSingleton<IEventService, EventService>();
        services.TryAddSingleton<IEventGeneralizedTypeService, EventGeneralizedTypeService>();
        services.TryAddSingleton<EventScanner>();
        services.TryAddSingleton<EventHandlerScanner>();

        // ===== Database 模块 =====
        services.TryAddSingleton<IDatabaseService, DatabaseService>();

        // ===== FileConfig 模块 =====
        services.TryAddSingleton<IFileConfigService, FileConfigService>();
        services.TryAddSingleton<FileTypeScanner>();

        // ===== Command 模块 =====
        services.TryAddSingleton<CommandScanner>();
        services.TryAddSingleton<CommandDescriptionScanner>();
        services.TryAddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.TryAddSingleton<ICommandExecutor, CommandExecutor>();
        services.TryAddSingleton<ICommandArgumentProcessor, CommandArgumentProcessor>();
        services.TryAddSingleton<ICommandPermissionManager, CommandPermissionManager>();
        services.TryAddSingleton<ICommandScopeManager, CommandScopeManager>();

        // ===== ApiResponse 模块 =====
        services.TryAddSingleton<IAPIResponseService, APIResponseService>();

        // ===== Reload 模块 =====
        // IReloadService 通过工厂方法创建，将所有 IReloadable 扫描器和服务
        // 按优先级排序后统一管理，确保 Init/Reload 生命周期一致。
        // 依赖顺序：
        //   - ServiceProvider 在首次解析 IReloadService 时已完全构建，
        //     所有扫描器均已注册，解析不会触发循环依赖。
        //   - FileConfigService(IReoloadable) 依赖 FileTypeScanner(IReloadable)，
        //     两者均为 Singleton，先解析 FileTypeScanner 再解析 FileConfigService 不会冲突。
        services.TryAddSingleton<IReloadService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ReloadService>>();
            var reloadService = new ReloadService(logger);

            // 按声明顺序添加可重载组件（ReloadService 内部按 Priority 降序重排）
            // 高优先级组件（如 FileTypeScanner Priority=1000）先于低优先级组件执行 Init/Reload
            reloadService.AddReloadable(sp.GetRequiredService<TaskStageScanner>());
            reloadService.AddReloadable(sp.GetRequiredService<EventScanner>());
            reloadService.AddReloadable(sp.GetRequiredService<EventHandlerScanner>());
            reloadService.AddReloadable(sp.GetRequiredService<IFileConfigService>());
            reloadService.AddReloadable(sp.GetRequiredService<FileTypeScanner>());
            reloadService.AddReloadable(sp.GetRequiredService<CommandScanner>());
            reloadService.AddReloadable(sp.GetRequiredService<CommandDescriptionScanner>());

            return reloadService;
        });

        return services;
    }
}
