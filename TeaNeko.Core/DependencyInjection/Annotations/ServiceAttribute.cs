namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记一个类为可自动注册的服务（类似 Spring 的 @Service）。
/// 如果不设置 Id，则默认使用类的名字作为注册 ID。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ServiceAttribute : Attribute
{
    /// <summary>
    /// 服务注册 ID。为空时使用类名。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 标记为可自动注册的服务。
    /// </summary>
    public ServiceAttribute() { }

    /// <summary>
    /// 标记为可自动注册的服务，指定注册 ID。
    /// </summary>
    public ServiceAttribute(string id)
    {
        Id = id;
    }
}
