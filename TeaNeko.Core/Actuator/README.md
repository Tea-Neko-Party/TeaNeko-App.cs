# Actuator 模块

## 概述

Actuator 模块是 TeaNeko Core 的任务执行引擎与定时器调度中心。
包含两个子模块：**Task**（异步任务系统）和 **Timer**（定时器调度系统）。
Task 模块提供带阶段链（Stage Chain）的任务提交、执行、重试机制；
Timer 模块提供 Cron、固定速率、固定延迟、自适应速率四种定时器实现。

## Task 子模块

### 核心类与接口

- **`ITaskService`** -- 任务提交服务接口。提供 `Subscribe` 系列方法将任务投入队列。
- **`TaskService`** -- 任务服务实现。内部使用 `ConcurrentDictionary` 维护命名空间到阶段链的映射。
- **`ITaskExecuteService`** -- 任务执行器接口。从队列中取出任务并驱动阶段链执行。
- **`TaskExecuteService`** -- 任务执行器实现。单线程循环处理任务，每个任务依次经过所有 `ITaskStage` 后运行 Callable。
- **`ITaskRetryService`** -- 任务重试服务接口。提供基于策略的重试逻辑。
- **`TaskRetryService`** -- 任务重试服务实现。支持 `FIXED_DELAY`、`EXPONENTIAL_BACKOFF`、`RANDOM_DELAY` 三种重试策略。
- **`ITask<T>`** / **`Task<T>`** -- 任务抽象。封装 Callable、配置、状态机和结果。
- **`ITaskConfig<T>`** / **`TaskConfig<T>`** -- 任务配置。使用 Builder 模式构建任务参数（名称、Callable、超时、阶段链命名空间等）。
- **`ITaskResult<T>`** / **`TaskResult<T>`** -- 任务结果封装。
- **`TaskFuture<T>`** -- 任务 Future。基于 `TaskCompletionSource<T>` 提供异步等待结果的能力。
- **`ITaskStage`** -- 任务阶段接口。阶段可在任务执行前后插入自定义逻辑（如事务管理、日志记录）。
- **`TaskStageChain`** -- 阶段链。按顺序遍历阶段列表，到达末尾时执行 Callable。
- **`TaskStageScanner`** -- 阶段扫描器。扫描带有 `[TaskStage]` 注解的 Bean，按命名空间和优先级组织阶段列表。
- **`ITaskState`** / 各状态实现 -- 任务状态机（Created、Submitted、Executed、Finished）。

### 线程安全

- `TaskService` 使用 `ConcurrentDictionary` 存储任务队列和阶段链，支持多生产者并发提交。
- `TaskExecuteService` 使用内部 `ConcurrentQueue` 实现生产者-消费者模式。
- `TaskStageScanner` 使用 `ConcurrentDictionary` 存储扫描结果，每次 `_scan()` 先 `Clear()` 再重新填充。
- `TaskFuture<T>` 基于 `TaskCompletionSource<T>`，线程安全。
- 状态机 `Task<T>` 内部使用 `LockStateMachine` 保护状态切换。

## Timer 子模块

### 核心类与接口

- **`ITimerService`** -- 定时器服务接口。管理定时器的注册和移除。
- **`TimerService`** -- 定时器服务实现。同时实现 `IHostedService`，通过 `PeriodicTimer` 后台循环轮询所有已注册定时器，检查触发时间并调用 `timer.Execute(taskService)`。
- **`ITimer<T>`** -- 定时器接口。定义 `IsTime`、`Update`、`Execute` 核心方法。
- **`CronTimer`** -- Cron 表达式定时器。基于 `Cronos` 库解析标准 Cron 表达式。
- **`FixedRateTimer`** -- 固定速率定时器。从首次执行时间开始，按固定间隔触发。
- **`FixedDelayTimer`** -- 固定延迟定时器。上次执行完成后等待固定间隔再触发。
- **`SmartRateTimer`** -- 自适应速率定时器。在指定时间窗口内随机化延迟，适合负载分散场景。
- **`ITimerTaskConfig`** / **`TimerTaskConfig`** -- 定时器任务配置。包含 `ILivable`、`IPausable` 引用。

### 线程安全

- `TimerService` 使用 `ConcurrentDictionary<object, byte>` 存储定时器引用（作为并发 set 使用），后台循环遍历时获取快照。
- 各 `ITimer` 实现内部使用 `DateTimeOffset` 比较（值类型操作，线程安全）。

## 使用示例

```csharp
using TeaNeko.Core.Actuator.Task;
using TeaNeko.Core.Actuator.Task.Interfaces;
using TeaNeko.Core.Actuator.Timer;

// === 提交异步任务 ===
var config = TaskConfig<string>.CreateBuilder()
    .WithName("MyTask")
    .WithCallable(() => new TaskResult<string>(true, "完成!"))
    .WithTaskStageNamespace("default")
    .Build();

TaskFuture<string> future = taskService.SubscribeWithFuture<string>(config, typeof(string));
future.Finish(); // 等待任务完成
Console.WriteLine(future.Get().Data); // "完成!"

// === 注册 Cron 定时器 ===
var cronTimer = new CronTimer("0 */5 * * *", new TimerTaskConfig());
timerService.RegisterTimer(cronTimer); // 每 5 分钟触发

// === 固定速率定时器 ===
var fixedRate = new FixedRateTimer(
    TimeSpan.FromSeconds(30),
    new TimerTaskConfig
    {
        Livable = myComponent,    // 组件失效时自动取消注册
        Pausable = myPausable      // 暂停时跳过执行
    });
timerService.RegisterTimer(fixedRate);
```
