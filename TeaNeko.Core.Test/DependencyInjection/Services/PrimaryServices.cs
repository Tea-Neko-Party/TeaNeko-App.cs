using TeaNeko.Core.DependencyInjection.Annotations;

namespace TeaNeko.Core.Test.DependencyInjection.Services;

// ===== 多实现 + [Primary]（唯一 Primary 注册为 Singleton + Keyed）=====

/// <summary>
/// 简单执行器（无 <see cref="PrimaryAttribute"/>）。
/// <see cref="AdvancedExecutor"/> 持有 [Primary]，故此实现仅注册为 Keyed("simple")。
/// </summary>
[Service(Id = "simple")]
public class SimpleExecutor : IComplexService
{
    /// <inheritdoc />
    public string Execute() => "simple";
}

/// <summary>
/// 高级执行器（持有 <see cref="PrimaryAttribute"/>）。
/// 由于是唯一标记 [Primary] 的 <see cref="IComplexService"/> 实现，
/// 同时注册为 Singleton 和 Keyed("advanced")，作为默认解析的首选。
/// </summary>
[Primary]
[Service(Id = "advanced")]
public class AdvancedExecutor : IComplexService
{
    /// <inheritdoc />
    public string Execute() => "advanced";
}

/// <summary>
/// 回退执行器（无 [Primary]）。仅注册为 Keyed("fallback")。
/// </summary>
[Service(Id = "fallback")]
public class FallbackExecutor : IComplexService
{
    /// <inheritdoc />
    public string Execute() => "fallback";
}
