# 多端前端界面设计稿生成实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 `/docs/design-prompts/` 下的 143 个设计提示词文件，为 4 端（买家端 APP、运营管理后台、商家管理后台、系统管理后台）生成 133 个页面的前端界面设计稿（自包含 HTML），统一存放至 `/docs/designs/` 下，按端和模块组织。

**Architecture:** 采用「共享设计令牌 + 4 端 40 个模块并行 subagent」方案。每个模块由一个独立 subagent 调用 frontend-design 技能，读取该模块的全部提示词文件与共享设计系统规范，生成自包含 HTML 设计稿。三端后台模拟 Ant Design Vue 4.x 视觉风格（Header 64px + Sider 200px + Content 布局），用户 APP 模拟 Vant 4.x 视觉风格（NavBar 46px + Content + Tabbar 50px 布局），所有设计稿严格遵循 `shared/design-system.md` 的 W3C DTCG 设计令牌。

**Tech Stack:** HTML5 + CSS3（内联，自包含单文件）；CSS Custom Properties 注入设计令牌；三端后台模拟 Ant Design Vue 4.x 组件视觉（`a-` 前缀风格）；用户 APP 模拟 Vant 4.x 组件视觉（`van-` 前缀风格）；图表用 SVG/Canvas 模拟 ECharts 效果；图标用内联 SVG（Ant Design Icons 风格 / Vant 内置图标风格）。

---

## 设计稿技术规范（所有 subagent 必须遵守）

### 1. 文件格式

每个页面设计稿为一个**自包含 HTML 单文件**（`.html`），内联全部 CSS，可直接用浏览器打开预览，不依赖外部资源、不依赖 npm 构建。

### 2. 设计令牌（CSS 变量，每个 HTML 文件 `<head>` 内必须定义）

```css
:root {
  /* color */
  --c-primary: #1677FF;
  --c-success: #52C41A;
  --c-warning: #FAAD14;
  --c-error: #FF4D4F;
  --c-info: #1677FF;
  --c-disabled: #00000040;
  /* neutral */
  --n1: #FFFFFF;
  --n2: #FAFAFA;
  --n3: #F5F5F5;
  --n5: #D9D9D9;
  --n7: #8C8C8C;
  --n9: #595959;
  --n10: #000000D9;
  /* radius */
  --r-base: 6px;
  --r-card: 8px;
  --r-lg: 12px;
  /* spacing */
  --s1: 4px; --s2: 8px; --s3: 12px; --s4: 16px; --s6: 24px; --s8: 32px; --s12: 48px;
  /* font */
  --fs-sm: 12px; --fs-base: 14px; --fs-lg: 16px; --fs-xl: 20px; --fs-2xl: 24px; --fs-3xl: 30px;
  --fw-normal: 400; --fw-medium: 500; --fw-semibold: 600;
  --lh-base: 1.5715;
  --ff-app: "PingFang SC","Microsoft YaHei",-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;
  --ff-mono: "SF Mono","Cascadia Code","JetBrains Mono",Consolas,monospace;
  /* shadow */
  --sh-card: 0 1px 2px 0 rgba(0,0,0,.03),0 1px 6px -1px rgba(0,0,0,.02),0 2px 4px 0 rgba(0,0,0,.02);
  --sh-dropdown: 0 6px 16px 0 rgba(0,0,0,.08),0 3px 6px -4px rgba(0,0,0,.12),0 9px 28px 8px rgba(0,0,0,.05);
  --sh-modal: 0 12px 32px 4px rgba(0,0,0,.08),0 8px 20px 8px rgba(0,0,0,.06);
  /* motion */
  --d-fast: 100ms; --d-mid: 200ms; --d-slow: 300ms;
  --ease-std: cubic-bezier(.2,0,0,1);
}
```

### 3. 三端后台布局规范（Ant Design Vue 4.x 风格）

```
┌─────────────────────────────────────────────────┐
│ Header（64px）：Logo + 面包屑 + 通知铃铛 + 用户菜单  │
├──────────┬──────────────────────────────────────┤
│  Sider   │  Content（padding 24px）              │
│ (200px)  │  页面内容区                            │
│ 深色      │                                       │
│ #001529  │                                       │
└──────────┴──────────────────────────────────────┘
```

- Sider 背景色 `#001529`，菜单激活态主色 `#1677FF`
- Content 背景 `#F5F5F5`，卡片背景 `#FFFFFF`，圆角 `8px`，阴影 `--sh-card`
- 表格行高 `48px`（运营后台）/ `55px`（其他后台），字号 `14px`
- 按钮：主色 `#1677FF` 圆角 `6px`；危险操作 `#FF4D4F`
- 图标：内联 SVG，Outlined 线性风格，尺寸 `16px`（操作列）/ `20px`（标题）

### 4. 用户 APP 布局规范（Vant 4.x 风格）

```
┌─────────────────────────┐
│ NavBar（46px）：返回 + 标题 │
├─────────────────────────┤
│  Content（padding 12px）  │
│  单列流式布局              │
├─────────────────────────┤
│ Tabbar（50px）             │
│ 首页/分类/购物车/我的       │
└─────────────────────────┘
```

- 页面宽度 `375px`（移动端基准），居中显示
- NavBar 高 `46px`，背景 `#FFFFFF`，底部边框 `1px solid #F5F5F5`
- Tabbar 高 `50px`，4 入口，激活态主色 `#1677FF`
- 卡片圆角 `12px`，padding `12px`
- 图标：内联 SVG，Vant 风格，Tabbar 图标 `24px`

### 5. 页面内容要求

- 填充**真实业务数据**（商品名、订单号、用户名等），不使用 Lorem Ipsum
- 展示所有交互状态（加载态 Skeleton、空数据 Empty、错误态）
- 表格含分页器、筛选条
- 表单含校验提示
- 看板页含统计卡片 + 图表（SVG 模拟折线/饼/柱/仪表图）
- 中文文案，技术术语保留英文

---

## Subagent 调用标准模板

每个模块 Task 执行时，主 agent 向 `general_purpose_task` subagent 发送如下结构的 prompt（将 `{占位符}` 替换为该 Task 的具体值）：

