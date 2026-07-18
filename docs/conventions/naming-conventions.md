# Leno 命名约定

## DTO 命名

| 类型 | 后缀 | 示例 |
|------|------|------|
| 查询返回 | `*Dto` | `UserDto`、`OrderDto`、`ProductDto` |
| 命令入参 | `*Request` | `CreateOrderRequest`、`SubmitReviewRequest` |
| 命令返回 | `*Response` | `CreateOrderResponse`、`OAuthCallbackResponse` |

**现状说明**：当前代码库主流使用 `*Dto` 后缀，UserAuth BC 使用 `*RequestDto/*ResponseDto`，Notification BC 使用 `*Request/*Response`。新代码遵循本约定，既有代码在重构时逐步对齐。

## 测试文件命名

- 单元测试文件：`{SUT类名}Tests.cs`（复数形式），如 `OrderAppServiceTests.cs`、`SPUTests.cs`
- 集成测试文件：`{场景名}IntegrationTests.cs`，如 `SeckillOrderFlowIntegrationTests.cs`
- 测试类名与文件名一致

## 应用服务接口命名

- 应用服务接口：`I{领域}AppService`，如 `IPointsOffsetAppService`、`IMemberAppService`
- 防腐层接口：`I{领域}AntiCorruptionService`，如 `IPointsAntiCorruptionService`、`IProductAntiCorruptionService`
- 仓储接口：`I{聚合}Repository`，如 `IOrderRepository`、`ISPURepository`

## ErrorCode 命名（M2.1 约定）

- 格式：`{DOMAIN}_{ENTITY}_{ACTION}`，SCREAMING_SNAKE_CASE
- 示例：`PRODUCT_NOT_FOUND`、`ORDER_NOT_OWNED`、`COUPON_ALREADY_RECEIVED`
- 后缀约定驱动 HTTP 状态码映射（详见 ErrorCodeMapping）
