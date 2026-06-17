using TeaNeko.Core.DependencyInjection.Annotations;

namespace TeaNeko.Core.Test.DependencyInjection.Services;

// ===== 多实现无 [Primary]（全部仅 Keyed，触发警告）=====

/// <summary>
/// 通知服务接口。
/// 有两个实现（<see cref="EmailNotifier"/> 和 <see cref="SmsNotifier"/>），均无 [Primary]。
/// 此时 <see cref="AutoServiceScanner"/> 应输出警告，且仅注册 Keyed，不注册 Singleton。
/// </summary>
public interface INotifier { string Notify(string msg); }

/// <summary>
/// 邮件通知器。无 [Primary]，仅注册为 Keyed("email")。
/// </summary>
[Service(Id = "email")]
public class EmailNotifier : INotifier
{
    /// <inheritdoc />
    public string Notify(string msg) => $"Email: {msg}";
}

/// <summary>
/// 短信通知器。无 [Primary]，仅注册为 Keyed("sms")。
/// </summary>
[Service(Id = "sms")]
public class SmsNotifier : INotifier
{
    /// <inheritdoc />
    public string Notify(string msg) => $"SMS: {msg}";
}