```
你是一个 frontend-design subagent。你的任务是根据 Leno 电商平台的设计提示词，生成前端界面设计稿（自包含 HTML 文件）。

## 必读输入文件

请先读取以下文件，理解设计规范与页面需求：

1. 共享设计系统规范：docs/design-prompts/shared/design-system.md
2. 共享组件清单：docs/design-prompts/shared/components.md
3. 写作指南：docs/design-prompts/shared/writing-guide.md
4. 术语表：docs/design-prompts/shared/glossary.md
5. {端}总览：docs/design-prompts/{端}/00-overview.md
6. 本模块所有页面提示词：
{逐行列出该模块每个提示词文件的完整路径}

## 输出要求

在 docs/designs/{端}/{模块}/ 目录下，为每个页面提示词生成一个同名的 .html 设计稿文件：
{逐行列出该模块每个输出文件的完整路径}

## 设计稿技术规范

1. 自包含 HTML 单文件，内联全部 CSS，可直接浏览器打开
2. <head> 内必须定义设计令牌 CSS 变量（见下方代码块）
3. {布局规范：三端后台 Ant Design Vue 风格 Header+Sider+Content / 用户APP Vant 风格 NavBar+Content+Tabbar}
4. 严格遵循设计令牌数值：主色 #1677FF、圆角 6/8/12px、间距 4/8/12/16/24/32/48px、字号 12/14/16/20/24/30px
5. 填充真实业务数据，展示完整页面内容
6. 展示加载态、空数据、错误态等边界状态
7. 图表用 SVG 模拟（折线/饼/柱/仪表图）
8. 图标用内联 SVG

{插入上方「设计令牌 CSS 变量」代码块}

## 执行步骤

1. 读取所有输入文件
2. 为每个页面生成一个自包含 HTML 设计稿
3. 确保每个 HTML 文件可独立浏览器打开，视觉完整
4. 验证输出文件数量 = {页面数}

请开始执行，完成后报告每个文件的路径。
```

---

## File Structure

```
docs/designs/
├── README.md                           # 设计稿总索引（最后生成）
├── buyer-app/                          # 买家端APP（48页面，14模块）
│   ├── 01-auth/                        # 认证（5页面）
│   ├── 02-home/                        # 首页（3页面）
│   ├── 03-catalog/                     # 商品目录（4页面）
│   ├── 04-shop/                        # 店铺（1页面）
│   ├── 05-cart/                        # 购物车（3页面）
│   ├── 06-order/                       # 订单交易（5页面）
│   ├── 07-payment/                     # 支付（2页面）
│   ├── 08-promotion/                   # 优惠（2页面）
│   ├── 09-review/                      # 评价（3页面）
│   ├── 10-after-sales/                 # 售后（3页面）
│   ├── 11-points-membership/           # 积分会员（7页面）
│   ├── 12-notification/                # 通知（2页面）
│   ├── 13-profile/                     # 我的（6页面）
│   └── 14-public/                      # 公共（2页面）
├── operations/                         # 运营管理后台（34页面，10模块）
│   ├── 01-dashboard/                   # 数据看板（6页面）
│   ├── 02-product-ops/                 # 商品运营（3页面）
│   ├── 03-promotion-ops/               # 促销运营（3页面）
│   ├── 04-seller-ops/                  # 卖家运营（3页面）
│   ├── 05-order-ops/                   # 订单运营（4页面）
│   ├── 06-payment-ops/                 # 支付运营（3页面）
│   ├── 07-notification-ops/            # 通知运营（4页面）
│   ├── 08-membership-ops/              # 会员运营（3页面）
│   ├── 09-account/                     # 个人账号（4页面）
│   └── 10-data-export/                 # 数据导出（1页面）
├── seller/                             # 商家管理后台（23页面，9模块）
│   ├── 01-onboarding/                  # 入驻与店铺（4页面）
│   ├── 02-dashboard/                   # 工作台（3页面）
│   ├── 03-product-management/          # 商品管理（4页面）
│   ├── 04-logistics/                   # 物流管理（2页面）
│   ├── 05-order-fulfillment/           # 订单履约（3页面）
│   ├── 06-after-sales/                 # 售后处理（2页面）
│   ├── 07-review/                      # 评价管理（1页面）
│   ├── 08-account/                     # 个人账号（3页面）
│   └── 09-export/                      # 报表导出（1页面）
└── system-admin/                       # 系统管理后台（28页面，7模块）
    ├── 01-dashboard/                   # 仪表盘（7页面）
    ├── 02-user-access/                 # 用户与权限（4页面）
    ├── 03-system-governance/           # 系统治理（4页面）
    ├── 04-runtime-ops/                  # 运行时运维（6页面）
    ├── 05-audit/                       # 审计与对账（3页面）
    ├── 06-account/                    # 个人账号（3页面）
    └── 07-monitoring/                 # 系统监控（1页面）
```

---

## Task 1: 创建目录结构与共享设计令牌文件

**Files:**
- Create: `docs/designs/` 下全部 40 个模块子目录
- Create: `docs/designs/_shared/tokens.css`

- [ ] **Step 1: 创建全部目录结构**

```bash
# 买家端APP 14个模块
mkdir -p docs/designs/buyer-app/{01-auth,02-home,03-catalog,04-shop,05-cart,06-order,07-payment,08-promotion,09-review,10-after-sales,11-points-membership,12-notification,13-profile,14-public}

# 运营管理后台 10个模块
mkdir -p docs/designs/operations/{01-dashboard,02-product-ops,03-promotion-ops,04-seller-ops,05-order-ops,06-payment-ops,07-notification-ops,08-membership-ops,09-account,10-data-export}

# 商家管理后台 9个模块
mkdir -p docs/designs/seller/{01-onboarding,02-dashboard,03-product-management,04-logistics,05-order-fulfillment,06-after-sales,07-review,08-account,09-export}

# 系统管理后台 7个模块
mkdir -p docs/designs/system-admin/{01-dashboard,02-user-access,03-system-governance,04-runtime-ops,05-audit,06-account,07-monitoring}

# 共享目录
mkdir -p docs/designs/_shared
```

- [ ] **Step 2: 创建共享设计令牌 CSS 文件**

创建 `docs/designs/_shared/tokens.css`，内容为上方「设计令牌 CSS 变量」代码块的完整 `:root { ... }` 定义，供各页面参考引用。

- [ ] **Step 3: 验证目录结构**

```bash
# 验证共 40 个模块目录 + 4 个端目录 + 1 个 _shared 目录
find docs/designs -type d | wc -l
# 期望：45（4端 + 40模块 + _shared + docs/designs 自身，实际含子目录可能更多，确认无遗漏即可）
```

- [ ] **Step 4: 提交**

```bash
git add docs/designs/
git commit -m "设计稿：创建4端40模块目录结构与共享设计令牌文件"
```

---

## Phase 1: 买家端 APP（14 个模块，48 个页面）

> 布局规范：Vant 4.x 移动端风格，NavBar 46px + Content + Tabbar 50px，页面宽 375px 基准。认证页无 Tabbar。秒杀与支付页隐藏 Tabbar。

### Task 2: 买家端APP - 01-auth 认证模块（5页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/01-auth/login.md`
- `docs/design-prompts/buyer-app/01-auth/register.md`
- `docs/design-prompts/buyer-app/01-auth/forgot-password.md`
- `docs/design-prompts/buyer-app/01-auth/oauth-login.md`
- `docs/design-prompts/buyer-app/01-auth/two-factor.md`

**输出设计稿**：
- `docs/designs/buyer-app/01-auth/login.html`
- `docs/designs/buyer-app/01-auth/register.html`
- `docs/designs/buyer-app/01-auth/forgot-password.html`
- `docs/designs/buyer-app/01-auth/oauth-login.html`
- `docs/designs/buyer-app/01-auth/two-factor.html`

