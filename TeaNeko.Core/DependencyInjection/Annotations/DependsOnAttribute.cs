namespace TeaNeko.Core.DependencyInjection.Annotations;

/// <summary>
/// 标记服务依赖的其他服务类型（类似 Spring 的 @DependsOn）。
/// 确保指定的服务先于此服务初始化，用于解决循环依赖。
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
    public DependsOnAttribute(params Type[] dependsOnTypes)
    {
        DependsOnTypes = dependsOnTypes;
    }
}
