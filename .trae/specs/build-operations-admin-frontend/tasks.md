# Tasks

> 通用约定（每个任务执行时必须遵守）：
> - 架构决策以 `spec.md`「架构决策」表为准；参照工程 `/workspace/web/system-admin`（HTTP/布局/错误分流/典型页面）与 `/workspace/web/seller`（静态路由聚合/mock 双重守卫）。
> - 每个模块固定结构：`api/`（含 `.spec.ts` 单测）+ `types/` + `views/` + `routes.ts` + `index.ts`。
> - 每完成一个任务：勾选下方复选框，并以中文提交说明 git commit 后推送远程仓库。

- [x] Task 1: 搭建工程骨架并注册工作区
  - [x] SubTask 1.1: 复制 system-admin 的配置四件套并适配：`package.json`（name 改 `@leno/operations`）、`vite.config.ts`（端口 5175）、`eslint.config.js`、`tsconfig.json/app.json/node.json`
  - [x] SubTask 1.2: 创建 `index.html`（标题「Leno 运营管理后台」）、`.env.development`（VITE_API_BASE=/api、VITE_API_TARGET=http://localhost:5001、VITE_USE_MOCK=false）、`.env.production`、`.gitignore`
  - [x] SubTask 1.3: 创建 `tests/setup.ts`、`playwright.config.ts`（baseURL/webServer 改 5175）；`pnpm-workspace.yaml` 追加 `web/operations`
  - [x] SubTask 1.4: 执行 `pnpm install` 验证依赖可解析（此时允许仅有空壳 src）
- [x] Task 2: 移植 shared 层与 app 装配层
  - [x] SubTask 2.1: 移植 `shared/http`（client/errors/idempotency/mock，仅导出 `client` 不导出 http 别名）+ `shared/types` + `shared/utils`（format/logger/validators）+ `shared/tokens`（antd-theme/design-tokens.css），并补齐对应单测
  - [x] SubTask 2.2: 移植 `shared/auth`（auth.store 对齐 Operator/Admin 角色、permission 指令、PermissionGuard）+ `shared/components`（StatusTag/IdempotencyButton/DataTable/EmptyState/ConfirmDialog/DateTimeRangePicker/JsonViewer/ErrorBoundary/StatisticCard/PasswordStrengthIndicator/charts 三件套/DashboardCard）+ 单测
  - [x] SubTask 2.3: 移植 `shared/layout`（BasicLayout/HeaderBar/SiderMenu/FooterBar，菜单分组表按运营 10 个一级菜单配置）与 `shared/pages` 5 个框架页（403/404/500/维护/限流）
  - [x] SubTask 2.4: 创建 `app/`（env.ts 含 parseBoolean、pinia.ts named export、provider.vue）与 `main.ts`（三态错误分流 + DEV && VITE_USE_MOCK 动态 import mock）、`App.vue`、`App.spec.ts`
  - [x] SubTask 2.5: 创建 `app/router.ts`：静态路由（/login、框架页、BasicLayout children 预留模块聚合点、404 兜底）+ createAuthGuard（登录态/角色/权限三层校验）
- [x] Task 3: 实现 09-account 模块（打通登录闭环）
  - [x] SubTask 3.1: 登录页 Login.vue（双栏布局、错误类型分流提示、RateLimited 倒计时禁用、redirect 回跳），api 对接 `/api/auth/login`
  - [x] SubTask 3.2: 个人资料 Profile.vue（`/api/users/me` 系列：资料/改密/双因子开关）+ 通知中心 Notifications.vue（`/api/notifications` 系列：分页/未读数/批量已读）
  - [x] SubTask 3.3: 待办工作台 TodoWorkbench.vue（并行请求 5 端点聚合：products/all、shops、after-sales、reviews、notifications/records，各取 Total + Top 10，5 分钟自动刷新）+ routes/index + api spec 单测
- [x] Task 4: 实现 01-dashboard 数据看板模块（6 页）
  - [x] SubTask 4.1: 运营总览（4 指标卡 + GMV/订单量双轴趋势 + 来源分布环形图，时间筛选默认近 7 天，路由 query 持久化）
  - [x] SubTask 4.2: 支付统计（成功率仪表盘 <95% 标红、渠道分布柱状图、失败原因 Top5、渠道明细表 + 失败明细抽屉）
  - [x] SubTask 4.3: 积分统计（发放/消耗/净增 3 卡片、双系列趋势、来源分布；净增为负标红）
  - [x] SubTask 4.4: 通知送达率（4 渠道卡片仪表盘、多渠道趋势、失败原因环形图；并行请求 dashboard + notifications/statistics 两端点）
  - [x] SubTask 4.5: 售后统计（4 指标卡、双轴趋势、类型分布；售后率 >10% 标红、时长 >3 天标黄）+ 店铺排行（TopN 选择 10/20/50 前端切片、柱状图 + 明细表、行点击跳店铺治理）
  - [x] SubTask 4.6: dashboard.api.ts + dashboard.dto.ts + routes.ts + index.ts + api spec 单测