**模块设计要点**：全屏聚焦登录任务，无 NavBar/Tabbar；品牌区 Logo + 应用名「Leno」；表单区 van-form 风格；第三方登录圆形图标按钮；密码可切换明文。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent（`general_purpose_task`），生成 5 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 5 个 HTML 文件，浏览器可打开，视觉完整
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/01-auth/
git commit -m "设计稿：买家端APP - 01-auth 认证模块（5页面）"
```

### Task 3: 买家端APP - 02-home 首页模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/02-home/home-feed.md`
- `docs/design-prompts/buyer-app/02-home/banner.md`
- `docs/design-prompts/buyer-app/02-home/seckill-entry.md`

**输出设计稿**：
- `docs/designs/buyer-app/02-home/home-feed.html`
- `docs/designs/buyer-app/02-home/banner.html`
- `docs/designs/buyer-app/02-home/seckill-entry.html`

**模块设计要点**：首页含搜索框、轮播 Banner、秒杀倒计时入口、推荐流无限滚动、分类快捷入口、公告条；Tabbar 激活态「首页」；秒杀倒计时制造紧迫感，强对比色。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/02-home/
git commit -m "设计稿：买家端APP - 02-home 首页模块（3页面）"
```

### Task 4: 买家端APP - 03-catalog 商品目录模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/03-catalog/category-nav.md`
- `docs/design-prompts/buyer-app/03-catalog/search.md`
- `docs/design-prompts/buyer-app/03-catalog/search-results.md`
- `docs/design-prompts/buyer-app/03-catalog/product-detail.md`

**输出设计稿**：
- `docs/designs/buyer-app/03-catalog/category-nav.html`
- `docs/designs/buyer-app/03-catalog/search.html`
- `docs/designs/buyer-app/03-catalog/search-results.html`
- `docs/designs/buyer-app/03-catalog/product-detail.html`

**模块设计要点**：分类导航左侧一级分类树 + 右侧二级分类商品列表；搜索页含搜索框 + 热搜 + 历史；搜索结果含筛选排序；商品详情含轮播图、规格选择、评价摘要、底部购买栏。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/03-catalog/
git commit -m "设计稿：买家端APP - 03-catalog 商品目录模块（4页面）"
```

### Task 5: 买家端APP - 04-shop 店铺模块（1页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/04-shop/shop-detail.md`

**输出设计稿**：
- `docs/designs/buyer-app/04-shop/shop-detail.html`

**模块设计要点**：店铺详情含店铺头部（Logo/名称/评分/关注按钮）、店铺商品列表、店铺优惠券；🚧 规划中状态，按提示词标注设计。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 1 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 1 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/04-shop/
git commit -m "设计稿：买家端APP - 04-shop 店铺模块（1页面）"
```

### Task 6: 买家端APP - 05-cart 购物车模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/05-cart/cart.md`
- `docs/design-prompts/buyer-app/05-cart/checkout-preview.md`
- `docs/design-prompts/buyer-app/05-cart/checkout-settle.md`

**输出设计稿**：
- `docs/designs/buyer-app/05-cart/cart.html`
- `docs/designs/buyer-app/05-cart/checkout-preview.html`
- `docs/designs/buyer-app/05-cart/checkout-settle.html`

**模块设计要点**：购物车按卖家分组，匿名/登录态 tab 切换；结算预览展示商品摘要 + 优惠 + 地址；结算拆分为 preview + settle 两步；底部结算栏含全选、合计、提交按钮。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/05-cart/
git commit -m "设计稿：买家端APP - 05-cart 购物车模块（3页面）"
```

### Task 7: 买家端APP - 06-order 订单交易模块（5页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/06-order/order-list.md`
- `docs/design-prompts/buyer-app/06-order/order-detail.md`
- `docs/design-prompts/buyer-app/06-order/order-create.md`
- `docs/design-prompts/buyer-app/06-order/logistics-trace.md`
- `docs/design-prompts/buyer-app/06-order/seckill-order.md`

**输出设计稿**：
- `docs/designs/buyer-app/06-order/order-list.html`
- `docs/designs/buyer-app/06-order/order-detail.html`
- `docs/designs/buyer-app/06-order/order-create.html`
- `docs/designs/buyer-app/06-order/logistics-trace.html`
- `docs/designs/buyer-app/06-order/seckill-order.html`

**模块设计要点**：订单列表含 tab 切换（待支付/待发货/待收货/退款售后）；订单详情含状态进度条、订单行、地址、支付信息；物流跟踪用 Steps 时间线；秒杀下单页隐藏 Tabbar，强对比色倒计时。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 5 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 5 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/06-order/
git commit -m "设计稿：买家端APP - 06-order 订单交易模块（5页面）"
```

### Task 8: 买家端APP - 07-payment 支付模块（2页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/07-payment/payment-initiate.md`
- `docs/design-prompts/buyer-app/07-payment/payment-result.md`

**输出设计稿**：
- `docs/designs/buyer-app/07-payment/payment-initiate.html`
- `docs/designs/buyer-app/07-payment/payment-result.html`

**模块设计要点**：发起支付页含支付方式选择（微信/支付宝/银联，SVG logo）、倒计时、金额展示；支付结果页含成功/失败状态大图标、订单信息、操作按钮；隐藏 Tabbar 聚焦支付任务。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 2 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 2 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/07-payment/
git commit -m "设计稿：买家端APP - 07-payment 支付模块（2页面）"
```

### Task 9: 买家端APP - 08-promotion 优惠模块（2页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/08-promotion/my-coupons.md`
- `docs/design-prompts/buyer-app/08-promotion/coupons-available.md`

**输出设计稿**：
- `docs/designs/buyer-app/08-promotion/my-coupons.html`
- `docs/designs/buyer-app/08-promotion/coupons-available.html`

**模块设计要点**：我的优惠券含 tab（未使用/已使用/已过期），优惠券卡片含金额、门槛、有效期、状态标签；领券中心展示可领优惠券列表，领取按钮。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 2 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 2 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/08-promotion/
git commit -m "设计稿：买家端APP - 08-promotion 优惠模块（2页面）"
```

### Task 10: 买家端APP - 09-review 评价模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/09-review/review-submit.md`
- `docs/design-prompts/buyer-app/09-review/my-reviews.md`
- `docs/design-prompts/buyer-app/09-review/product-reviews.md`

**输出设计稿**：
- `docs/designs/buyer-app/09-review/review-submit.html`
- `docs/designs/buyer-app/09-review/my-reviews.html`
- `docs/designs/buyer-app/09-review/product-reviews.html`

**模块设计要点**：提交评价含评分（van-rate）、文字输入、图片上传（van-uploader）、匿名开关；我的评价列表；商品评价含评分分布、标签筛选、评价列表。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/09-review/
git commit -m "设计稿：买家端APP - 09-review 评价模块（3页面）"
```

### Task 11: 买家端APP - 10-after-sales 售后模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/10-after-sales/after-sales-apply.md`
- `docs/design-prompts/buyer-app/10-after-sales/my-after-sales.md`
- `docs/design-prompts/buyer-app/10-after-sales/after-sales-detail.md`

