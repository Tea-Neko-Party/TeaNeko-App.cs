namespace TeaNeko.Core.Test.DependencyInjection.Services;

/// <summary>
/// 问候服务接口，用于测试单实现自动注册。
/// </summary>
public interface IGreeter { string Greet(string name); }

/// <summary>
/// 计算器服务接口，用于测试自定义 ID 的 Keyed 注册。
/// </summary>
public interface ICalculator { int Add(int a, int b); }

/// <summary>
/// 日志服务接口，用于测试 <see cref="ServiceLifetimeScope.Transient"/> 和 <see cref="ServiceLifetimeScope.Singleton"/> 作用域。
/// 命名避开了 Microsoft.Extensions.Logging.ILogger 的歧义。
/// </summary>
public interface ISimpleLogger { void Log(string msg); }

/// <summary>
/// 复杂服务接口，用于测试 <see cref="PrimaryAttribute"/> 多实现选择。
/// </summary>
public interface IComplexService { string Execute(); }

/// <summary>
/// 数据源接口，用于测试 <see cref="ConfigurationAttribute"/> + <see cref="BeanAttribute"/> 工厂注册。
/// </summary>
public interface IDataSource { string GetData(); }