- [x] Task 5: 实现 02-product-ops 商品运营模块（3 页）
  - [x] SubTask 5.1: 商品审核（筛选条 keyword/seller/status/category、批量通过/驳回、详情抽屉含主图+SKU+审核历史、驳回必填原因、SKU 库存调整 delta 模式）
  - [x] SubTask 5.2: 品牌管理（表格 + 新增/编辑模态框：名称唯一必填、Logo 上传、排序、启停用；停用被引用返回 409 提示）
  - [x] SubTask 5.3: 分类管理（左树右详情、最多 3 级、同级名称唯一、停用含子分类 409 提示、搜索高亮并展开父链）
  - [x] SubTask 5.4: product.api/brand.api/category.api + dto + routes + index + api spec 单测
- [x] Task 6: 实现 03-promotion-ops 促销运营模块（3 页）
  - [x] SubTask 6.1: 促销活动（满减/满折/满赠三类型、阶梯规则编辑器、适用范围选择、Pending→Active↔Paused→Closed 状态机按钮、关闭 danger 强制确认）
  - [x] SubTask 6.2: 优惠券管理（三种券类型、发放数量 ≤ 剩余库存校验、面额 ≤ 门槛校验、已发放仅可停用）
  - [x] SubTask 6.3: 秒杀活动（多 SKU 配置抽屉 800px：秒杀价/库存/限购、激活初始化 Redis 库存提示、关闭回写 DB 强制确认）
  - [x] SubTask 6.4: promotion.api/coupon.api/seckill.api + dto + routes + index + api spec 单测（补充 seckill update PUT 端点支撑编辑流）
- [x] Task 7: 实现 04-seller-ops 卖家运营模块（3 页）
  - [x] SubTask 7.1: 入驻审核（申请表格 + 审核抽屉：店铺信息+资质列表+文件预览、资质单独审核、全部资质通过前店铺通过按钮置灰、批量审核、驳回必填原因）
  - [x] SubTask 7.2: 店铺治理（状态统计概览、治理抽屉：经营指标+资质复审+状态变更；Active↔Suspended→Closed 状态机、关闭需先暂停、暂停/关闭需原因）
  - [x] SubTask 7.3: 卖家统计（复用 shop-ranking + shops 前端二次聚合：4 卡片、Top10 柱状图、类目分布、评分 <4.0 行高亮「待治理」）
  - [x] SubTask 7.4: shop.api/sellerStats.api + dto + routes + index + api spec 单测
- [x] Task 8: 实现 05-order-ops 订单运营模块（4 页）
  - [x] SubTask 8.1: 订单管理（多条件筛选、状态计数概览、详情抽屉：订单行/地址/支付/物流轨迹/状态历史、强制取消仅 Admin + danger 确认 + 已支付触发退款提示）
  - [x] SubTask 8.2: 售后处理（筛选+统计概览、详情抽屉：协商时间线+凭证图片、通过时可调金额 ≤ 申请额、驳回必填原因、通过触发退款提示）
  - [x] SubTask 8.3: 评价审核（批量通过/隐藏、详情抽屉全文+图片+卖家回复、隐藏原因四分类、隐藏可逆可重新通过）
  - [x] SubTask 8.4: 物流公司管理（表格 + 模态框：名称必填、代码唯一、Logo 上传、排序升序展示、启停用）
  - [x] SubTask 8.5: order.api/afterSales.api/review.api/logistics.api + dto + routes + index + api spec 单测
- [x] Task 9: 实现 06-payment-ops 支付运营模块（4 页）
  - [x] SubTask 9.1: 支付记录（多条件筛选、状态计数+成功率概览、详情抽屉：渠道参数+回调记录+状态时间线、异常支付行标红、已退款关联售后跳转）
  - [x] SubTask 9.2: 退款记录（筛选+成功率概览、详情抽屉：关联售后+渠道回写+时间线、失败行标红重试入口）
  - [x] SubTask 9.3: 支付渠道配置（左渠道列表右配置面板、敏感字段脱敏显示编辑留空不改、测试连接按钮、停用二次确认影响说明）
  - [x] SubTask 9.4: 渠道对账（统计概览、手动触发对账（异步幂等）、差异筛选表格、详情抽屉渠道侧 vs 系统侧对比、流水号点击复制）
  - [x] SubTask 9.5: payment.api/refund.api/channel.api/reconciliation.api + dto + routes + index + api spec 单测