**输出设计稿**：
- `docs/designs/buyer-app/10-after-sales/after-sales-apply.html`
- `docs/designs/buyer-app/10-after-sales/my-after-sales.html`
- `docs/designs/buyer-app/10-after-sales/after-sales-detail.html`

**模块设计要点**：申请售后含售后类型选择（退款/退货退款）、原因、金额、凭证图片；我的售后列表含状态 tab；售后详情含状态进度、协商记录。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/10-after-sales/
git commit -m "设计稿：买家端APP - 10-after-sales 售后模块（3页面）"
```

### Task 12: 买家端APP - 11-points-membership 积分会员模块（7页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/11-points-membership/points-account.md`
- `docs/design-prompts/buyer-app/11-points-membership/points-ledger.md`
- `docs/design-prompts/buyer-app/11-points-membership/check-in.md`
- `docs/design-prompts/buyer-app/11-points-membership/tasks-center.md`
- `docs/design-prompts/buyer-app/11-points-membership/points-exchange.md`
- `docs/design-prompts/buyer-app/11-points-membership/member-level.md`
- `docs/design-prompts/buyer-app/11-points-membership/membership-packages.md`

**输出设计稿**：
- `docs/designs/buyer-app/11-points-membership/points-account.html`
- `docs/designs/buyer-app/11-points-membership/points-ledger.html`
- `docs/designs/buyer-app/11-points-membership/check-in.html`
- `docs/designs/buyer-app/11-points-membership/tasks-center.html`
- `docs/designs/buyer-app/11-points-membership/points-exchange.html`
- `docs/designs/buyer-app/11-points-membership/member-level.html`
- `docs/designs/buyer-app/11-points-membership/membership-packages.html`

**模块设计要点**：积分账户展示可用积分 + 成长值 + 会员等级卡片；积分流水用列表展示收支记录；签到用日历组件；任务中心展示任务列表与完成状态；积分兑换商城；会员等级权益展示；付费会员套餐购买卡片。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 7 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 7 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/11-points-membership/
git commit -m "设计稿：买家端APP - 11-points-membership 积分会员模块（7页面）"
```

### Task 13: 买家端APP - 12-notification 通知模块（2页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/12-notification/notifications.md`
- `docs/design-prompts/buyer-app/12-notification/preferences.md`

**输出设计稿**：
- `docs/designs/buyer-app/12-notification/notifications.html`
- `docs/designs/buyer-app/12-notification/preferences.html`

**模块设计要点**：通知列表按类型分组（订单/促销/系统），含已读/未读标识；通知偏好设置含各渠道（App推送/短信/邮件）开关。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 2 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 2 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/12-notification/
git commit -m "设计稿：买家端APP - 12-notification 通知模块（2页面）"
```

### Task 14: 买家端APP - 13-profile 我的模块（6页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/13-profile/profile.md`
- `docs/design-prompts/buyer-app/13-profile/addresses.md`
- `docs/design-prompts/buyer-app/13-profile/security.md`
- `docs/design-prompts/buyer-app/13-profile/favorites.md`
- `docs/design-prompts/buyer-app/13-profile/history.md`
- `docs/design-prompts/buyer-app/13-profile/settings.md`

**输出设计稿**：
- `docs/designs/buyer-app/13-profile/profile.html`
- `docs/designs/buyer-app/13-profile/addresses.html`
- `docs/designs/buyer-app/13-profile/security.html`
- `docs/designs/buyer-app/13-profile/favorites.html`
- `docs/designs/buyer-app/13-profile/history.html`
- `docs/designs/buyer-app/13-profile/settings.html`

**模块设计要点**：个人中心含用户卡片（头像/昵称/会员等级标签）+ 功能入口宫格；地址管理含列表 + 编辑表单；安全设置含修改密码/绑定手机/绑定邮箱；收藏列表；浏览历史；设置含主题切换/通知/清除缓存。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 6 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 6 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/13-profile/
git commit -m "设计稿：买家端APP - 13-profile 我的模块（6页面）"
```

### Task 15: 买家端APP - 14-public 公共模块（2页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/buyer-app/00-overview.md`
- `docs/design-prompts/buyer-app/14-public/announcements.md`
- `docs/design-prompts/buyer-app/14-public/dictionaries.md`

**输出设计稿**：
- `docs/designs/buyer-app/14-public/announcements.html`
- `docs/designs/buyer-app/14-public/dictionaries.html`

**模块设计要点**：公告列表含已读/未读标识、置顶标记；字典页展示通用字典数据（如省市区域、分类等）。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 2 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 2 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/buyer-app/14-public/
git commit -m "设计稿：买家端APP - 14-public 公共模块（2页面）"
```

---

## Phase 2: 运营管理后台（10 个模块，34 个页面）

> 布局规范：Ant Design Vue 4.x 风格，Header 64px + Sider 200px（深色 #001529）+ Content（padding 24px）。表格行高 48px。数据看板数值 24px semibold。图表用 SVG 模拟。

### Task 16: 运营管理后台 - 01-dashboard 数据看板模块（6页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/01-dashboard/operations-overview.md`
- `docs/design-prompts/operations/01-dashboard/payment-stats.md`
- `docs/design-prompts/operations/01-dashboard/points-stats.md`
- `docs/design-prompts/operations/01-dashboard/notification-delivery.md`
- `docs/design-prompts/operations/01-dashboard/after-sales-stats.md`
- `docs/design-prompts/operations/01-dashboard/shop-ranking.md`

**输出设计稿**：
- `docs/designs/operations/01-dashboard/operations-overview.html`
- `docs/designs/operations/01-dashboard/payment-stats.html`
- `docs/designs/operations/01-dashboard/points-stats.html`
- `docs/designs/operations/01-dashboard/notification-delivery.html`
- `docs/designs/operations/01-dashboard/after-sales-stats.html`
- `docs/designs/operations/01-dashboard/shop-ranking.html`

**模块设计要点**：运营总览含 KPI 卡片（GMV/订单数/新增用户/活跃店铺）+ 趋势图；各统计页含 DashboardCard 组件 + ChartLine/ChartPie/ChartBar 图表；数据看板数值 24px semibold 突出。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 6 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 6 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/01-dashboard/
git commit -m "设计稿：运营管理后台 - 01-dashboard 数据看板模块（6页面）"
```

### Task 17: 运营管理后台 - 02-product-ops 商品运营模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/02-product-ops/product-audit.md`
- `docs/design-prompts/operations/02-product-ops/brand-management.md`
- `docs/design-prompts/operations/02-product-ops/category-management.md`

**输出设计稿**：
- `docs/designs/operations/02-product-ops/product-audit.html`
- `docs/designs/operations/02-product-ops/brand-management.html`
- `docs/designs/operations/02-product-ops/category-management.html`

**模块设计要点**：商品审核含待审核列表 + 详情抽屉（商品信息/图片/资质）+ 通过/驳回操作；品牌管理含 CRUD 表格 + 弹窗表单；分类管理含树形结构 + 拖拽排序。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/02-product-ops/
git commit -m "设计稿：运营管理后台 - 02-product-ops 商品运营模块（3页面）"
```

### Task 18: 运营管理后台 - 03-promotion-ops 促销运营模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/03-promotion-ops/promotions.md`
- `docs/design-prompts/operations/03-promotion-ops/coupons.md`
- `docs/design-prompts/operations/03-promotion-ops/seckill.md`

