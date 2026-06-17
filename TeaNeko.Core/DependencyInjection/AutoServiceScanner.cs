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
                    interfaceToImpls.GetOrAdd(iface, _ => new List<Type>()).Add(implType);
            }
            // 无接口的类，以自身为 key
            interfaceToImpls.GetOrAdd(implType, _ => new List<Type>()).Add(implType);
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
            else if (primaryImpls.Count == 1)
            {
                var primaryType = primaryImpls[0];
                RegisterSingle(services, interfaceType, primaryType, logger);
                foreach (var implType in implTypes.Where(t => t != primaryType))
                    RegisterKeyedOnly(services, interfaceType, implType, logger);
            }
            else if (primaryImpls.Count > 1)
            {
                logger?.LogWarning(
                    "[AutoServiceScanner] 接口 '{Interface}' 有多个实现使用了 [Primary]: {Types}。" +
                    "全部仅注册 Keyed，不注册 Singleton。",
                    interfaceType.FullName,
                    string.Join(", ", primaryImpls.Select(t => t.Name)));
                foreach (var implType in implTypes)
                    RegisterKeyedOnly(services, interfaceType, implType, logger);
            }
            else
            {
                logger?.LogWarning(
                    "[AutoServiceScanner] 接口 '{Interface}' 有多个实现但无 [Primary]。" +
                    "全部仅注册 Keyed。实现: {Types}",
                    interfaceType.FullName,
                    string.Join(", ", implTypes.Select(t => t.Name)));
                foreach (var implType in implTypes)
                    RegisterKeyedOnly(services, interfaceType, implType, logger);
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

                Func<IServiceProvider, object> factory = sp =>
                {
                    var configInstance = sp.GetRequiredService(configType);
                    return method.Invoke(configInstance, null)!;
                };

                // Keyed 注册：使用 services.AddKeyedSingleton/Scoped/Transient
                RegisterByLifetimeKeyed(services, returnType, beanId, factory, lifetime);
            }
        }

        return services;
    }

    // ========== 注册策略 ==========

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

    private static void RegisterKeyedOnly(IServiceCollection services, Type interfaceType, Type implType, ILogger? logger)
    {
        var serviceId = GetServiceId(implType);
        var lifetime = GetScope(implType);
        Func<IServiceProvider, object> factory = sp => CreateInstance(sp, implType);

        RegisterByLifetimeKeyed(services, interfaceType, serviceId, factory, lifetime);

        logger?.LogDebug(
            "[AutoServiceScanner] '{Impl}' -> {Interface} Keyed('{Id}') only",
            implType.Name, interfaceType.Name, serviceId);
    }

    // ========== 生命周期注册辅助 ==========

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

    private static string GetServiceId(Type implType)
    {
        var attr = implType.GetCustomAttribute<ServiceAttribute>();
        return !string.IsNullOrWhiteSpace(attr?.Id) ? attr.Id : implType.Name;
    }

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

        for (int i = 0; i < parameters.Length; i++)
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
