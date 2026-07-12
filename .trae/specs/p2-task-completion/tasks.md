# P2 任务完成 - 任务列表

> **总任务数**: 19 | **全部 P2** | **分布在 9 个模块**

---

## 批次一：共享内核基础设施（4 个任务，无跨模块依赖，可并行）

- [x] Task 1: SK-03 OpenTelemetry 链路追踪集成
	  - [x] 添加 OpenTelemetry NuGet 包到 Leno.Infrastructure
	  - [x] 创建 OpenTelemetryExtensions 扩展方法，配置 ASP.NET Core / HttpClient / EFCore 追踪
	  - [x] 配置 OTLP Exporter（Jaeger），配置 MassTransit ActivitySource 追踪
	  - [x] 创建自定义 ActivitySource 覆盖关键业务操作
	  - [x] 在 Serilog 中携带 TraceId 字段
	  - [x] 配置采样策略（生产 10%，开发 100%）
	  - [x] 编写单元测试验证 OpenTelemetry 配置

- [x] Task 2: SK-04 Consul 配置中心集成
	  - [x] 添加 Winton.Extensions.Configuration.Consul NuGet 包
	  - [x] 创建 ConfigCenterExtensions 扩展方法
	  - [x] 配置 Consul KV 作为配置源，实现热更新监听
	  - [x] 将敏感参数迁移至 Consul（支付密钥、短信 API Key、OAuth2 Secret）
	  - [x] 保留 appsettings.json 作为默认降级配置源
	  - [x] 编写单元测试验证配置热更新

- [x] Task 3: SK-05 布隆过滤器实现
	  - [x] 定义 IBloomFilter 接口（AddAsync、MightContainAsync）
	  - [x] 实现 RedisBloomFilter（基于 Redis Bitmap + 多个 Hash 函数）
	  - [x] 修改 CacheService.GetOrSetAsync 集成布隆过滤器校验
	  - [x] 缓存空值短过期（2 分钟）防穿透，随机过期时间防雪崩
	  - [x] 服务启动时预热布隆过滤器
	  - [x] 编写布隆过滤器误判率测试

- [x] Task 4: SK-06 HealthChecksUI 仪表盘
	  - [x] 添加 AspNetCore.HealthChecks.UI 和 UI.Client NuGet 包
	  - [x] 在各服务配置 AddHealthChecks 覆盖 DB/Redis/ES/RabbitMQ
	  - [x] 配置 /health 和 /health/ready 端点
	  - [x] 创建独立健康检查仪表盘服务
	  - [x] 配置各服务健康端点注册到仪表盘
	  - [x] 编写健康检查端点测试

---

## 批次二：用户域增强（3 个任务，模块内独立，可并行）

- [x] Task 5: UA-06 OAuth2 客户端参数配置
	  - [x] 创建 OAuthClient 实体（Provider、ClientId、ClientSecret、RedirectUri、Enabled）
	  - [x] 实现 IOAuthClientRepository 接口和 EfCore 实现
	  - [x] 实现 CRUD 管理端点（列表/更新/启用/停用）
	  - [x] clientSecret AES-256 加密存储，脱敏返回
	  - [x] 编写领域层单元测试

- [x] Task 6: UA-07 第三方账号绑定与解绑
	  - [x] 在 User 聚合中实现 BindExternalLogin / UnbindExternalLogin 方法
	  - [x] 解绑时校验至少保留一种登录方式
	  - [x] 实现绑定/解绑 API 端点
	  - [x] 绑定/解绑发布对应领域事件
	  - [x] 编写领域层单元测试

- [x] Task 7: UA-08 审计日志中间件
	  - [x] 创建 AuditLog 实体（LogId、OperatorId、Action、ResourceType、ResourceId、BeforeSnapshot、AfterSnapshot 等）
	  - [x] 实现 IAuditLogRepository 接口（仅 AddAsync）
	  - [x] 实现 AuditLogInterceptor 中间件拦截管理操作
	  - [x] 审计日志与业务事务同一事务写入（发件箱模式）
	  - [x] 编写领域层单元测试

---

## 批次三：商品域与购物车域（3 个任务，模块内独立，可并行）

- [x] Task 8: PRD-06 运营全量商品管理列表
	  - [x] 实现 GET /api/admin/products/all 端点
	  - [x] 支持按状态/卖家/分类/关键词筛选
	  - [x] 分页返回，包含审核状态信息
	  - [x] 仅 Admin/Operator 角色可访问
	  - [x] 编写应用层和 API 层测试

- [x] Task 9: PRD-07 SKU 编码全局唯一校验
	  - [x] 定义 IProductUniquenessChecker 接口
	  - [x] 实现 ProductUniquenessChecker（数据库校验唯一性）
	  - [x] 在商品创建/编辑应用服务中集成校验
	  - [x] 编辑场景支持排除自身 ID
	  - [x] 编写领域层单元测试

