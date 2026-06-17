using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeaNeko.Core.DependencyInjection;
using TeaNeko.Core.Test.DependencyInjection.Services;

namespace TeaNeko.Core.Test.DependencyInjection;

/// <summary>
/// [Primary] 注解测试：验证多实现场景下的首选服务选择、
/// 无 [Primary] 和多 [Primary] 冲突时的降级行为。
/// </summary>
public class AutoScanPrimaryTests
{
    private readonly IServiceProvider _provider;

    /// <summary>
    /// 初始化测试容器。自动扫描当前程序集并注册所有 [Service] 类。
    /// </summary>
    public AutoScanPrimaryTests()
    {
        var services = new ServiceCollection();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(loggerFactory);
        services.AddTeaNekoAutoScan([typeof(AutoScanPrimaryTests).Assembly], loggerFactory);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 验证持有 [Primary] 的 <see cref="AdvancedExecutor"/> 被注册为 Singleton 默认实现。
    /// </summary>
    [Fact]
    public void PrimaryImpl_ShouldBe_Singleton()
    {
        var svc = _provider.GetService<IComplexService>();
        Assert.NotNull(svc);
        Assert.IsType<AdvancedExecutor>(svc);
        Assert.Equal("advanced", svc.Execute());
    }

    /// <summary>
    /// 验证无 [Primary] 的实现在多实现场景下仅注册为 Keyed，不注册为 Singleton。
    /// </summary>
    [Fact]
    public void NonPrimaryImpls_ShouldBe_KeyedOnly()
    {
        var simple = _provider.GetKeyedService<IComplexService>("simple");
        var fallback = _provider.GetKeyedService<IComplexService>("fallback");
        Assert.NotNull(simple);
        Assert.NotNull(fallback);
        Assert.IsType<SimpleExecutor>(simple);
        Assert.IsType<FallbackExecutor>(fallback);
        Assert.Equal("simple", simple.Execute());
        Assert.Equal("fallback", fallback.Execute());
    }

    /// <summary>
    /// 验证多个实现均无 [Primary] 时，不注册任何 Singleton，
    /// 全部仅注册为 Keyed（通过 <see cref="INotifier"/> 的两个实现验证）。
    /// </summary>
    [Fact]
    public void MultipleImplsNoPrimary_AllKeyedOnly_NoSingleton()
    {
        // 无 [Primary] → 无 Singleton 注册
        var singleton = _provider.GetService<INotifier>();
        Assert.Null(singleton);

        // 全部可通过 Keyed 解析
        var email = _provider.GetKeyedService<INotifier>("email");
        var sms = _provider.GetKeyedService<INotifier>("sms");
        Assert.NotNull(email);
        Assert.NotNull(sms);
        Assert.Equal("Email: hello", email.Notify("hello"));
        Assert.Equal("SMS: hello", sms.Notify("hello"));
    }

    /// <summary>
    /// 验证多个实现均持有 [Primary] 导致冲突时，不注册任何 Singleton，
    /// 全部降级为 Keyed（通过 <see cref="IWorker"/> 的两个实现验证）。
    /// </summary>
    [Fact]
    public void MultiplePrimaryImpls_AllKeyedOnly_NoSingleton()
    {
        // 多个 [Primary] 冲突 → 无 Singleton
        var singleton = _provider.GetService<IWorker>();
        Assert.Null(singleton);

        // 全部可通过 Keyed 解析
        var a = _provider.GetKeyedService<IWorker>("workerA");
        var b = _provider.GetKeyedService<IWorker>("workerB");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal("A done: task", a.Work("task"));
        Assert.Equal("B done: task", b.Work("task"));
    }
}
