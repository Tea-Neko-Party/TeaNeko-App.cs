namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记服务依赖的其他服务类型（类似 Spring 的 @DependsOn）。
/// 确保指定的服务先于此服务初始化，用于解决循环依赖。
/// 当前特性保存依赖声明，具体初始化顺序需要由扫描或注册流程读取后执行。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class DependsOnAttribute : Attribute
{
    /// <summary>
    /// 依赖的服务类型列表。
    /// </summary>
    public Type[] DependsOnTypes { get; }

    /// <summary>
    /// 指定依赖的服务类型。
    /// </summary>
    /// <param name="dependsOnTypes">当前服务初始化前需要优先准备的服务类型。</param>
    public DependsOnAttribute(params Type[] dependsOnTypes)
    {
        DependsOnTypes = dependsOnTypes;
    }
}
