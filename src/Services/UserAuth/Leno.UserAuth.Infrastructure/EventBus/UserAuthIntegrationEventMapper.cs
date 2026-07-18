using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Leno.UserAuth.Domain.Events;

namespace Leno.UserAuth.Infrastructure.EventBus;

/// <summary>
/// UserAuth BC 领域事件到集成事件的翻译器。
/// 将 User 聚合收集的领域事件翻译为 SharedContracts 中的集成事件。
/// </summary>
public class UserAuthIntegrationEventMapper : IntegrationEventMapperBase
{
    public UserAuthIntegrationEventMapper()
    {
        // UserRegisteredDomainEvent → UserRegisteredEvent（积分与会员域消费创建账户、消息通知域欢迎通知）
        RegisterHandler<UserRegisteredDomainEvent, UserRegisteredEvent>(e =>
            new UserRegisteredEvent(e.UserId, e.Username, e.Email, e.PhoneNumber));

        // UserSuspendedEvent/UserRoleAssignedEvent/UserPasswordChangedEvent/ForgotPasswordRequestedEvent/
        // ExternalLoginUnlinkedEvent/ExternalLoginLinkedEvent 当前无对应集成事件，保持内部领域事件。
        // 若 Notification BC 需消费，后续在 SharedContracts 新建对应集成事件并在此注册翻译。
    }
}