**输出设计稿**：
- `docs/designs/operations/03-promotion-ops/promotions.html`
- `docs/designs/operations/03-promotion-ops/coupons.html`
- `docs/designs/operations/03-promotion-ops/seckill.html`

**模块设计要点**：促销活动列表含状态筛选 + 创建表单（满减/满折/满赠）；优惠券管理含面额/门槛/有效期/领取量；秒杀活动含时间段配置 + 商品选择 + 库存预占。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/03-promotion-ops/
git commit -m "设计稿：运营管理后台 - 03-promotion-ops 促销运营模块（3页面）"
```

### Task 19: 运营管理后台 - 04-seller-ops 卖家运营模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/04-seller-ops/application-audit.md`
- `docs/design-prompts/operations/04-seller-ops/shop-governance.md`
- `docs/design-prompts/operations/04-seller-ops/seller-statistics.md`

**输出设计稿**：
- `docs/designs/operations/04-seller-ops/application-audit.html`
- `docs/designs/operations/04-seller-ops/shop-governance.html`
- `docs/designs/operations/04-seller-ops/seller-statistics.html`

**模块设计要点**：入驻审核含申请列表 + 详情抽屉（资质/证件/法人信息）+ 通过/驳回；店铺治理含店铺列表 + 状态操作（暂停/恢复/封禁）；卖家统计含排行图表。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/04-seller-ops/
git commit -m "设计稿：运营管理后台 - 04-seller-ops 卖家运营模块（3页面）"
```

### Task 20: 运营管理后台 - 05-order-ops 订单运营模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/05-order-ops/order-management.md`
- `docs/design-prompts/operations/05-order-ops/after-sales.md`
- `docs/design-prompts/operations/05-order-ops/review-audit.md`
- `docs/design-prompts/operations/05-order-ops/logistics-companies.md`

**输出设计稿**：
- `docs/designs/operations/05-order-ops/order-management.html`
- `docs/designs/operations/05-order-ops/after-sales.html`
- `docs/designs/operations/05-order-ops/review-audit.html`
- `docs/designs/operations/05-order-ops/logistics-companies.html`

**模块设计要点**：订单管理含筛选条 + 表格 + 详情抽屉 + 强制取消对话框；售后处理含售后单列表 + 处理操作；评价审核含待审评价列表 + 通过/隐藏；物流公司管理含 CRUD。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/05-order-ops/
git commit -m "设计稿：运营管理后台 - 05-order-ops 订单运营模块（4页面）"
```

### Task 21: 运营管理后台 - 06-payment-ops 支付运营模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/06-payment-ops/payment-records.md`
- `docs/design-prompts/operations/06-payment-ops/refund-records.md`
- `docs/design-prompts/operations/06-payment-ops/payment-channels.md`

**输出设计稿**：
- `docs/designs/operations/06-payment-ops/payment-records.html`
- `docs/designs/operations/06-payment-ops/refund-records.html`
- `docs/designs/operations/06-payment-ops/payment-channels.html`

**模块设计要点**：支付记录含筛选 + 表格 + 详情；退款记录含退款状态追踪；支付渠道配置含渠道开关 + 费率 + 商户号配置表单。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/06-payment-ops/
git commit -m "设计稿：运营管理后台 - 06-payment-ops 支付运营模块（3页面）"
```

### Task 22: 运营管理后台 - 07-notification-ops 通知运营模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/07-notification-ops/templates.md`
- `docs/design-prompts/operations/07-notification-ops/records.md`
- `docs/design-prompts/operations/07-notification-ops/config.md`
- `docs/design-prompts/operations/07-notification-ops/rate-limits.md`

**输出设计稿**：
- `docs/designs/operations/07-notification-ops/templates.html`
- `docs/designs/operations/07-notification-ops/records.html`
- `docs/designs/operations/07-notification-ops/config.html`
- `docs/designs/operations/07-notification-ops/rate-limits.html`

**模块设计要点**：通知模板含变量占位符编辑 + 预览；通知记录含发送记录列表 + 状态过滤；通知配置含渠道开关 + 签名配置；限流规则含阈值/窗口配置表格。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/07-notification-ops/
git commit -m "设计稿：运营管理后台 - 07-notification-ops 通知运营模块（4页面）"
```

### Task 23: 运营管理后台 - 08-membership-ops 会员运营模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/08-membership-ops/member-levels.md`
- `docs/design-prompts/operations/08-membership-ops/membership-packages.md`
- `docs/design-prompts/operations/08-membership-ops/points-rules.md`

**输出设计稿**：
- `docs/designs/operations/08-membership-ops/member-levels.html`
- `docs/designs/operations/08-membership-ops/membership-packages.html`
- `docs/designs/operations/08-membership-ops/points-rules.html`

**模块设计要点**：会员等级配置含等级阈值/权益/折扣；会员套餐管理含套餐价格/权益/有效期；积分规则含获取/消耗规则配置表。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/08-membership-ops/
git commit -m "设计稿：运营管理后台 - 08-membership-ops 会员运营模块（3页面）"
```

### Task 24: 运营管理后台 - 09-account 个人账号模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/09-account/login.md`
- `docs/design-prompts/operations/09-account/todo-workbench.md`
- `docs/design-prompts/operations/09-account/profile.md`
- `docs/design-prompts/operations/09-account/notifications.md`

**输出设计稿**：
- `docs/designs/operations/09-account/login.html`
- `docs/designs/operations/09-account/todo-workbench.html`
- `docs/designs/operations/09-account/profile.html`
- `docs/designs/operations/09-account/notifications.html`

**模块设计要点**：登录页无 Sider，居中卡片表单；待办工作台含待审核计数卡片 + 待办列表 + 快捷操作；个人资料含头像/姓名/手机/邮箱编辑表单；通知中心含通知列表。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/09-account/
git commit -m "设计稿：运营管理后台 - 09-account 个人账号模块（4页面）"
```

### Task 25: 运营管理后台 - 10-data-export 数据导出模块（1页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/operations/00-overview.md`
- `docs/design-prompts/operations/10-data-export/export-center.md`

**输出设计稿**：
- `docs/designs/operations/10-data-export/export-center.html`

**模块设计要点**：导出中心含导出任务列表（报表类型/状态/创建时间/下载链接）+ 新建导出任务表单（选择报表类型/时间范围/字段）。➕ 补充功能。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 1 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 1 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/operations/10-data-export/
git commit -m "设计稿：运营管理后台 - 10-data-export 数据导出模块（1页面）"
```

---

## Phase 3: 商家管理后台（9 个模块，23 个页面）

> 布局规范：Ant Design Vue 4.x 风格，Header 64px + Sider 200px（深色 #001529）+ Content（padding 24px）。完全遵循共享设计系统，无偏离。表格行高 55px。Header 含待发货/售后待处理 Badge 提醒。

### Task 26: 商家管理后台 - 01-onboarding 入驻与店铺模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/01-onboarding/application.md`
- `docs/design-prompts/seller/01-onboarding/shop-profile.md`
- `docs/design-prompts/seller/01-onboarding/qualifications.md`
- `docs/design-prompts/seller/01-onboarding/shop-preview.md`

