using JetBrains.Annotations;
using TeaNeko.Core.DependencyInjection.Annotations;

namespace TeaNeko.Core.Test.DependencyInjection.Services;

// ===== 单实现（自动 Keyed + Singleton）=====

/// <summary>
/// 问候服务实现。仅实现 <see cref="IGreeter"/>，无其他实现竞争，
/// 应被注册为 Singleton + Keyed("HelloGreeter")，且两者共享同一实例。
/// </summary>
[Service]
[UsedImplicitly]
public class HelloGreeter : IGreeter
{
    /// <inheritdoc />
    public string Greet(string name) => $"Hello, {name}!";
}

// ===== 自定义 ID =====

/// <summary>
/// 基础计算器。通过 <c>[Service(Id = "calc")]</c> 指定 Keyed 注册 ID 为 "calc"。
/// 应可通过 <c>GetKeyedService</c> 按键解析。
/// </summary>
[Service(Id = "calc")]
public class BasicCalculator : ICalculator
{
    /// <inheritdoc />
    public int Add(int a, int b) => a + b;
}

// ===== [Scope] 测试 =====

/// <summary>
/// 瞬态日志器。标记了 <c>[Scope(ServiceLifetimeScope.Transient)]</c>，
/// 每次从容器解析应返回不同实例。
/// </summary>
[Service]
[Scope(ServiceLifetimeScope.Transient)]
public class TransientSimpleLogger : ISimpleLogger
{
    /// <summary>
    /// 实例 ID，用于验证每次解析是否产生新实例。
    /// </summary>
    public Guid InstanceId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public void Log(string msg) { }
}

/// <summary>
/// 单例日志器。标记了 <c>[Scope(ServiceLifetimeScope.Singleton)]</c>（默认行为），
/// 多次解析应返回同一实例。
/// </summary>
[Service]
[Scope(ServiceLifetimeScope.Singleton)]
public class SingletonSimpleLogger : ISimpleLogger
{
    /// <summary>
    /// 实例 ID，用于验证同一性。
    /// </summary>
    public Guid InstanceId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public void Log(string msg) { }
}
