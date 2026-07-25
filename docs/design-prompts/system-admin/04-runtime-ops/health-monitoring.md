# 健康监控 - 系统管理后台

## 1. 页面定位
- **所属端**：系统管理后台
- **所属模块**：04-runtime-ops 运行时运维
- **页面类型**：看板页
- **目标用户**：系统管理员（Admin）
- **核心目标**：聚合各模块 /health 端点状态，查看整体健康与各模块依赖项（DB/Redis/ES/MQ/支付渠道/通知渠道）明细，定位不健康模块。
- **访问入口**：Sider「运行时运维 → 健康监控」
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部整体状态条 + 模块健康网格 + 模块详情抽屉。
- **关键区域**：
  - 区域 A（整体状态条）：`<a-alert>` 显示整体健康状态（健康/降级/不健康）+ 检查时间 + 「立即检查」按钮。
  - 区域 B（模块网格）：`<a-row>` 排列各模块卡片，每卡片含模块名/状态徽标/依赖项数/不健康依赖数，按状态排序（不健康优先）。
  - 区域 C（模块详情抽屉）：`<a-drawer width="640">` 展示该模块全部依赖项明细（DB/Redis/ES/MQ 等），每项含状态/延迟/错误信息/最近检查时间。
- **响应式断点**：≥1200px 网格 4 列；992-1199px 2 列。
- **首屏内容**：整体状态 + 各模块卡片。
- **线框图描述**：

```
┌────────────────────────────────────────────────┐
│ 整体状态：🟡 降级 │ 检查时间 07-26 14:30 │ [立即检查]│
├──────────┬──────────┬──────────┬──────────────┤
│ UserAuth │ Product  │ Order    │ Payment      │
│ ✅ 健康   │ ✅ 健康  │ 🟡 降级  │ ❌ 不健康    │
│ 6 依赖    │ 4 依赖   │ 1 不健康 │ 2 不健康     │
└──────────┴──────────┴──────────┴──────────────┘
→ 抽屉：依赖项明细（名称/状态/延迟/错误/最近检查）
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/admin/health` | 获取聚合健康状态（整体 + 各模块） | Admin,Operator |
| GET | `/api/admin/health/modules` | 获取各模块健康详情列表 | Admin,Operator |

- **请求参数**：无（全量聚合）；后端并发拉取各模块 /health 端点，超时 3s 归为不健康。
- **响应字段**：`HealthAggregationResultDto` 含 `OverallStatus`（Healthy/Degraded/Unhealthy）、`CheckedAt`、`Modules:[ModuleHealthDto]`；`ModuleHealthDto` 含 `ModuleName`、`Status`、`LatencyMs`、`Dependencies:[{Name,Status,LatencyMs,Error,LastCheckedAt}]`。
- **数据加载策略**：进入页面并行加载整体状态 + 模块详情；每 30s 自动轮询刷新；点击「立即检查」立即刷新。
- **缓存策略**：不缓存（健康状态实时）。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行 GET `/api/admin/health` + GET `/api/admin/health/modules` → 状态条 + 网格渲染。
  2. 每 30s 自动轮询刷新整体状态。
  3. 点击「立即检查」 → 重新请求 → 全部刷新。
  4. 点击模块卡片 → 抽屉展示依赖项明细。
  5. 不健康模块触发 `notification.error` 告警（首次进入时）。
- **分支流程**：
  - 模块健康端点不可达：该模块归为 Unhealthy，依赖项 Error 显示「端点不可达」。
  - 整体状态取各模块最差：若有 1 个 Unhealthy 则整体 Unhealthy。
- **跨页面流转**：点击「查看历史告警」跳 `/runtime-ops/alert-management?module={moduleName}`。
- **状态机可视化**：Healthy → Degraded → Unhealthy，`StatusTag` 自定义 health 类型：健康绿、降级黄、不健康红。

## 5. 组件清单
- **基础组件**：`<a-row>`/`<a-col>`、`<a-card>`、`<a-alert>`、`<a-drawer>`、`<a-descriptions>`、`<a-tooltip>`、`<a-tag>`
- **业务组件**：
  - `StatusTag`（见 shared/components.md §1）— 健康状态
  - `EmptyState`（见 shared/components.md §5）
- **图表组件**：无
- **图标使用**：`CheckCircleFilled`（绿）、`ExclamationCircleFilled`（黄）、`CloseCircleFilled`（红）、`ReloadOutlined` 16px。
- **空状态**：「暂无健康数据，请稍后重试」+ CTA「立即检查」。

## 6. 视觉规范
- **主色应用**：检查按钮主色；模块卡片边框按状态色（绿/黄/红）。
- **状态色**：Healthy `#52C41A`、Degraded `#FAAD14`、Unhealthy `#FF4D4F`。
- **间距**：网格卡片间距 16px；卡片内边距 16px；抽屉内边距 24px。
- **字体**：模块名 16px medium；状态文字 14px；延迟数值 12px `#595959`。
- **图标尺寸**：状态图标 20px；操作图标 16px。

## 7. 异常处理与边界
- **加载态**：状态条 `<a-skeleton>`；网格卡片骨架。
- **空数据**：`EmptyState` 兜底。
- **错误态**：聚合接口失败 `message.error('健康检查失败')` 3s；保留上次成功数据。
- **权限控制**：页面级 `roles: ['Admin','Operator']`；Operator 仅看概览不看依赖项明细（`PermissionGuard permission="health:detail"`）。
- **并发与乐观锁**：只读无锁。
- **危险操作确认**：无危险操作。

## 8. 验收要点
- [ ] 整体状态取各模块最差
- [ ] 每 30s 自动轮询刷新
- [ ] 不健康模块卡片置顶
- [ ] 首次进入有不健康模块触发 error 通知
- **性能要求**：首屏 < 1.5s；轮询不阻塞 UI；模块数 < 20 无需虚拟滚动。
- **可访问性**：状态图标有 `aria-label`；卡片支持键盘聚焦；抽屉聚焦管理。
