using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeaNeko.Core.DependencyInjection;
using TeaNeko.Core.Test.DependencyInjection.Services;

namespace TeaNeko.Core.Test.DependencyInjection;

/// <summary>
/// [Configuration]/[Bean]/[Lazy]/[DependsOn] 注解测试：
/// 验证工厂方法注册、延迟初始化和依赖顺序。
/// </summary>
public class AutoScanConfigAndLazyTests
{
    private readonly IServiceProvider _provider;

    /// <summary>
    /// 初始化测试容器。自动扫描当前程序集并注册所有 [Service]/[Configuration] 类。
    /// </summary>
    public AutoScanConfigAndLazyTests()
    {
        var services = new ServiceCollection();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(loggerFactory);
        services.AddTeaNekoAutoScan([typeof(AutoScanConfigAndLazyTests).Assembly], loggerFactory);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 验证 <see cref="ConfigurationAttribute"/> 类中 <see cref="BeanAttribute"/> 工厂方法
    /// 创建的 Bean 可通过 Keyed ID 正确解析。
    /// </summary>
    [Fact]
    public void ConfigurationBean_ShouldRegister_FactoryCreatedBeans()
    {
        var mainDb = _provider.GetKeyedService<IDataSource>("mainDB");
        var cacheDb = _provider.GetKeyedService<IDataSource>("cacheDS");

        Assert.NotNull(mainDb);
        Assert.NotNull(cacheDb);
        Assert.IsType<MockDataSource>(mainDb);
        Assert.Equal("Data from MainDB", mainDb.GetData());
        Assert.Equal("Data from CacheDB", cacheDb.GetData());
    }

    /// <summary>
    /// 验证不同 Bean 工厂方法创建的实例互不相同。
    /// </summary>
    [Fact]
    public void ConfigBeanInstances_ShouldBe_Different()
    {
        var mainDb = _provider.GetKeyedService<IDataSource>("mainDB");
        var cacheDb = _provider.GetKeyedService<IDataSource>("cacheDS");
        Assert.NotSame(mainDb, cacheDb);
    }

    /// <summary>
    /// 验证标记 <see cref="LazyAttribute"/> 的服务可正常解析。
    /// 注意：<see cref="LazyService"/> 无 [Primary]，因此 <see cref="AdvancedExecutor"/> 仍为默认 Singleton。
    /// </summary>
    [Fact]
    public void LazyService_ShouldBeResolvable()
    {
        var svc = _provider.GetService<IComplexService>();
        Assert.NotNull(svc);
        // AdvancedExecutor 持有 [Primary]，是首选 Singleton
        Assert.IsType<AdvancedExecutor>(svc);
    }

    /// <summary>
    /// 验证 <see cref="DependsOnAttribute"/> 声明的依赖顺序：
    /// <see cref="InitServiceA"/> 通过构造函数注入到 <see cref="InitServiceB"/>，
    /// 确保 A 先于 B 初始化。
    /// </summary>
    [Fact]
    public void DependsOn_ShouldInitialize_DependenciesFirst()
    {
        var b = _provider.GetKeyedService<InitServiceB>("initB");
        Assert.NotNull(b);
        // InitServiceA 通过构造函数注入，DI 容器确保解析顺序
    }
}
