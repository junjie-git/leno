using Leno.Infrastructure.EventBus;
using DomainUserRegisteredEvent = Leno.Identity.Domain.Events.UserRegisteredEvent;
using IntegrationUserRegisteredEvent = Leno.SharedContracts.Events.UserRegisteredEvent;

namespace Leno.Identity.Infrastructure.EventBus;

/// <summary>
/// Identity BC 领域事件到集成事件的翻译器（3.6 AuthN/AuthZ 拆分）。
/// 将 Identity 聚合根收集的领域事件翻译为 SharedContracts 中的集成事件，
/// 供其他 BC（积分、会员、通知等）消费。
/// </summary>
public sealed class IdentityIntegrationEventMapper : IntegrationEventMapperBase
{
    public IdentityIntegrationEventMapper()
    {
        // 领域层 UserRegisteredEvent → 集成层 UserRegisteredEvent
        // （积分与会员域消费创建账户、消息通知域欢迎通知）
        RegisterHandler<DomainUserRegisteredEvent, IntegrationUserRegisteredEvent>(e =>
            new IntegrationUserRegisteredEvent(e.UserId, e.Username, e.Email, e.PhoneNumber));

        // UserAuthenticatedEvent / UserPasswordChangedEvent / UserSuspendedEvent / ForgotPasswordRequestedEvent /
        // ExternalLoginLinkedEvent / ExternalLoginUnlinkedEvent 当前无对应集成事件，保持内部领域事件。
        // 若 Notification / Risk 等 BC 需消费，后续在 SharedContracts 新建对应集成事件并在此注册翻译。
    }
}
