namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记服务为延迟初始化（类似 Spring 的 @Lazy）。
/// 使用 Lazy&lt;T&gt; 包装以延迟实例创建，用于解决循环依赖。
/// 该特性可以标记在类或构造参数上；实际延迟解析行为由 <see cref="AutoServiceScanner"/> 创建实例时处理。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter, Inherited = false)]
public class LazyAttribute : Attribute
{
}
