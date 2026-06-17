namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记一个类为配置类（类似 Spring 的 @Configuration）。
/// 配置类中的 <see cref="BeanAttribute"/> 方法用于工厂方式注册复杂 Bean。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConfigurationAttribute : Attribute
{
}
