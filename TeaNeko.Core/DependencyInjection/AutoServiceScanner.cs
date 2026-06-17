using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeaNeko.Core.DependencyInjection.Annotations;

namespace TeaNeko.Core.DependencyInjection;

/// <summary>
/// 自动服务扫描器。
/// 扫描程序集中的 [Service]/[Configuration]/[Bean] 注解，自动注册到 DI 容器。
/// 实现 Keyed + Singleton 混合注册策略。
/// </summary>
public static class AutoServiceScanner
{
    /// <summary>
    /// 扫描指定程序集，自动注册所有带注解的服务。
    /// </summary>
    /// <param name="services">需要追加注册结果的服务集合。</param>
    /// <param name="assemblies">需要扫描的程序集集合；动态程序集会被跳过。</param>
    /// <param name="loggerFactory">可选日志工厂，用于记录多实现冲突、无 Primary 等注册诊断信息。</param>
    /// <returns>传入的 <paramref name="services"/>，用于链式调用。</returns>
    public static IServiceCollection AddAutoScannedServices(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ILoggerFactory? loggerFactory = null)
    {
        var logger = loggerFactory?.CreateLogger("TeaNeko.Core.AutoServiceScanner");
        var allTypes = assemblies
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try { return a.GetExportedTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .ToList();

        // ===== 1. 收集所有 [Service] 类 =====
        var serviceTypes = allTypes
            .Where(t => t.GetCustomAttribute<ServiceAttribute>() != null)
            .ToList();

        var interfaceToImpls = new ConcurrentDictionary<Type, List<Type>>();
        foreach (var implType in serviceTypes)
        {
            var ifaces = implType.GetInterfaces();
            if (ifaces.Length > 0)
            {
                foreach (var iface in ifaces)
                    interfaceToImpls.GetOrAdd(iface, _ => []).Add(implType);
            }
            // 无接口的类，以自身为 key
            interfaceToImpls.GetOrAdd(implType, _ => []).Add(implType);
        }

        // ===== 2. Keyed + Singleton 混合注册 =====
        foreach (var (interfaceType, implTypes) in interfaceToImpls)
        {
            if (implTypes.Count == 0) continue;

            var primaryImpls = implTypes
                .Where(t => t.GetCustomAttribute<PrimaryAttribute>() != null)
                .ToList();

            if (implTypes.Count == 1)
            {
                RegisterSingle(services, interfaceType, implTypes[0], logger);
            }
            else switch (primaryImpls.Count)
            {
                case 1:
                {
                    var primaryType = primaryImpls[0];
                    RegisterSingle(services, interfaceType, primaryType, logger);
                    foreach (var implType in implTypes.Where(t => t != primaryType))
                        RegisterKeyedOnly(services, interfaceType, implType, logger);
                    break;
                }
                case > 1:
                {
                    logger?.LogWarning(
                        "[AutoServiceScanner] 接口 '{Interface}' 有多个实现使用了 [Primary]: {Types}。" +
                        "全部仅注册 Keyed，不注册 Singleton。",
                        interfaceType.FullName,
                        string.Join(", ", primaryImpls.Select(t => t.Name)));
                    foreach (var implType in implTypes)
                        RegisterKeyedOnly(services, interfaceType, implType, logger);
                    break;
                }
                default:
                {
                    logger?.LogWarning(
                        "[AutoServiceScanner] 接口 '{Interface}' 有多个实现但无 [Primary]。" +
                        "全部仅注册 Keyed。实现: {Types}",
                        interfaceType.FullName,
                        string.Join(", ", implTypes.Select(t => t.Name)));
                    foreach (var implType in implTypes)
                        RegisterKeyedOnly(services, interfaceType, implType, logger);
                    break;
                }
            }
        }

        // ===== 3. 处理 [Configuration] + [Bean] =====
        var configTypes = allTypes
            .Where(t => t.GetCustomAttribute<ConfigurationAttribute>() != null)
            .ToList();

        foreach (var configType in configTypes)
        {
            services.AddSingleton(configType);
            var beanMethods = configType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<BeanAttribute>() != null);

            foreach (var method in beanMethods)
            {
                var beanAttr = method.GetCustomAttribute<BeanAttribute>()!;
                var returnType = method.ReturnType;
                var beanId = !string.IsNullOrWhiteSpace(beanAttr.Id) ? beanAttr.Id : method.Name;
                var lifetime = GetScope(configType);

                // Keyed 注册：使用 services.AddKeyedSingleton/Scoped/Transient
                RegisterByLifetimeKeyed(services, returnType, beanId, Factory, lifetime);
                continue;

                object Factory(IServiceProvider sp)
                {
                    var configInstance = sp.GetRequiredService(configType);
                    return method.Invoke(configInstance, null)!;
                }
            }
        }

        return services;
    }