- [x] Task 10: CART-05 全选/取消全选
	  - [x] 在 Cart 聚合中实现 ToggleAllSelection 方法
	  - [x] 仅操作有效项，失效项保持未选中
	  - [x] 实现 PATCH /api/cart/selection 端点
	  - [x] 编写领域层单元测试

---

## 批次四：订单域与支付域（3 个任务，模块内独立，可并行）

- [x] Task 11: ORD-09 运营强制取消异常订单
	  - [x] 完善 POST /api/admin/orders/{id}/force-cancel 端点
	  - [x] 已支付订单取消触发退款流程（RefundRequestedIntegrationEvent）
	  - [x] 待支付订单取消释放库存/积分/优惠券
	  - [x] 记录操作日志与审计日志
	  - [x] 通知买卖双方
	  - [x] 编写领域层单元测试

- [x] Task 12: PAY-05 对账文件下载
	  - [x] 创建 ReconciliationService BackgroundService
	  - [x] 实现微信/支付宝对账文件下载
	  - [x] 解析对账文件（CSV/TXT），提取交易记录
	  - [x] 与本系统支付单比对，记录差异
	  - [x] 实现 GET /api/admin/reconciliation/diffs 查询
	  - [x] 编写领域层单元测试

- [x] Task 13: PAY-06 支付渠道配置管理
	  - [x] 创建 PaymentChannelConfig 实体
	  - [x] 实现 CRUD 管理端点（列表/更新/启用/停用）
	  - [x] 密钥 AES-256 加密存储，脱敏返回
	  - [x] 参数变更发布事件通知支付域刷新配置
	  - [x] 编写领域层单元测试

---

## 批次五：积分域与售后域（2 个任务，模块内独立，可并行）

- [x] Task 14: PM-08 任务中心
	  - [x] 创建 Task 聚合（TaskType、Name、Description、RewardPoints、CompletionCondition）
	  - [x] 创建 UserTask 实体（UserId、TaskId、Status、CompletedAt）
	  - [x] 任务类型：完善资料、首次下单、分享商品
	  - [x] 实现 GET /api/points/tasks 和 POST /api/points/tasks/{taskId}/complete
	  - [x] 每日任务北京时间 0 点重置，一次性任务不可重复完成
	  - [x] 编写领域层单元测试

- [x] Task 15: RAS-06 售后凭证与评价图片上传
	  - [x] 售后申请支持上传凭证图片（通过 IFileStorageService）
	  - [x] 评价支持上传图片
	  - [x] 图片数量限制（凭证 ≤ 5 张，评价 ≤ 9 张）
	  - [x] 图片大小限制（单张 ≤ 5MB），格式限制（JPG/PNG/WebP）
	  - [x] 编写领域层单元测试

---

## 批次六：通知域增强（4 个任务，模块内独立，可并行）

- [x] Task 16: NTF-10 多渠道选择与故障转移
  - [x] 实现 IChannelSelector.Select 方法
  - [x] 邮件渠道默认 SMTP，短信渠道按 Provider 选择
  - [x] 主适配器失败时切备适配器（仅可重试错误）
  - [x] 所有服务商均不可用记录失败并告警
  - [x] 编写领域层单元测试

- [x] Task 17: NTF-11 渠道回执接收与状态更新
  - [x] 实现邮件/短信回执回调端点
  - [x] 验签防伪造，验签失败返回 401
  - [x] 按 ChannelMessageId 匹配记录更新状态
  - [x] 已 Succeeded 记录幂等去重
  - [x] 编写领域层单元测试

- [x] Task 18: NTF-12 发送记录查询与送达率统计
  - [x] 实现发送记录列表查询（多维度筛选+分页）
  - [x] 实现记录详情和按业务关联追踪
  - [x] 实现死信记录重发
  - [x] 实现送达率统计（按渠道与模板分桶）
  - [x] 手机号/邮箱脱敏展示
  - [x] 编写领域层单元测试

- [x] Task 19: NTF-13 通知模板管理增强
  - [x] 模板变量维护：新增/编辑模板时支持增减 TemplateVariable
  - [x] 保存时校验变量名与正文占位符一致
  - [x] 实现模板预览端点
  - [x] 已禁用模板编辑须先启用
  - [x] 编写领域层单元测试

---

# 任务依赖

- 所有 19 个 P2 任务之间无强依赖关系，可按批次并行执行
- 批次一（共享内核）建议优先执行，为其他模块提供基础设施支撑
- 批次二至批次六可在各自模块内独立并行执行
- 所有任务依赖已完成的 P0/P1 任务（均已就绪）