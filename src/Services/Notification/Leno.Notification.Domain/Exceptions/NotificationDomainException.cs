using Leno.SharedKernel.Exceptions;

namespace Leno.Notification.Domain.Exceptions;

/// <summary>
/// 通知域领域异常。
/// </summary>
public sealed class NotificationDomainException : DomainException
{
    public NotificationDomainException(string message, string errorCode = "NOTIFICATION_ERROR")
        : base(message, errorCode)
    {
    }
}
