namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 服务生命周期枚举。
/// 该枚举会被 <see cref="AutoServiceScanner"/> 映射为 MSDI 的 <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime"/>。
/// </summary>
public enum ServiceLifetimeScope
{
    /// <summary>单例（默认）</summary>
    Singleton,

    /// <summary>每次请求创建新实例</summary>
    Transient,

    /// <summary>每个作用域创建新实例</summary>
    Scoped
}

/// <summary>
/// 标记服务的生命周期（类似 Spring 的 @Scope）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ScopeAttribute : Attribute
{
    /// <summary>
    /// 服务生命周期，默认为 Singleton。
    /// </summary>
    public ServiceLifetimeScope Lifetime { get; }

    /// <summary>
    /// 指定服务生命周期。
    /// </summary>
    /// <param name="lifetime">服务在依赖注入容器中的生命周期，默认是单例。</param>
    public ScopeAttribute(ServiceLifetimeScope lifetime = ServiceLifetimeScope.Singleton)
    {
        Lifetime = lifetime;
    }
}
