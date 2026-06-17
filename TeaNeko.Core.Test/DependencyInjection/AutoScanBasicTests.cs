using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeaNeko.Core.DependencyInjection;
using TeaNeko.Core.Test.DependencyInjection.Services;

namespace TeaNeko.Core.Test.DependencyInjection;

/// <summary>
/// 基础自动扫描测试：验证单实现注册、自定义 ID、Transient/Singleton 作用域。
/// </summary>
public class AutoScanBasicTests
{
    private readonly IServiceProvider _provider;

    /// <summary>
    /// 初始化测试容器。自动扫描当前程序集并注册所有 [Service] 类。
    /// </summary>
    public AutoScanBasicTests()
    {
        var services = new ServiceCollection();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(loggerFactory);
        services.AddTeaNekoAutoScan([typeof(AutoScanBasicTests).Assembly], loggerFactory);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 验证单一实现的接口同时注册为 Singleton 和 Keyed，
    /// 且两者为同一实例（工厂共享）。
    /// </summary>
    [Fact]
    public void SingleImpl_ShouldRegister_SingletonAndKeyed()
    {
        var greeter = _provider.GetService<IGreeter>();
        Assert.NotNull(greeter);
        Assert.IsType<HelloGreeter>(greeter);
        Assert.Equal("Hello, World!", greeter.Greet("World"));

        // Keyed 通过 ID="HelloGreeter"（默认类名）解析
        var keyed = _provider.GetKeyedService<IGreeter>("HelloGreeter");
        Assert.NotNull(keyed);

        // 同一工厂保证同一实例
        Assert.Same(greeter, keyed);
    }

    /// <summary>
    /// 验证通过 <c>[Service(Id = "calc")]</c> 指定的自定义 ID 可通过 Keyed 解析。
    /// </summary>
    [Fact]
    public void CustomId_ShouldBeResolvable_ByKey()
    {
        var keyed = _provider.GetKeyedService<ICalculator>("calc");
        Assert.NotNull(keyed);
        Assert.Equal(42, keyed.Add(20, 22));
    }

    /// <summary>
    /// 验证标记了 <see cref="ScopeAttribute"/> Transient 的服务每次解析返回新实例。
    /// </summary>
    [Fact]
    public void TransientScope_ShouldCreate_NewInstances()
    {
        var t1 = _provider.GetKeyedService<ISimpleLogger>("TransientSimpleLogger");
        var t2 = _provider.GetKeyedService<ISimpleLogger>("TransientSimpleLogger");
        Assert.NotNull(t1);
        Assert.NotNull(t2);
        Assert.NotSame(t1, t2);
    }

    /// <summary>
    /// 验证标记了 <see cref="ScopeAttribute"/> Singleton 的服务多次解析返回同一实例。
    /// </summary>
    [Fact]
    public void SingletonScope_ShouldReturn_SameInstance()
    {
        var s1 = _provider.GetKeyedService<ISimpleLogger>("SingletonSimpleLogger");
        var s2 = _provider.GetKeyedService<ISimpleLogger>("SingletonSimpleLogger");
        Assert.NotNull(s1);
        Assert.NotNull(s2);
        Assert.Same(s1, s2);
    }
}
