namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记服务为默认首选实现（类似 Spring 的 @Primary）。
/// 当同一接口有多个实现时，标记此注解的实现会被注册为 Singleton。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PrimaryAttribute : Attribute
{
}