    // ========== 注册策略 ==========

    /// <summary>
    /// 将指定实现注册为默认服务和 Keyed 服务。
    /// 该方法使用同一个工厂闭包完成两种注册，从而保证默认解析和 Keyed 解析在单例生命周期下拿到同一实例。
    /// </summary>
    /// <param name="services">目标服务集合。</param>
    /// <param name="interfaceType">对外暴露的服务类型，通常是接口；无接口服务会以实现类型自身作为服务类型。</param>
    /// <param name="implType">实际实现类型。</param>
    /// <param name="logger">可选日志组件，用于输出注册调试信息。</param>
    private static void RegisterSingle(IServiceCollection services, Type interfaceType, Type implType, ILogger? logger)
    {
        var serviceId = GetServiceId(implType);
        var lifetime = GetScope(implType);

        // 工厂 + Double-check lock 保证 Singleton 和 Keyed 共享同一实例
        object? instance = null;
        var lockObj = new object();
        Func<IServiceProvider, object> factory = sp =>
        {
            if (instance is null)
            {
                lock (lockObj)
                    instance ??= CreateInstance(sp, implType);
            }
            return instance;
        };

        // Singleton 注册
        RegisterByLifetime(services, interfaceType, factory, lifetime);
        // Keyed 注册（共享同一工厂）
        RegisterByLifetimeKeyed(services, interfaceType, serviceId, factory, lifetime);

        logger?.LogDebug(
            "[AutoServiceScanner] '{Impl}' -> {Interface} Singleton + Keyed('{Id}')",
            implType.Name, interfaceType.Name, serviceId);
    }

    /// <summary>
    /// 只为指定实现创建 Keyed 注册，不创建默认服务注册。
    /// 多实现且没有唯一 Primary，或多个 Primary 冲突时会采用该策略，避免默认解析结果不确定。
    /// </summary>
    /// <param name="services">目标服务集合。</param>
    /// <param name="interfaceType">对外暴露的服务类型。</param>
    /// <param name="implType">实际实现类型。</param>
    /// <param name="logger">可选日志组件，用于输出注册调试信息。</param>
    private static void RegisterKeyedOnly(IServiceCollection services, Type interfaceType, Type implType, ILogger? logger)
    {
        var serviceId = GetServiceId(implType);
        var lifetime = GetScope(implType);

        RegisterByLifetimeKeyed(services, interfaceType, serviceId, Factory, lifetime);

        logger?.LogDebug(
            "[AutoServiceScanner] '{Impl}' -> {Interface} Keyed('{Id}') only",
            implType.Name, interfaceType.Name, serviceId);
        return;

        object Factory(IServiceProvider sp) => CreateInstance(sp, implType);
    }

    // ========== 生命周期注册辅助 ==========

