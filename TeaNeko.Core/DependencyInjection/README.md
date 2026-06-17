# DependencyInjection 模块

## 概述

DependencyInjection 模块提供 Spring Boot 风格的注解驱动 DI 自动注册系统。
通过 `[Service]`/`[Configuration]`/`[Bean]` 等注解自动发现并注册服务，
无需手动逐行配置 `IServiceCollection`。

## 快速开始

```csharp
using TeaNeko.Core.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();                          // 可选，启用警告日志
services.AddTeaNekoAutoScan();                  // 自动扫描入口程序集
var provider = services.BuildServiceProvider();

// 直接解析
var greeter = provider.GetService<IGreeter>();

// Keyed 解析（按 ID）
var calc = provider.GetKeyedService<ICalculator>("calc");
```

## 注解体系

### `[Service]` — 自动注册

标记类为自动注册的服务。类似 Spring `@Service`。

| 属性 | 默认值 | 说明 |
|------|--------|------|
| `Id` | 类名 | Keyed 注册的键名 |

```csharp
[Service]                        // Keyed ID = "HelloGreeter"
public class HelloGreeter : IGreeter { }

[Service(Id = "calc")]           // Keyed ID = "calc"
public class Calc : ICalculator { }
```

### `[Configuration]` + `[Bean]` — 工厂注册

用于需要自定义构造逻辑的复杂 Bean。类似 Spring `@Configuration` + `@Bean`。

```csharp
[Configuration]
public class AppConfig
{
    [Bean(Id = "mainDB")]         // 注册 IDataSource，Keyed = "mainDB"
    public IDataSource CreateMainDB() => new MyDataSource("connStr");
}
```

### `[Primary]` — 首选实现

当同一接口有多个实现时，标记为首选。类似 Spring `@Primary`。

```csharp
[Primary]                         // 作为 Singleton 默认实现
[Service(Id = "advanced")]
public class AdvancedExecutor : IComplexService { }

[Service(Id = "simple")]          // 仅 Keyed
public class SimpleExecutor : IComplexService { }
```

### `[Scope]` — 生命周期

控制服务解析时创建实例的方式。

| 值 | 说明 |
|----|------|
| `Singleton`（默认） | 全局唯一实例 |
| `Transient` | 每次解析创建新实例 |
| `Scoped` | 每个作用域创建新实例 |

```csharp
[Service]
[Scope(ServiceLifetimeScope.Transient)]
public class TransientLogger : ISimpleLogger { }
```

### `[Lazy]` — 延迟初始化

标记服务为延迟创建。类似 Spring `@Lazy`。构造参数的依赖通过 `Lazy<T>` 包装，
延迟到首次访问时才解析。

```csharp
[Service]
[Lazy]
public class LazyService(ISimpleLogger logger) : IComplexService
{
    // logger 由 AutoServiceScanner 使用 Lazy<ISimpleLogger> 包装注入
}
```

### `[DependsOn]` — 依赖顺序

确保指定服务先于当前服务初始化。类似 Spring `@DependsOn`。

```csharp
[Service(Id = "initA")]
public class InitServiceA { }

[Service(Id = "initB")]
[DependsOn(typeof(InitServiceA))]   // A 先于 B 初始化
public class InitServiceB(InitServiceA a) { }
```

## Keyed + Singleton 混合注册策略

AutoServiceScanner 根据接口实现数量和 `[Primary]` 分布自动决定注册方式：

| 场景 | Singleton 注册 | Keyed 注册 | 行为 |
|------|:---:|:---:|------|
| 1 个实现 | ✅ | ✅ | 同一实例（工厂共享） |
| 多实现，1 个 `[Primary]` | ✅（仅 Primary） | ✅（全部） | Primary = 默认 |
| 多实现，0 个 `[Primary]` | ❌ | ✅ | Logger 输出警告 |
| 多实现，≥2 个 `[Primary]` | ❌ | ✅ | Logger 输出冲突警告 |

> **警告示例**：
> ```
> [AutoServiceScanner] 接口 'INotifier' 有多个实现但无 [Primary]。全部仅 Keyed。实现: EmailNotifier, SmsNotifier
> ```

## 核心类型

### 注册入口

| 类型 | 说明 |
|------|------|
| `TeaNekoAutoScanExtensions` | `AddTeaNekoAutoScan()` — 自动扫描扩展（3 个重载） |
| `AutoServiceScanner` | 底层扫描引擎，实现注解解析和策略注册 |

### 注解（`Annotations/`）

| 注解 | 目标 | 说明 |
|------|------|------|
| `[Service]` | 类 | 标记自动注册 |
| `[Configuration]` | 类 | 标记配置类 |
| `[Bean]` | 方法 | 标记工厂方法 |
| `[Primary]` | 类 | 标记首选实现 |
| `[Lazy]` | 类/参数 | 标记延迟初始化 |
| `[DependsOn]` | 类 | 声明依赖顺序 |
| `[Scope]` | 类 | 控制生命周期 |

### 配置

| 类型 | 说明 |
|------|------|
| `TeaNekoCoreOptions` | 核心配置选项类（`IOptions<T>` 模式） |

## 线程安全

- 扫描过程在 DI 容器构建阶段（`BuildServiceProvider` 前）执行，单线程
- `ConcurrentDictionary` 用于接口→实现分组
- 注册的 Singleton 工厂使用 `lock` + double-check 保证实例唯一
- Keyed 注册的工厂与 Singleton 共享同一工厂闭包，保证同一实例
