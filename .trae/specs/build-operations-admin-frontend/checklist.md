# Checklist

## 工程与构建
- [ ] `pnpm-workspace.yaml` 已注册 `web/operations`，`pnpm install` 成功
- [ ] `pnpm dev` 在 5175 端口启动，`/api` 代理指向 `http://localhost:5001`
- [ ] `pnpm lint` 零错误
- [ ] `pnpm typecheck` 零错误
- [ ] `pnpm test` 全部通过且覆盖率达标（lines/functions/statements ≥70%、branches ≥60%）
- [ ] `pnpm build` 构建成功

## 架构一致性
- [ ] HTTP 层仅导出 `client`（无 `http` 别名），写操作携带 `Idempotency-Key`，响应解包 `data` 并按状态码映射强类型错误
- [ ] auth store 持久化仅 pick 必要字段（token/user/roles/permissions/expiresAt）
- [ ] 路由守卫三层校验：登录态 → 角色（Operator/Admin）→ meta.permission
- [ ] BasicLayout：Header 64px / Sider 200px 深色 #001529 可折叠 / 992-1199px 自动折叠 / <992px 桌面端提示
- [ ] 侧栏 10 个一级菜单分组与总览文档一致且当前项高亮
- [ ] 每个模块固定结构齐全：api/ + types/ + views/ + routes.ts + index.ts

## 页面完整性（36 业务页 + 5 框架页）
- [ ] 01-dashboard：运营总览/支付统计/积分统计/通知送达率/售后统计/店铺排行 6 页实现，看板含加载/空/错误三态
- [ ] 02-product-ops：商品审核/品牌管理/分类管理 3 页实现，审核驳回必填原因，批量操作可用
- [ ] 03-promotion-ops：促销活动/优惠券/秒杀 3 页实现，状态机操作（激活/暂停/关闭）带强制确认
- [ ] 04-seller-ops：入驻审核/店铺治理/卖家统计 3 页实现，资质单独审核、店铺关闭需先暂停
- [ ] 05-order-ops：订单管理/售后处理/评价审核/物流公司 4 页实现，强制取消仅 Admin 可见
- [ ] 06-payment-ops：支付记录/退款记录/支付渠道/渠道对账 4 页实现，敏感字段脱敏
- [x] 07-notification-ops：通知模板/通知记录/通知配置/通知限流/死信管理 5 页实现，批量丢弃原因 ≥10 字符
- [x] 08-membership-ops：会员等级/会员套餐/积分规则 3 页实现，含手动发放积分确认
- [x] 09-account：登录/待办工作台/个人资料/通知中心 4 页实现，登录闭环 + redirect 回跳
- [x] 10-data-export：导出中心实现，降级导出方案可用（≤10000 行）
- [ ] 11-framework：403/404/500/维护/限流 5 个框架页实现，未匹配路由兜底 404
- [ ] 页面视觉对齐设计稿（主色 #1677FF、圆角 6/8px、表格行高 48px、看板数值 24px semibold）

## 质量红线
- [ ] 全部源码无 TODO/FIXME/占位注释/空函数体/未实现分支（check-placeholders.sh 通过）
- [ ] 每个 `*.api.ts` 均有对应 `.spec.ts` 单测（axios-mock-adapter）
- [ ] e2e 登录冒烟用例通过（mock 响应 code: 200）
- [ ] 未修改任何后端代码与 system-admin/seller 既有工程

## 交付
- [ ] 任务完成情况已在 tasks.md 勾选
- [ ] 变更已按任务粒度以中文提交说明 commit 并推送远程仓库