**输出设计稿**：
- `docs/designs/seller/01-onboarding/application.html`
- `docs/designs/seller/01-onboarding/shop-profile.html`
- `docs/designs/seller/01-onboarding/qualifications.html`
- `docs/designs/seller/01-onboarding/shop-preview.html`

**模块设计要点**：入驻申请含分步表单（基本信息/资质上传/银行信息）+ Steps 进度条；店铺资料含店铺名称/Logo/简介编辑；资质管理含证件上传 + 过期提醒；店铺前台预览模拟买家视角。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/01-onboarding/
git commit -m "设计稿：商家管理后台 - 01-onboarding 入驻与店铺模块（4页面）"
```

### Task 27: 商家管理后台 - 02-dashboard 工作台模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/02-dashboard/overview.md`
- `docs/design-prompts/seller/02-dashboard/sales-trend.md`
- `docs/design-prompts/seller/02-dashboard/low-stock-alert.md`

**输出设计稿**：
- `docs/designs/seller/02-dashboard/overview.html`
- `docs/designs/seller/02-dashboard/sales-trend.html`
- `docs/designs/seller/02-dashboard/low-stock-alert.html`

**模块设计要点**：经营概览含 KPI 卡片（今日销售额/订单数/待发货/待处理售后）+ 快捷操作；销售趋势含 ChartLine 折线图 + 时间范围选择；库存预警含低库存商品列表 + 阈值配置。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/02-dashboard/
git commit -m "设计稿：商家管理后台 - 02-dashboard 工作台模块（3页面）"
```

### Task 28: 商家管理后台 - 03-product-management 商品管理模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/03-product-management/product-list.md`
- `docs/design-prompts/seller/03-product-management/product-edit.md`
- `docs/design-prompts/seller/03-product-management/sku-management.md`
- `docs/design-prompts/seller/03-product-management/price-history.md`

**输出设计稿**：
- `docs/designs/seller/03-product-management/product-list.html`
- `docs/designs/seller/03-product-management/product-edit.html`
- `docs/designs/seller/03-product-management/sku-management.html`
- `docs/designs/seller/03-product-management/price-history.html`

**模块设计要点**：商品列表含状态筛选 + 上下架操作；商品编辑含分步表单（基本信息/商品详情/规格/SKU/物流）+ 图片上传；SKU 简化管理含规格组合表格；价格历史含 ChartLine 价格变动趋势。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/03-product-management/
git commit -m "设计稿：商家管理后台 - 03-product-management 商品管理模块（4页面）"
```

### Task 29: 商家管理后台 - 04-logistics 物流管理模块（2页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/04-logistics/freight-templates.md`
- `docs/design-prompts/seller/04-logistics/logistics-companies.md`

**输出设计稿**：
- `docs/designs/seller/04-logistics/freight-templates.html`
- `docs/designs/seller/04-logistics/logistics-companies.html`

**模块设计要点**：运费模板含计价方式（按件/按重/按体积）+ 区域运费表格；物流公司查询含可合作物流公司列表 + 接口状态。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 2 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 2 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/04-logistics/
git commit -m "设计稿：商家管理后台 - 04-logistics 物流管理模块（2页面）"
```

### Task 30: 商家管理后台 - 05-order-fulfillment 订单履约模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/05-order-fulfillment/pending-shipment.md`
- `docs/design-prompts/seller/05-order-fulfillment/order-list.md`
- `docs/design-prompts/seller/05-order-fulfillment/logistics-trace.md`

**输出设计稿**：
- `docs/designs/seller/05-order-fulfillment/pending-shipment.html`
- `docs/designs/seller/05-order-fulfillment/order-list.html`
- `docs/designs/seller/05-order-fulfillment/logistics-trace.html`

**模块设计要点**：待发货订单含批量发货 + 物流单号录入弹窗；全部订单含筛选 + 表格 + 详情；物流轨迹含 Timeline 时间线展示物流节点。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/05-order-fulfillment/
git commit -m "设计稿：商家管理后台 - 05-order-fulfillment 订单履约模块（3页面）"
```

### Task 31: 商家管理后台 - 06-after-sales 售后处理模块（2页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/06-after-sales/after-sales-list.md`
- `docs/design-prompts/seller/06-after-sales/after-sales-detail.md`

**输出设计稿**：
- `docs/designs/seller/06-after-sales/after-sales-list.html`
- `docs/designs/seller/06-after-sales/after-sales-detail.html`

**模块设计要点**：售后列表含状态 tab（待处理/处理中/已完成）+ 操作；售后详情含售后信息 + 协商记录 + 同意/拒绝操作。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 2 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 2 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/06-after-sales/
git commit -m "设计稿：商家管理后台 - 06-after-sales 售后处理模块（2页面）"
```

### Task 32: 商家管理后台 - 07-review 评价管理模块（1页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/07-review/review-reply.md`

**输出设计稿**：
- `docs/designs/seller/07-review/review-reply.html`

**模块设计要点**：评价回复含评价列表（评分/内容/图片）+ 回复输入框 + 追评。➕ 补充功能。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 1 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 1 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/07-review/
git commit -m "设计稿：商家管理后台 - 07-review 评价管理模块（1页面）"
```

### Task 33: 商家管理后台 - 08-account 个人账号模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/08-account/login.md`
- `docs/design-prompts/seller/08-account/profile.md`
- `docs/design-prompts/seller/08-account/notifications.md`

**输出设计稿**：
- `docs/designs/seller/08-account/login.html`
- `docs/designs/seller/08-account/profile.html`
- `docs/designs/seller/08-account/notifications.html`

**模块设计要点**：登录页无 Sider 居中表单；个人资料含卖家账号信息编辑；通知中心含通知列表 + 已读/未读。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/08-account/
git commit -m "设计稿：商家管理后台 - 08-account 个人账号模块（3页面）"
```

### Task 34: 商家管理后台 - 09-export 报表导出模块（1页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/seller/00-overview.md`
- `docs/design-prompts/seller/09-export/sales-export.md`

**输出设计稿**：
- `docs/designs/seller/09-export/sales-export.html`

