using TeaNeko.Core.DependencyInjection.Annotations;

namespace TeaNeko.Core.Test.DependencyInjection.Services;

// ===== [Configuration] + [Bean] 工厂注册 =====

/// <summary>
/// 应用配置类（<see cref="ConfigurationAttribute"/>）。
/// 内部的 <see cref="BeanAttribute"/> 方法作为工厂方法注册 Bean。
/// </summary>
[Configuration]
public class AppConfig
{
    /// <summary>
    /// 创建主数据源 Bean。注册 ID 为 "mainDB"。
    /// </summary>
    [Bean(Id = "mainDB")]
    public IDataSource CreateMainDataSource() => new MockDataSource("MainDB");

    /// <summary>
    /// 创建缓存数据源 Bean。注册 ID 为 "cacheDS"。
    /// </summary>
    [Bean(Id = "cacheDS")]
    public IDataSource CreateCacheDataSource() => new MockDataSource("CacheDB");
}

/// <summary>
/// 模拟数据源。通过 <see cref="AppConfig"/> 的 [Bean] 工厂方法创建，
/// 无法直接 new，必须通过 DI 容器解析。
/// </summary>
/// <param name="name">数据源名称</param>
public class MockDataSource(string name) : IDataSource
{
    /// <summary>数据源名称</summary>
    public string Name { get; } = name;

    /// <inheritdoc />
    public string GetData() => $"Data from {Name}";
}

// ===== [Lazy] 测试 =====

/// <summary>
/// 延迟初始化服务。标记 <see cref="LazyAttribute"/>，
/// 构造参数支持 <see cref="Lazy{T}"/> 包装以延迟解析依赖。
/// </summary>
[Service]
[Lazy]
public class LazyService : IComplexService
{
    /// <summary>
    /// 创建延迟服务实例。<paramref name="simpleLogger"/> 被 [Lazy] 包装为 Lazy&lt;T&gt; 延迟解析。
    /// </summary>
    /// <param name="simpleLogger">延迟解析的日志器</param>
    public LazyService(ISimpleLogger simpleLogger)
    {
        // [Lazy] 标记时该参数由 AutoServiceScanner 使用 Lazy<T> 包装注入
    }

    /// <inheritdoc />
    public string Execute() => "lazy-executed";
}

// ===== [DependsOn] 测试 =====

/// <summary>
/// 初始化服务 A。被 <see cref="InitServiceB"/> 依赖，
/// 设置静态标记 <see cref="InitializedA"/> 表示已完成初始化。
/// </summary>
[Service(Id = "initA")]
public class InitServiceA
{
    /// <summary>标记 A 是否已初始化</summary>
    public static bool InitializedA { get; set; }

    /// <summary>构造时设置初始化标记</summary>
    public InitServiceA() { InitializedA = true; }
}

/// <summary>
/// 初始化服务 B。通过 <see cref="DependsOnAttribute"/> 声明依赖 <see cref="InitServiceA"/>，
/// 构造函数注入确保 A 先于 B 初始化。
/// </summary>
[Service(Id = "initB")]
[DependsOn(typeof(InitServiceA))]
public class InitServiceB
{
    /// <summary>标记 B 是否已初始化</summary>
    public static bool InitializedB { get; set; }

    /// <summary>
    /// 构造时注入 <see cref="InitServiceA"/> 以确保依赖顺序。
    /// </summary>
    public InitServiceB(InitServiceA a) { InitializedB = true; }
}
