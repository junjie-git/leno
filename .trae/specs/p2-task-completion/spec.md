# P2 任务完成 - 规范文档

## Why
Leno 电商平台 P0 和 P1 任务已全部完成（68/87，78.2%），剩余 19 个 P2 任务分布在 9 个模块中。完成这些任务将把项目整体完成率提升至 100%，补齐运营体验、运维便利和安全合规方面的最后缺口。

## What Changes
- 共享内核：SK-03 OpenTelemetry 链路追踪、SK-04 Consul 配置中心、SK-05 布隆过滤器、SK-06 HealthChecksUI 仪表盘
- 用户域：UA-06 OAuth2 客户端参数配置、UA-07 第三方账号绑定/解绑、UA-08 审计日志中间件
- 商品域：PRD-06 运营全量商品管理列表、PRD-07 SKU 编码全局唯一校验
- 购物车域：CART-05 全选/取消全选
- 订单域：ORD-09 运营强制取消异常订单
- 支付域：PAY-05 对账文件下载、PAY-06 支付渠道配置管理
- 积分域：PM-08 任务中心
- 售后域：RAS-06 售后凭证与评价图片上传
- 通知域：NTF-10 多渠道选择与故障转移、NTF-11 渠道回执接收、NTF-12 发送记录查询与统计、NTF-13 模板管理增强

## Impact
- Affected specs: 共享内核、用户域、商品域、购物车域、订单域、支付域、积分域、售后域、通知域
- Affected code: 9 个限界上下文的 Domain/Application/Infrastructure/API 层

## ADDED Requirements

### Requirement: OpenTelemetry 全链路追踪 (SK-03)
系统 SHALL 集成 OpenTelemetry 实现跨服务链路追踪，traceId 贯穿网关到各服务到事件消费。

#### Scenario: 链路追踪
- **WHEN** 用户请求经过网关到达各微服务
- **THEN** traceId 在完整调用链路中保持一致，日志中携带 TraceId 字段

### Requirement: Consul 配置中心集成 (SK-04)
系统 SHALL 集成 Consul 配置中心，支持配置热更新与敏感参数外部化。

#### Scenario: 配置热更新
- **WHEN** 运营在 Consul 中修改配置项
- **THEN** 各服务无需重启即可生效新配置

### Requirement: 布隆过滤器防缓存穿透 (SK-05)
系统 SHALL 实现布隆过滤器防缓存穿透，在 CacheService 中集成。

#### Scenario: 缓存穿透防护
- **WHEN** 大量不存在的 key 被请求
- **THEN** 布隆过滤器拦截不存在的数据，缓存空值短过期，防止穿透到数据库

### Requirement: HealthChecksUI 仪表盘 (SK-06)
系统 SHALL 为所有服务添加 HealthChecksUI 仪表盘，可视化各服务健康状态。

#### Scenario: 健康监控
- **WHEN** 运维访问健康检查仪表盘
- **THEN** 可查看所有服务（DB/Redis/ES/RabbitMQ）的健康状态

### Requirement: OAuth2 客户端参数配置 (UA-06)
系统 SHALL 提供 OAuth2 客户端参数的管理能力，支持动态配置第三方登录参数。

#### Scenario: 管理 OAuth2 配置
- **WHEN** 管理员配置微信/支付宝/Google OAuth2 参数
- **THEN** clientSecret 加密存储，脱敏返回，停用不影响已绑定账号

### Requirement: 第三方账号绑定与解绑 (UA-07)
系统 SHALL 支持用户绑定和解绑第三方账号。

#### Scenario: 绑定解绑第三方账号
- **WHEN** 用户绑定或解绑第三方账号
- **THEN** 解绑时至少保留一种登录方式，同一 provider+providerUserId 唯一绑定

### Requirement: 审计日志中间件 (UA-08)
系统 SHALL 自动记录管理操作的审计日志，与业务事务同一事务写入。