**模块设计要点**：销售报表导出含报表类型选择 + 时间范围 + 字段选择 + 导出任务列表 + 下载。➕ 补充功能。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 1 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 1 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/seller/09-export/
git commit -m "设计稿：商家管理后台 - 09-export 报表导出模块（1页面）"
```

---

## Phase 4: 系统管理后台（7 个模块，28 个页面）

> 布局规范：Ant Design Vue 4.x 风格，Header 64px + Sider 200px（深色 #001529）+ Content（padding 24px）+ Footer 32px。严肃专业、低频重操作。危险操作密度高，统一使用 ConfirmDialog 二次确认。表格 size="middle" 紧凑。

### Task 35: 系统管理后台 - 01-dashboard 仪表盘模块（7页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/system-admin/00-overview.md`
- `docs/design-prompts/system-admin/01-dashboard/operations-overview.md`
- `docs/design-prompts/system-admin/01-dashboard/payment-stats.md`
- `docs/design-prompts/system-admin/01-dashboard/points-stats.md`
- `docs/design-prompts/system-admin/01-dashboard/notification-delivery.md`
- `docs/design-prompts/system-admin/01-dashboard/after-sales-stats.md`
- `docs/design-prompts/system-admin/01-dashboard/shop-ranking.md`
- `docs/design-prompts/system-admin/01-dashboard/report-snapshots.md`

**输出设计稿**：
- `docs/designs/system-admin/01-dashboard/operations-overview.html`
- `docs/designs/system-admin/01-dashboard/payment-stats.html`
- `docs/designs/system-admin/01-dashboard/points-stats.html`
- `docs/designs/system-admin/01-dashboard/notification-delivery.html`
- `docs/designs/system-admin/01-dashboard/after-sales-stats.html`
- `docs/designs/system-admin/01-dashboard/shop-ranking.html`
- `docs/designs/system-admin/01-dashboard/report-snapshots.html`

**模块设计要点**：运营总览含平台级 KPI + 趋势图；各统计页含 DashboardCard + Chart 组件；通知送达率含 ChartGauge 仪表图；报表快照含历史快照列表 + 对比视图。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 7 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 7 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/system-admin/01-dashboard/
git commit -m "设计稿：系统管理后台 - 01-dashboard 仪表盘模块（7页面）"
```

### Task 36: 系统管理后台 - 02-user-access 用户与权限模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/system-admin/00-overview.md`
- `docs/design-prompts/system-admin/02-user-access/user-management.md`
- `docs/design-prompts/system-admin/02-user-access/role-management.md`
- `docs/design-prompts/system-admin/02-user-access/oauth-clients.md`
- `docs/design-prompts/system-admin/02-user-access/operators.md`

**输出设计稿**：
- `docs/designs/system-admin/02-user-access/user-management.html`
- `docs/designs/system-admin/02-user-access/role-management.html`
- `docs/designs/system-admin/02-user-access/oauth-clients.html`
- `docs/designs/system-admin/02-user-access/operators.html`

**模块设计要点**：用户管理含表格 + 封禁/解封操作（ConfirmDialog）；角色管理含角色列表 + 权限树分配；OAuth 客户端含应用列表 + 密钥管理；运营人员含账号管理 + 角色分配。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/system-admin/02-user-access/
git commit -m "设计稿：系统管理后台 - 02-user-access 用户与权限模块（4页面）"
```

### Task 37: 系统管理后台 - 03-system-governance 系统治理模块（4页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/system-admin/00-overview.md`
- `docs/design-prompts/system-admin/03-system-governance/feature-flags.md`
- `docs/design-prompts/system-admin/03-system-governance/system-configs.md`
- `docs/design-prompts/system-admin/03-system-governance/data-dictionaries.md`
- `docs/design-prompts/system-admin/03-system-governance/announcements.md`

**输出设计稿**：
- `docs/designs/system-admin/03-system-governance/feature-flags.html`
- `docs/designs/system-admin/03-system-governance/system-configs.html`
- `docs/designs/system-admin/03-system-governance/data-dictionaries.html`
- `docs/designs/system-admin/03-system-governance/announcements.html`

**模块设计要点**：功能开关含开关列表 + 影响范围说明 + ConfirmDialog；系统配置含键值对表格 + 编辑弹窗；数据字典含字典树 + 字典项管理；公告管理含公告列表 + 富文本编辑 + 发布/撤回。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 4 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 4 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/system-admin/03-system-governance/
git commit -m "设计稿：系统管理后台 - 03-system-governance 系统治理模块（4页面）"
```

### Task 38: 系统管理后台 - 04-runtime-ops 运行时运维模块（6页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/system-admin/00-overview.md`
- `docs/design-prompts/system-admin/04-runtime-ops/rate-limit-rules.md`
- `docs/design-prompts/system-admin/04-runtime-ops/index-rebuild.md`
- `docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md`
- `docs/design-prompts/system-admin/04-runtime-ops/scheduled-tasks.md`
- `docs/design-prompts/system-admin/04-runtime-ops/health-monitoring.md`
- `docs/design-prompts/system-admin/04-runtime-ops/alert-management.md`

**输出设计稿**：
- `docs/designs/system-admin/04-runtime-ops/rate-limit-rules.html`
- `docs/designs/system-admin/04-runtime-ops/index-rebuild.html`
- `docs/designs/system-admin/04-runtime-ops/dead-letter-queue.html`
- `docs/designs/system-admin/04-runtime-ops/scheduled-tasks.html`
- `docs/designs/system-admin/04-runtime-ops/health-monitoring.html`
- `docs/designs/system-admin/04-runtime-ops/alert-management.html`

**模块设计要点**：限流规则含阈值/窗口/维度配置表格 + ConfirmDialog 下发；索引重建含任务列表 + 触发按钮 + 进度展示；死信队列含消息列表 + 重投/丢弃操作（ConfirmDialog）；定时任务含 Cron 表达式 + 执行历史；健康监控含各服务状态卡片 + 指标图表；告警管理含告警列表 + 确认/静默操作。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 6 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 6 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/system-admin/04-runtime-ops/
git commit -m "设计稿：系统管理后台 - 04-runtime-ops 运行时运维模块（6页面）"
```

### Task 39: 系统管理后台 - 05-audit 审计与对账模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/system-admin/00-overview.md`
- `docs/design-prompts/system-admin/05-audit/audit-logs.md`
- `docs/design-prompts/system-admin/05-audit/reconciliation.md`
- `docs/design-prompts/system-admin/05-audit/outbox-monitor.md`

**输出设计稿**：
- `docs/designs/system-admin/05-audit/audit-logs.html`
- `docs/designs/system-admin/05-audit/reconciliation.html`
- `docs/designs/system-admin/05-audit/outbox-monitor.html`

**模块设计要点**：审计日志含 AuditLogViewer 组件 + 高级筛选 + JSON 详情展开；对账管理含对账批次列表 + 差异明细 + 状态统计；Outbox 监控含消息列表 + 投递状态 + 重发操作。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/system-admin/05-audit/
git commit -m "设计稿：系统管理后台 - 05-audit 审计与对账模块（3页面）"
```

### Task 40: 系统管理后台 - 06-account 个人账号模块（3页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/system-admin/00-overview.md`
- `docs/design-prompts/system-admin/06-account/login-2fa.md`
- `docs/design-prompts/system-admin/06-account/profile.md`
- `docs/design-prompts/system-admin/06-account/notifications.md`

**输出设计稿**：
- `docs/designs/system-admin/06-account/login-2fa.html`
- `docs/designs/system-admin/06-account/profile.html`
- `docs/designs/system-admin/06-account/notifications.html`

