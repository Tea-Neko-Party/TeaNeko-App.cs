namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记一个类为配置类（类似 Spring 的 @Configuration）。
/// 配置类中的 <see cref="BeanAttribute"/> 方法用于工厂方式注册复杂 Bean。
/// 配置类自身会以单例形式注册，工厂方法的返回值会按方法返回类型和 Bean ID 注册为 Keyed 服务。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConfigurationAttribute : Attribute
{
}