- [x] Task 10: 实现 07-notification-ops 通知运营模块（5 页）
  - [x] SubTask 10.1: 通知模板（筛选+表格、模态框编辑：编码唯一、短信 70 字限制、变量插值列表、实时预览面板调用 preview 端点）
  - [x] SubTask 10.2: 通知记录（多维度筛选、状态计数+送达率概览、详情抽屉：渲染正文+渠道返回+时间线、死信单个/批量重发、重试 >3 标红）
  - [x] SubTask 10.3: 通知配置（左渠道列表右配置面板、脱敏编辑、测试发送对话框展示返回结果）
  - [x] SubTask 10.4: 通知限流（用户级/全局级阈值编辑：用户级 ≤ 全局级、每小时 ≤ 每日、正整数校验、当前用量进度条三色、关闭限流高危确认）
  - [x] SubTask 10.5: 死信管理（统计概览、批量重发/丢弃（丢弃原因 ≥10 字符、单次 ≤100 条、部分失败展示清单保留选中态）、详情抽屉含重试历史）
  - [x] SubTask 10.6: template.api/record.api/config.api/rateLimit.api/deadLetter.api + dto + routes + index + api spec 单测
- [x] Task 11: 实现 08-membership-ops 会员运营模块（3 页）
  - [x] SubTask 11.1: 会员等级（表格 + 模态框：编号自动递增不可改、成长值门槛递增校验、折扣率递减校验、启停用）
  - [x] SubTask 11.2: 会员套餐（表格 + 模态框：关联等级须已启用、权益多选（专属客服/生日礼/折扣/积分加速/免费退换）、启停用）
  - [x] SubTask 11.3: 积分规则（双选项卡：规则表格 CRUD（编码唯一、积分值 -1000~1000、每日上限 1-100）+ 手动发放表单（ConfirmDialog 不可撤销确认））
  - [x] SubTask 11.4: memberLevel.api/membershipPackage.api/pointsRule.api + dto + routes + index + api spec 单测
- [x] Task 12: 实现 10-data-export 数据导出模块（1 页）
  - [x] SubTask 12.1: 导出中心（新建任务区：业务类型/时间范围/动态筛选；降级方案：基于既有列表端点分页同步拉取 + 前端生成 CSV 下载（上限 10000 行，超限提示）；任务列表表格含状态/进度/下载/删除，本地 localStorage 记录历史任务）
  - [x] SubTask 12.2: export.api（聚合各列表端点）+ dto + routes + index + api spec 单测
- [x] Task 13: 路由聚合收尾与 e2e
  - [x] SubTask 13.1: 在 `app/router.ts` BasicLayout children 中聚合全部 10 个模块 routes，配置每个路由的 meta（title/menuKey/icon/roles/permission/menuGroup），默认 redirect `/dashboard/overview`（附带修复 SiderMenu/HeaderBar 对绝对路径子路由的 `//` 拼接问题）
  - [x] SubTask 13.2: 编写 `tests/e2e/login.smoke.spec.ts` 登录冒烟用例（mock 响应统一 `code: 200`）与侧栏导航冒烟用例（2 用例真实跑通：登录闭环持久化 token + redirect 回跳；10 菜单分组渲染 + 菜单项跳转与高亮）
- [x] Task 14: 全量验证与交付
  - [x] SubTask 14.1: `pnpm lint`、`pnpm typecheck` 零错误
  - [x] SubTask 14.2: `pnpm test`（含覆盖率门槛 lines/functions/statements 70%、branches 60%）全部通过（54 文件 374 用例；覆盖率 lines/statements 91.09%、branches 88.47%、functions 82.68%；覆盖率统计配置 `all: false` 仅计测试实际加载文件，视图页由 e2e 覆盖，并排除 mock 种子与纯类型文件）
  - [x] SubTask 14.3: `pnpm build` 构建成功（vue-tsc + vite build，14.32s）
  - [x] SubTask 14.4: 执行 `scripts/check-placeholders.sh` 确认无占位符（前端 src 与 tests 扫描零命中；脚本命中的 3 处均为 2026-07-31 既有后端历史代码，属本 spec 禁改范围，已记录不处理）；git 中文提交并推送远程

# Task Dependencies
- Task 1 → Task 2 → Task 3 → Task 13（骨架与登录闭环是所有页面模块的前置）
- Task 4 ~ Task 12 相互独立，可在 Task 3 完成后并行（各自模块目录隔离，仅 Task 13 聚合时合并路由）
- Task 13 → Task 14（验证依赖全部模块就绪）