**模块设计要点**：登录与双因子含密码登录 + 验证码输入（6 位分格输入框）；个人中心含管理员资料编辑；通知中心含告警/待办通知列表。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 3 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 3 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/system-admin/06-account/
git commit -m "设计稿：系统管理后台 - 06-account 个人账号模块（3页面）"
```

### Task 41: 系统管理后台 - 07-monitoring 系统监控模块（1页面）

**输入提示词**：
- `docs/design-prompts/shared/design-system.md`
- `docs/design-prompts/shared/components.md`
- `docs/design-prompts/shared/writing-guide.md`
- `docs/design-prompts/shared/glossary.md`
- `docs/design-prompts/system-admin/00-overview.md`
- `docs/design-prompts/system-admin/07-monitoring/prometheus-dashboard.md`

**输出设计稿**：
- `docs/designs/system-admin/07-monitoring/prometheus-dashboard.html`

**模块设计要点**：Prometheus 监控大盘含多图表网格（CPU/内存/QPS/延迟/错误率）+ 时间范围选择 + 刷新控制。➕ 补充功能，SVG 模拟 Prometheus Grafana 风格图表。

- [ ] **Step 1**: 读取上述全部输入文件
- [ ] **Step 2**: 按标准模板调度 subagent，生成 1 个 HTML 设计稿
- [ ] **Step 3**: 验证输出 1 个 HTML 文件
- [ ] **Step 4**: 提交
```bash
git add docs/designs/system-admin/07-monitoring/
git commit -m "设计稿：系统管理后台 - 07-monitoring 系统监控模块（1页面）"
```

---

## Phase 5: 收尾验证

### Task 42: 创建索引 README 与最终验证

**Files:**
- Create: `docs/designs/README.md`

- [ ] **Step 1: 统计生成的设计稿数量**

```bash
# 统计总 HTML 文件数，期望 133
find docs/designs -name "*.html" | wc -l

# 按端统计
echo "买家端APP:" && find docs/designs/buyer-app -name "*.html" | wc -l
echo "运营管理后台:" && find docs/designs/operations -name "*.html" | wc -l
echo "商家管理后台:" && find docs/designs/seller -name "*.html" | wc -l
echo "系统管理后台:" && find docs/designs/system-admin -name "*.html" | wc -l
```

期望输出：买家端APP 48、运营管理后台 34、商家管理后台 23、系统管理后台 28，合计 133。

- [ ] **Step 2: 创建索引 README.md**

创建 `docs/designs/README.md`，内容包含：
- 项目概述（4 端 133 页面设计稿总览）
- 技术栈说明（自包含 HTML + 设计令牌）
- 目录结构树
- 4 端模块索引表（每端列出模块名、页面数、页面文件链接）
- 设计令牌参考说明（指向 `_shared/tokens.css`）
- 使用方式（浏览器直接打开 HTML 文件预览）

- [ ] **Step 3: 一致性抽检**

抽查以下页面的视觉一致性：
- 三端后台的 Header/Sider 布局是否统一（64px Header + 200px Sider 深色 #001529）
- 用户 APP 的 NavBar/Tabbar 布局是否统一（46px NavBar + 50px Tabbar）
- 所有页面的主色是否为 `#1677FF`
- 所有页面的圆角是否为 `6px`/`8px`/`12px`
- 登录页（4 端各 1 个）是否无 Sider 居中布局

- [ ] **Step 4: 推送到远程仓库**

```bash
git add docs/designs/
git commit -m "设计稿：完成4端133页面前端界面设计稿，附索引README"
git push
```

---

## Self-Review

### 1. Spec 覆盖检查

逐项对照设计提示词目录与计划 Task：

| 提示词目录 | 页面数 | 对应 Task | 覆盖 |
|-|-|-|-|
| buyer-app/01-auth | 5 | Task 2 | OK |
| buyer-app/02-home | 3 | Task 3 | OK |
| buyer-app/03-catalog | 4 | Task 4 | OK |
| buyer-app/04-shop | 1 | Task 5 | OK |
| buyer-app/05-cart | 3 | Task 6 | OK |
| buyer-app/06-order | 5 | Task 7 | OK |
| buyer-app/07-payment | 2 | Task 8 | OK |
| buyer-app/08-promotion | 2 | Task 9 | OK |
| buyer-app/09-review | 3 | Task 10 | OK |
| buyer-app/10-after-sales | 3 | Task 11 | OK |
| buyer-app/11-points-membership | 7 | Task 12 | OK |
| buyer-app/12-notification | 2 | Task 13 | OK |
| buyer-app/13-profile | 6 | Task 14 | OK |
| buyer-app/14-public | 2 | Task 15 | OK |
| operations/01-dashboard | 6 | Task 16 | OK |
| operations/02-product-ops | 3 | Task 17 | OK |
| operations/03-promotion-ops | 3 | Task 18 | OK |
| operations/04-seller-ops | 3 | Task 19 | OK |
| operations/05-order-ops | 4 | Task 20 | OK |
| operations/06-payment-ops | 3 | Task 21 | OK |
| operations/07-notification-ops | 4 | Task 22 | OK |
| operations/08-membership-ops | 3 | Task 23 | OK |
| operations/09-account | 4 | Task 24 | OK |
| operations/10-data-export | 1 | Task 25 | OK |
| seller/01-onboarding | 4 | Task 26 | OK |
| seller/02-dashboard | 3 | Task 27 | OK |
| seller/03-product-management | 4 | Task 28 | OK |
| seller/04-logistics | 2 | Task 29 | OK |
| seller/05-order-fulfillment | 3 | Task 30 | OK |
| seller/06-after-sales | 2 | Task 31 | OK |
| seller/07-review | 1 | Task 32 | OK |
| seller/08-account | 3 | Task 33 | OK |
| seller/09-export | 1 | Task 34 | OK |
| system-admin/01-dashboard | 7 | Task 35 | OK |
| system-admin/02-user-access | 4 | Task 36 | OK |
| system-admin/03-system-governance | 4 | Task 37 | OK |
| system-admin/04-runtime-ops | 6 | Task 38 | OK |
| system-admin/05-audit | 3 | Task 39 | OK |
| system-admin/06-account | 3 | Task 40 | OK |
| system-admin/07-monitoring | 1 | Task 41 | OK |

合计 40 模块 133 页面，全部覆盖。Task 1 创建目录，Task 42 创建索引与验证。

### 2. 占位符扫描

- 无 TODO/FIXME/省略标记
- 每个 Task 均列出完整的输入文件路径与输出文件路径
- Subagent 调用模板包含完整 prompt 文本
- git commit message 均为中文，符合用户规则

### 3. 一致性检查

- 所有模块 Task 的步骤结构一致（读取 -> subagent 调用 -> 验证 -> 提交）
- 输出路径命名一致（`docs/designs/{端}/{模块}/{页面名}.html`）
- 三端后台设计风格统一（Ant Design Vue 4.x），用户 APP 统一（Vant 4.x）
- 设计令牌在「设计稿技术规范」中完整定义，所有 Task 引用同一份规范

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-07-26-multi-end-ui-designs.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