    /// <summary>
    /// 按 MSDI 生命周期注册非 Keyed 服务。
    /// </summary>
    /// <param name="services">目标服务集合。</param>
    /// <param name="serviceType">服务类型。</param>
    /// <param name="factory">用于创建服务实例的工厂。</param>
    /// <param name="lifetime">服务生命周期。</param>
    private static void RegisterByLifetime(IServiceCollection services, Type serviceType,
        Func<IServiceProvider, object> factory, ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(serviceType, factory);
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped(serviceType, factory);
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(serviceType, factory);
                break;
        }
    }

    /// <summary>
    /// 按 MSDI 生命周期注册 Keyed 服务。
    /// </summary>
    /// <param name="services">目标服务集合。</param>
    /// <param name="serviceType">服务类型。</param>
    /// <param name="serviceKey">Keyed 服务使用的解析键。</param>
    /// <param name="factory">用于创建服务实例的普通工厂；方法内部会包装成 .NET Keyed 工厂签名。</param>
    /// <param name="lifetime">服务生命周期。</param>
    private static void RegisterByLifetimeKeyed(IServiceCollection services, Type serviceType,
        object serviceKey, Func<IServiceProvider, object> factory, ServiceLifetime lifetime)
    {
        // .NET 8+ Keyed 工厂签名: Func<IServiceProvider, object?, object>
        Func<IServiceProvider, object?, object> keyedFactory = (sp, _) => factory(sp);
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddKeyedSingleton(serviceType, serviceKey, keyedFactory);
                break;
            case ServiceLifetime.Scoped:
                services.AddKeyedScoped(serviceType, serviceKey, keyedFactory);
                break;
            case ServiceLifetime.Transient:
                services.AddKeyedTransient(serviceType, serviceKey, keyedFactory);
                break;
        }
    }

    // ========== 辅助方法 ==========

    /// <summary>
    /// 获取服务实现的注册 ID。
    /// </summary>
    /// <param name="implType">带有 <see cref="ServiceAttribute"/> 的实现类型。</param>
    /// <returns>特性上显式声明的 ID；未声明时返回类型名。</returns>
    private static string GetServiceId(Type implType)
    {
        var attr = implType.GetCustomAttribute<ServiceAttribute>();
        return !string.IsNullOrWhiteSpace(attr?.Id) ? attr.Id : implType.Name;
    }

    /// <summary>
    /// 将 TeaNeko 的 <see cref="ServiceLifetimeScope"/> 映射为 MSDI 的 <see cref="ServiceLifetime"/>。
    /// </summary>
    /// <param name="implType">需要读取 <see cref="ScopeAttribute"/> 的实现类型或配置类型。</param>
    /// <returns>对应的 MSDI 生命周期；未声明时默认为 <see cref="ServiceLifetime.Singleton"/>。</returns>
    private static ServiceLifetime GetScope(Type implType)
    {
        var attr = implType.GetCustomAttribute<ScopeAttribute>();
        return attr?.Lifetime switch
        {
            ServiceLifetimeScope.Transient => ServiceLifetime.Transient,
            ServiceLifetimeScope.Scoped => ServiceLifetime.Scoped,
            _ => ServiceLifetime.Singleton
        };
    }

    /// <summary>
    /// 通过构造函数反射创建服务实例。
    /// 会选择参数最多的构造函数，并从 <see cref="IServiceProvider"/> 中按参数类型解析依赖；
    /// 当类型或参数标记了 <see cref="LazyAttribute"/> 且参数类型为 <see cref="Lazy{T}"/> 时，会创建延迟解析包装。
    /// </summary>
    /// <param name="sp">用于解析构造函数依赖的服务提供者。</param>
    /// <param name="implType">需要创建的实现类型。</param>
    /// <returns>构造完成的服务实例。</returns>
    private static object CreateInstance(IServiceProvider sp, Type implType)
    {
        var ctor = implType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();

        if (ctor is null)
            return Activator.CreateInstance(implType)!;

        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];

        var typeLevelLazy = implType.GetCustomAttribute<LazyAttribute>() != null;

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            var isLazy = typeLevelLazy || param.GetCustomAttribute<LazyAttribute>() != null;

            if (isLazy && param.ParameterType.IsGenericType
                && param.ParameterType.GetGenericTypeDefinition() == typeof(Lazy<>))
            {
                var innerType = param.ParameterType.GetGenericArguments()[0];
                var lazyType = typeof(Lazy<>).MakeGenericType(innerType);
                args[i] = Activator.CreateInstance(lazyType,
                    new Func<object?>(() => sp.GetService(innerType)));
            }
            else
            {
                args[i] = sp.GetService(param.ParameterType);
            }
        }

        return ctor.Invoke(args);
    }
}
