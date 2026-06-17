namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记配置类中的方法为 Bean 工厂方法（类似 Spring 的 @Bean）。
/// 方法返回值类型作为注册类型，可指定 ID。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class BeanAttribute : Attribute
{
    /// <summary>
    /// Bean 注册 ID。为空时使用方法名。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 标记 Bean 工厂方法。
    /// </summary>
    public BeanAttribute() { }

    /// <summary>
    /// 标记 Bean 工厂方法，指定注册 ID。
    /// </summary>
    /// <param name="id">Keyed 注册使用的 Bean ID。</param>
    public BeanAttribute(string id)
    {
        Id = id;
    }
}