#### Scenario: 审计日志记录
- **WHEN** 管理员执行 POST/PUT/DELETE 操作
- **THEN** 审计日志在事务内自动写入，业务回滚时审计日志一并回滚

### Requirement: 运营全量商品管理列表 (PRD-06)
系统 SHALL 提供运营端全平台商品管理列表，支持多维度筛选。

#### Scenario: 运营查看全平台商品
- **WHEN** 运营访问全量商品列表
- **THEN** 可按状态/卖家/分类/关键词筛选，分页返回

### Requirement: SKU 编码全局唯一校验 (PRD-07)
系统 SHALL 校验 SKU 编码全局唯一性和商品标题同店铺内不重复。

#### Scenario: SKU 唯一性校验
- **WHEN** 卖家创建或编辑商品
- **THEN** 重复 SKU 编码返回明确错误提示，编辑场景支持排除自身

### Requirement: 购物车全选/取消全选 (CART-05)
系统 SHALL 支持购物车全选/取消全选功能，仅操作有效项。

#### Scenario: 全选操作
- **WHEN** 用户执行全选/取消全选
- **THEN** 仅有效项被操作，失效项保持不变

### Requirement: 运营强制取消异常订单 (ORD-09)
系统 SHALL 支持运营强制取消异常订单，已支付订单触发退款。

#### Scenario: 强制取消订单
- **WHEN** 运营强制取消异常订单
- **THEN** 已支付订单触发退款，记录操作日志，通知买卖双方

### Requirement: 对账文件下载 (PAY-05)
系统 SHALL 支持每日自动下载微信/支付宝对账文件并与本系统比对。

#### Scenario: 对账处理
- **WHEN** 每日 T+1 自动下载对账文件
- **THEN** 解析对账文件，比对差异，记录可查询

### Requirement: 支付渠道配置管理 (PAY-06)
系统 SHALL 提供支付渠道参数的动态管理能力。

#### Scenario: 管理支付渠道配置
- **WHEN** 管理员更新支付渠道参数
- **THEN** 密钥加密存储，脱敏返回，启停不影响已发起支付

### Requirement: 积分任务中心 (PM-08)
系统 SHALL 提供积分任务中心，支持多种任务类型获取积分。

#### Scenario: 完成任务获取积分
- **WHEN** 用户完成每日任务或一次性任务
- **THEN** 获取对应积分，每日任务北京时间 0 点重置

### Requirement: 售后凭证与评价图片上传 (RAS-06)
系统 SHALL 支持售后凭证和评价图片上传，通过 IFileStorageService 存储。

#### Scenario: 上传图片
- **WHEN** 用户上传售后凭证或评价图片
- **THEN** 图片数量/大小/格式受限制，通过 MinIO 存储

### Requirement: 多渠道选择与故障转移 (NTF-10)
系统 SHALL 支持多渠道选择和故障转移能力。

#### Scenario: 故障转移
- **WHEN** 主服务商发送失败
- **THEN** 自动切换备服务商，不跨渠道转移

### Requirement: 渠道回执接收与状态更新 (NTF-11)
系统 SHALL 接收邮件/短信渠道回执并更新通知记录状态。

#### Scenario: 回执处理
- **WHEN** 渠道回调送达回执
- **THEN** 验签通过后更新记录状态，重复回执幂等去重

### Requirement: 发送记录查询与送达率统计 (NTF-12)
系统 SHALL 提供发送记录查询和送达率统计功能。

#### Scenario: 查询统计
- **WHEN** 运营查询发送记录或送达率统计
- **THEN** 支持多维度筛选，手机号/邮箱脱敏展示

### Requirement: 通知模板管理增强 (NTF-13)
系统 SHALL 增强通知模板管理功能，支持变量维护和预览。

#### Scenario: 模板管理
- **WHEN** 管理员编辑模板变量或预览模板
- **THEN** 变量与占位符一致性校验，预览渲染结果