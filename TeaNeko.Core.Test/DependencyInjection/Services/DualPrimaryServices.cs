using TeaNeko.Core.DependencyInjection.Annotations;

namespace TeaNeko.Core.Test.DependencyInjection.Services;

// ===== 多个 [Primary]（全部仅 Keyed，触发警告）=====

/// <summary>
/// 工作器服务接口。
/// 有两个实现都标记了 [Primary]（<see cref="WorkerA"/> 和 <see cref="WorkerB"/>），
/// <see cref="AutoServiceScanner"/> 应输出冲突警告，全部仅注册 Keyed，不注册 Singleton。
/// </summary>
public interface IWorker { string Work(string task); }

/// <summary>
/// 工作器 A。标记了 [Primary]，但因 <see cref="WorkerB"/> 也标记 [Primary] 导致冲突，
/// 仅注册为 Keyed("workerA")。
/// </summary>
[Primary]
[Service(Id = "workerA")]
public class WorkerA : IWorker
{
    /// <inheritdoc />
    public string Work(string task) => $"A done: {task}";
}

/// <summary>
/// 工作器 B。与 <see cref="WorkerA"/> 同时标记 [Primary] 导致冲突，
/// 仅注册为 Keyed("workerB")。
/// </summary>
[Primary]
[Service(Id = "workerB")]
public class WorkerB : IWorker
{
    /// <inheritdoc />
    public string Work(string task) => $"B done: {task}";
}
