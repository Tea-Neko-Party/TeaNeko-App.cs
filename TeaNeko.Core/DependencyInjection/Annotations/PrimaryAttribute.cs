namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记服务为默认首选实现（类似 Spring 的 @Primary）。
/// 当同一接口有多个实现时，标记此注解的实现会被注册为 Singleton。
/// 未被标记的其他实现仍会保留 Keyed 注册，可通过服务 ID 精确解析。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class PrimaryAttribute : Attribute
{
}
