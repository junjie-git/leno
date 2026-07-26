# Leno 电商平台共享设计系统规范

**文档版本**：V1.0
**适用范围**：系统管理后台、运营管理后台、商家管理后台、用户 APP
**最后更新**：2026-07-26

本文件是 4 端 UI 设计提示词的唯一设计规范来源。所有 subagent 必须严格遵循本文件定义的技术栈、设计令牌、布局规范与组件约定，不得自行定义冲突的规范。

---

## 1. 技术栈选型

| 端 | 框架与构建 | UI 库 | 状态/路由 | 图表 | 表单 |
|-|-|-|-|-|-|
| 系统管理后台 | Vue 3.5 + TypeScript 5.x + Vite 6 | Ant Design Vue 4.x | Pinia 2.x + Vue Router 4.x | @vue-echarts 7.x（ECharts 5.5） | Ant Design Vue Form |
| 运营管理后台 | Vue 3.5 + TypeScript 5.x + Vite 6 | Ant Design Vue 4.x | Pinia 2.x + Vue Router 4.x | @vue-echarts 7.x | Ant Design Vue Form |
| 商家管理后台 | Vue 3.5 + TypeScript 5.x + Vite 6 | Ant Design Vue 4.x | Pinia 2.x + Vue Router 4.x | @vue-echarts 7.x | Ant Design Vue Form |
| 用户 APP | Vue 3.5 + TypeScript 5.x + Vite 6 + PWA | Vant 4.x | Pinia 2.x + Vue Router 4.x | — | Vant Form |

**选型理由**：
- **Vue 3.5**：当前稳定主线，`<script setup>` + Composition API 是新项目标准
- **Vite 6**：最新构建工具，HMR 与构建速度领先
- **Ant Design Vue 4.x**：与 React 版 Ant Design 5.x 设计语言对齐，支持 ConfigProvider token 主题定制，4.x 版本对 Vue 3.5 完整适配
- **Vant 4.x**：移动端 Vue 3 生态最成熟的组件库，与 Ant Design Vue 共享设计令牌
- **Pinia 2.x**：Vue 官方推荐的状态管理，TS 支持优于 Vuex
- **@vue-echarts 7.x**：ECharts 5.5 的 Vue 3 包装，Composition API 友好

---

## 2. 设计令牌（Design Tokens）

统一采用 W3C DTCG 格式，4 端共享。以下数值为硬性约束，所有页面提示词必须引用这些数值，不得使用其他数值。

> 以下表格为人类可读视图，每个令牌的 W3C DTCG JSON 表示见各小节末尾的代码块。

### 2.1 色彩

| 令牌 | 数值 | 用途 |
|-|-|-|
| `color/primary` | `#1677FF` | 主色，用于主按钮、链接、激活态、强调 |
| `color/success` | `#52C41A` | 成功状态，如审核通过、支付成功、上架中 |
| `color/warning` | `#FAAD14` | 警告状态，如待审核、临期提醒、库存预警 |
| `color/error` | `#FF4D4F` | 危险状态，如驳回、封禁、删除、强制取消 |
| `color/info` | `#1677FF` | 信息提示，与主色相同 |
| `color/disabled` | `#00000040` | 禁用态文字/图标 |

**中性色阶（1-10）**：
| 令牌 | 数值 | 用途 |
|-|-|-|
| `color/neutral/1` | `#FFFFFF` | 背景（卡片、页面） |
| `color/neutral/2` | `#FAFAFA` | 次级背景（表格行、悬停） |
| `color/neutral/3` | `#F5F5F5` | 边框/分隔线浅色 |
| `color/neutral/5` | `#D9D9D9` | 边框/分隔线标准 |
| `color/neutral/7` | `#8C8C8C` | 辅助文字 |
| `color/neutral/9` | `#595959` | 次级文字 |
| `color/neutral/10` | `#000000D9` | 主文字（88% 透明度黑） |

**DTCG JSON 表示**：

```json
{
  "$metadata": {
    "format": "w3c-dtcg",
    "version": "1.0"
  },
  "color": {
    "primary": {
      "$value": "#1677FF",
      "$type": "color",
      "$description": "主色，用于主按钮、链接、激活态、强调"
    },
    "success": {
      "$value": "#52C41A",
      "$type": "color",
      "$description": "成功状态，如审核通过、支付成功、上架中"
    },
    "warning": {
      "$value": "#FAAD14",
      "$type": "color",
      "$description": "警告状态，如待审核、临期提醒、库存预警"
    },
    "error": {
      "$value": "#FF4D4F",
      "$type": "color",
      "$description": "危险状态，如驳回、封禁、删除、强制取消"
    },
    "info": {
      "$value": "#1677FF",
      "$type": "color",
      "$description": "信息提示，与主色相同"
    },
    "disabled": {
      "$value": "#00000040",
      "$type": "color",
      "$description": "禁用态文字/图标"
    },
    "neutral": {
      "1": {
        "$value": "#FFFFFF",
        "$type": "color",
        "$description": "背景（卡片、页面）"
      },
      "2": {
        "$value": "#FAFAFA",
        "$type": "color",
        "$description": "次级背景（表格行、悬停）"
      },
      "3": {
        "$value": "#F5F5F5",
        "$type": "color",
        "$description": "边框/分隔线浅色"
      },
      "5": {
        "$value": "#D9D9D9",
        "$type": "color",
        "$description": "边框/分隔线标准"
      },
      "7": {
        "$value": "#8C8C8C",
        "$type": "color",
        "$description": "辅助文字"
      },
      "9": {
        "$value": "#595959",
        "$type": "color",
        "$description": "次级文字"
      },
      "10": {
        "$value": "#000000D9",
        "$type": "color",
        "$description": "主文字（88% 透明度黑）"
      }
    }
  }
}
```

### 2.2 圆角

| 令牌 | 数值 | 用途 |
|-|-|-|
| `radius/base` | `6px` | 按钮、输入框、标签 |
| `radius/card` | `8px` | 卡片、模态框 |
| `radius/lg` | `12px` | 大型容器、移动端卡片 |

**DTCG JSON 表示**：

```json
{
  "$metadata": {
    "format": "w3c-dtcg",
    "version": "1.0"
  },
  "radius": {
    "base": {
      "$value": "6px",
      "$type": "dimension",
      "$description": "按钮、输入框、标签"
    },
    "card": {
      "$value": "8px",
      "$type": "dimension",
      "$description": "卡片、模态框"
    },
    "lg": {
      "$value": "12px",
      "$type": "dimension",
      "$description": "大型容器、移动端卡片"
    }
  }
}
```

### 2.3 间距

基于 4px 单位，所有间距必须取自以下数值：

| 令牌 | 数值 | 用途 |
|-|-|-|
| `spacing/1` | `4px` | 图标与文字间距、紧凑内边距 |
| `spacing/2` | `8px` | 表单项间距、按钮内边距 |
| `spacing/3` | `12px` | 卡片内边距、列表项间距 |
| `spacing/4` | `16px` | 模块间距、表单行间距 |
| `spacing/6` | `24px` | 区块间距、卡片间距 |
| `spacing/8` | `32px` | 大区块间距、页面边距 |
| `spacing/12` | `48px` | 主区块间距（仅桌面端） |

**DTCG JSON 表示**：

```json
{
  "$metadata": {
    "format": "w3c-dtcg",
    "version": "1.0"
  },
  "spacing": {
    "1": {
      "$value": "4px",
      "$type": "dimension",
      "$description": "图标与文字间距、紧凑内边距"
    },
    "2": {
      "$value": "8px",
      "$type": "dimension",
      "$description": "表单项间距、按钮内边距"
    },
    "3": {
      "$value": "12px",
      "$type": "dimension",
      "$description": "卡片内边距、列表项间距"
    },
    "4": {
      "$value": "16px",
      "$type": "dimension",
      "$description": "模块间距、表单行间距"
    },
    "6": {
      "$value": "24px",
      "$type": "dimension",
      "$description": "区块间距、卡片间距"
    },
    "8": {
      "$value": "32px",
      "$type": "dimension",
      "$description": "大区块间距、页面边距"
    },
    "12": {
      "$value": "48px",
      "$type": "dimension",
      "$description": "主区块间距（仅桌面端）"
    }
  }
}
```

### 2.4 字体

| 令牌 | 数值 | 用途 |
|-|-|-|
| `font/family` | `"PingFang SC", "Microsoft YaHei", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif` | 全局字体栈 |
| `font/size/sm` | `12px` | 辅助文字、标签、表单提示 |
| `font/size/base` | `14px` | 正文、表格、表单输入 |
| `font/size/lg` | `16px` | 小标题、卡片标题 |
| `font/size/xl` | `20px` | 区块标题、页面副标题 |
| `font/size/2xl` | `24px` | 数据看板数值 |
| `font/size/3xl` | `30px` | 首页大标题（仅用户 APP） |
| `font/weight/normal` | `400` | 正文 |
| `font/weight/medium` | `500` | 标题、强调 |
| `font/weight/semibold` | `600` | 数据数值、按钮 |
| `font/line-height/base` | `1.5715` | 正文行高 |

**DTCG JSON 表示**：

```json
{
  "$metadata": {
    "format": "w3c-dtcg",
    "version": "1.0"
  },
  "font": {
    "family": {
      "$value": "\"PingFang SC\", \"Microsoft YaHei\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif",
      "$type": "fontFamily",
      "$description": "全局字体栈"
    },
    "size": {
      "sm": {
        "$value": "12px",
        "$type": "dimension",
        "$description": "辅助文字、标签、表单提示"
      },
      "base": {
        "$value": "14px",
        "$type": "dimension",
        "$description": "正文、表格、表单输入"
      },
      "lg": {
        "$value": "16px",
        "$type": "dimension",
        "$description": "小标题、卡片标题"
      },
      "xl": {
        "$value": "20px",
        "$type": "dimension",
        "$description": "区块标题、页面副标题"
      },
      "2xl": {
        "$value": "24px",
        "$type": "dimension",
        "$description": "数据看板数值"
      },
      "3xl": {
        "$value": "30px",
        "$type": "dimension",
        "$description": "首页大标题（仅用户 APP）"
      }
    },
    "weight": {
      "normal": {
        "$value": 400,
        "$type": "fontWeight",
        "$description": "正文"
      },
      "medium": {
        "$value": 500,
        "$type": "fontWeight",
        "$description": "标题、强调"
      },
      "semibold": {
        "$value": 600,
        "$type": "fontWeight",
        "$description": "数据数值、按钮"
      }
    },
    "line-height": {
      "base": {
        "$value": 1.5715,
        "$type": "number",
        "$description": "正文行高"
      }
    }
  }
}
```

### 2.5 阴影

| 令牌 | 数值 | 用途 |
|-|-|-|
| `shadow/card` | `0 1px 2px 0 rgba(0,0,0,0.03), 0 1px 6px -1px rgba(0,0,0,0.02), 0 2px 4px 0 rgba(0,0,0,0.02)` | 卡片阴影 |
| `shadow/dropdown` | `0 6px 16px 0 rgba(0,0,0,0.08), 0 3px 6px -4px rgba(0,0,0,0.12), 0 9px 28px 8px rgba(0,0,0,0.05)` | 下拉菜单、弹出层 |
| `shadow/modal` | `0 12px 32px 4px rgba(0,0,0,0.08), 0 8px 20px 8px rgba(0,0,0,0.06)` | 模态框、抽屉 |

**DTCG JSON 表示**：

```json
{
  "$metadata": {
    "format": "w3c-dtcg",
    "version": "1.0"
  },
  "shadow": {
    "card": {
      "$value": "0 1px 2px 0 rgba(0,0,0,0.03), 0 1px 6px -1px rgba(0,0,0,0.02), 0 2px 4px 0 rgba(0,0,0,0.02)",
      "$type": "shadow",
      "$description": "卡片阴影"
    },
    "dropdown": {
      "$value": "0 6px 16px 0 rgba(0,0,0,0.08), 0 3px 6px -4px rgba(0,0,0,0.12), 0 9px 28px 8px rgba(0,0,0,0.05)",
      "$type": "shadow",
      "$description": "下拉菜单、弹出层"
    },
    "modal": {
      "$value": "0 12px 32px 4px rgba(0,0,0,0.08), 0 8px 20px 8px rgba(0,0,0,0.06)",
      "$type": "shadow",
      "$description": "模态框、抽屉"
    }
  }
}
```

### 2.6 动效

| 令牌 | 数值 | 用途 |
|-|-|-|
| `motion/duration/fast` | `100ms` | 悬停、激活反馈 |
| `motion/duration/mid` | `200ms` | 折叠、展开、抽屉 |
| `motion/duration/slow` | `300ms` | 模态框、大型过渡 |
| `motion/easing/standard` | `cubic-bezier(0.2, 0, 0, 1)` | 标准缓动 |
| `motion/easing/decelerated` | `cubic-bezier(0, 0, 0, 1)` | 减速进入 |
| `motion/easing/accelerated` | `cubic-bezier(0.8, 0, 1, 1)` | 加速离开 |

**DTCG JSON 表示**：

```json
{
  "$metadata": {
    "format": "w3c-dtcg",
    "version": "1.0"
  },
  "motion": {
    "duration": {
      "fast": {
        "$value": "100ms",
        "$type": "duration",
        "$description": "悬停、激活反馈"
      },
      "mid": {
        "$value": "200ms",
        "$type": "duration",
        "$description": "折叠、展开、抽屉"
      },
      "slow": {
        "$value": "300ms",
        "$type": "duration",
        "$description": "模态框、大型过渡"
      }
    },
    "easing": {
      "standard": {
        "$value": [0.2, 0, 0, 1],
        "$type": "cubicBezier",
        "$description": "标准缓动"
      },
      "decelerated": {
        "$value": [0, 0, 0, 1],
        "$type": "cubicBezier",
        "$description": "减速进入"
      },
      "accelerated": {
        "$value": [0.8, 0, 1, 1],
        "$type": "cubicBezier",
        "$description": "加速离开"
      }
    }
  }
}
```

---

## 3. 布局栅格

### 3.1 三端后台（系统管理 / 运营管理 / 商家管理）

基于 Ant Design Vue 的 Layout 组件组合：

```
┌─────────────────────────────────────────────────┐
│ Header（64px）：Logo + 面包屑 + 用户菜单 + 通知    │
├──────────┬──────────────────────────────────────┤
│          │                                      │
│  Sider   │  Content                             │
│ (200px)  │  ┌──────────────────────────────┐   │
│ 可折叠    │  │  页面内容区（24 栅格）         │   │
│ (80px)   │  │  padding: 24px               │   │
│          │  └──────────────────────────────┘   │
│          │                                      │
└──────────┴──────────────────────────────────────┘
```

- **Header**：固定 64px 高，含 Logo、Breadcrumb、用户头像下拉菜单、通知图标
- **Sider**：默认 200px 宽，可折叠至 80px（仅显示图标），深色背景 `#001529`
- **Content**：24 栅格系统，内边距 24px，最大内容宽度自适应
- **断点**：≥1200px 全展开；992-1199px Sider 自动折叠；<992px 不支持（后台不支持移动端）

### 3.2 用户 APP

基于 Vant 4.x 的 Tabbar + NavBar 组合：

```
┌─────────────────────────┐
│ NavBar（46px）：返回 + 标题 │
├─────────────────────────┤
│                         │
│  Content                │
│  （单列流式布局）          │
│  padding: 12px          │
│                         │
│                         │
├─────────────────────────┤
│ Tabbar（50px）：          │
│ 首页 / 分类 / 购物车 / 我的 │
└─────────────────────────┘
```

- **NavBar**：固定 46px 高，含返回按钮、页面标题、右侧操作（如设置图标）
- **Content**：单列流式，padding 12px，使用 `van-list` 实现无限滚动
- **Tabbar**：固定 50px 高，4 个一级入口（首页/分类/购物车/我的），激活态使用主色 `#1677FF`
- **断点**：375px 为基准设计；≥768px 居中显示并最大宽度 480px（PWA 桌面访问场景）

---

## 4. i18n 与主题预留

### 4.1 i18n 骨架

使用 `vue-i18n 9.x`，所有文案走 `$t('namespace.key')` 或组合式 `const { t } = useI18n()`。

- **默认 locale**：`zh-CN`（中文简体）
- **fallback locale**：`zh-CN`
- **加载策略**：按端懒加载 locale 文件，避免首屏加载全部语言
- **文件组织**：`src/locales/zh-CN/{module}.json`，按 BC 模块拆分
- **提示词中标注**：所有中文文案为默认 locale，生成时输出 `zh-CN.json` 与 key 引用，不生成其他语言

### 4.2 主题预留

通过 Ant Design Vue 的 `<a-config-provider :theme="{ token: {...} }">` 注入设计令牌。

- **亮色模式**：默认，使用上文令牌数值
- **暗色模式**：预留 `algorithm: theme.darkAlgorithm` 切换点，本次不生成暗色样式
- **Vant 主题**：通过 `ConfigProvider` 组件的 `theme-vars` prop 注入 CSS 变量
- **切换入口**：三端后台在 Header 用户菜单预留"切换主题"项；用户 APP 在「设置」页预留

---

## 5. 组件库统一约定

### 5.1 数据展示组件

| 组件 | 三端后台（Ant Design Vue） | 用户 APP（Vant） |
|-|-|-|
| 表格 | `<a-table>`（虚拟滚动 `:scroll="{ y: 500 }"`，>100 行启用） | `<van-list>` + 自定义卡片 |
| 描述列表 | `<a-descriptions>` | `<van-cell-group>` |
| 统计数值 | `<a-statistic>` | 自定义数值展示 |
| 卡片 | `<a-card>` | `<van-card>` |
| 标签 | `<a-tag>` | `<van-tag>` |
| 徽标 | `<a-badge>` | `<van-badge>` |

### 5.2 表单组件

| 组件 | 三端后台 | 用户 APP |
|-|-|-|
| 表单容器 | `<a-form>` + `<a-form-item>` + `rules` | `<van-form>` + `<van-cell-group>` |
| 输入框 | `<a-input>` / `<a-input-password>` / `<a-input-number>` | `<van-field>` |
| 选择器 | `<a-select>` / `<a-tree-select>` | `<van-picker>` / `<van-popup>` |
| 日期 | `<a-date-picker>` / `<a-range-picker>` | `<van-date-picker>` |
| 开关 | `<a-switch>` | `<van-switch>` |
| 单选 | `<a-radio-group>` | `<van-radio-group>` |
| 多选 | `<a-checkbox-group>` | `<van-checkbox-group>` |
| 上传 | `<a-upload>` | `<van-uploader>` |
| 评分 | `<a-rate>` | `<van-rate>` |

### 5.3 反馈组件

| 组件 | 三端后台 | 用户 APP |
|-|-|-|
| 全局消息 | `message.success/error/warning(info)` | `showToast` / `showNotify` |
| 对话框 | `Modal.confirm` / `Modal.info` | `showConfirmDialog` / `showDialog` |
| 通知 | `notification.open` | `showNotify`（顶部） |
| 抽屉 | `<a-drawer>` | `<van-popup position="right">` |
| 骨架屏 | `<a-skeleton>` | `<van-skeleton>` |
| 加载 | `<a-spin>` / `Spin.useSpin()` | `showLoadingToast` |

### 5.4 导航组件

| 组件 | 三端后台 | 用户 APP |
|-|-|-|
| 菜单 | `<a-menu>`（侧边/顶部模式） | `<van-tabbar>` |
| 面包屑 | `<a-breadcrumb>` | — |
| 步骤条 | `<a-steps>` | `<van-steps>` |
| 标签页 | `<a-tabs>` | `<van-tabs>` |
| 分页 | `<a-pagination>` | `<van-pagination>` |

### 5.5 图表组件（仅三端后台）

统一通过 @vue-echarts 封装为业务图表组件：
- **Line**：趋势图（销售趋势、积分流水、支付统计）
- **Pie**：分布图（订单来源、支付方式分布、售后类型）
- **Bar**：排行图（店铺排行、商品销量排行）
- **Gauge**：成功率（通知送达率、对账成功率）

图表配色使用设计令牌的色彩系列，不使用 ECharts 默认配色。

---

## 6. 工程化约定（Vue 3 特有）

### 6.1 组件风格

- 统一 `<script setup lang="ts">` 语法，**不使用 Options API**
- 组件名使用多词 PascalCase（如 `ProductList.vue`、`OrderDetail.vue`）
- Props 使用 `defineProps<T>()` 泛型定义，Emits 使用 `defineEmits<T>()`
- 复杂逻辑抽取为 composable（`useXxx.ts`），放置在 `composables/` 目录

### 6.2 状态管理

- Pinia store 按 BC 模块拆分，**不使用全局单一 store**
- Store 命名：`use{Domain}Store`，如 `useOrderStore`、`useProductStore`、`useUserStore`
- Store 文件放置在 `stores/{domain}.ts`
- 跨模块共享状态通过 store 间引用，避免事件总线

### 6.3 路由

- Vue Router 4 动态路由 + 路由守卫
- 路由守卫 `beforeEach` 处理：登录态校验、角色权限校验、菜单动态加载
- 路由命名：`{module}.{page}`，如 `product.list`、`order.detail`
- 路由 meta 字段：`{ requiresAuth: true, roles: ['admin'], title: '页面标题' }`

### 6.4 请求层

- axios 封装为 `request.ts`，统一拦截器
- **请求拦截器**：注入 `Authorization: Bearer {token}`、`Idempotency-Key`（POST/PUT/DELETE）、`X-Trace-Id`
- **响应拦截器**：统一处理 401（跳登录）、403（提示无权限）、500（提示服务异常）、业务错误码（统一 message 提示）
- API 调用按 BC 模块拆分到 `api/{domain}.ts`，每个函数返回 Promise<T>

### 6.5 类型定义

- 与后端 DTO 对齐的 TypeScript interface 定义在 `types/` 目录，按 BC 分文件
- 命名：`{Domain}{Entity}Dto`，如 `ProductListItemDto`、`OrderDetailDto`
- 枚举值与后端保持一致，定义在 `types/enums.ts`

---

## 7. 设计系统约束清单（subagent 必读）

每个 subagent 在生成页面提示词时，必须遵守以下硬性约束：

1. **主色统一**：`#1677FF`，不得使用其他主色数值（如 `#1890FF`、`#0052CC`）
2. **圆角统一**：按钮/输入框 `6px`，卡片 `8px`，不得使用其他数值
3. **间距统一**：必须取自 4/8/12/16/24/32/48 体系，不得使用 5px、10px、15px 等
4. **字体统一**：PingFang SC 优先，字号取自 12/14/16/20/24/30
5. **组件库区分**：用户 APP 用 `van-` 前缀，三端后台用 `a-` 前缀，不混用
6. **实现状态标注**：每个页面提示词「页面定位」段必须标注 ✅/🚧/➕ 之一
7. **术语统一**：遵循 `glossary.md`，不使用禁用同义词
8. **共享组件引用**：跨页面共享的组件在 `components.md` 中定义，页面提示词引用而非重新定义

---

## 8. 图标规范

### 8.1 图标库选型

| 端 | 图标库 | 引入方式 |
|-|-|-|
| 三端后台（系统管理 / 运营管理 / 商家管理） | `@ant-design/icons-vue 7.x` | 按需引入组件，PascalCase 命名 |
| 用户 APP | Vant 内置图标 + 少量 SVG | `<van-icon name="xxx" />`，自定义图标用 SVG 内联 |

### 8.2 尺寸体系

所有图标尺寸取自设计令牌字号体系，不得使用其他数值：

| 尺寸 | 用途 |
|-|-|
| `12px`（`font/size/sm`） | 标签内联图标、表单提示图标 |
| `14px`（`font/size/base`） | 表单控件内联图标、输入框前缀/后缀图标 |
| `16px`（`font/size/lg`） | 按钮内图标、操作按钮图标 |
| `20px`（`font/size/xl`） | 标题旁图标、卡片标题图标 |
| `24px`（`font/size/2xl`） | 导航图标、菜单图标、Tabbar 图标 |
| `32px`（`font/size/3xl`） | 空状态图标、引导页大图标（仅用户 APP） |

### 8.3 命名约定

| 端 | 命名风格 | 示例 |
|-|-|-|
| 三端后台 | PascalCase，统一以 `Outlined` 后缀（线性风格） | `UserOutlined`、`EditOutlined`、`DeleteOutlined`、`SearchOutlined` |
| 用户 APP | kebab-case，遵循 Vant 图标库命名 | `cart-o`、`user-o`、`home-o`、`search`、`setting-o` |

### 8.4 颜色规则

- **默认态**：图标颜色继承父级文字颜色（`color: inherit` 或 `currentColor`），不硬编码图标颜色
- **激活态**：使用 `color/primary`（`#1677FF`），适用于菜单选中、Tabbar 选中、按钮主操作
- **禁用态**：使用 `color/disabled`（`#00000040`），与禁用文字颜色一致
- **辅助态**：辅助图标使用 `color/neutral/7`（`#8C8C8C`），如提示信息图标、次要操作图标
- **危险态**：删除、强制下线等危险操作图标使用 `color/error`（`#FF4D4F`）

### 8.5 使用约束

#### 8.5.1 三端后台（Ant Design Vue 图标）

- **引入方式**：通过组件方式按需引入，禁止使用字体图标方式
  ```vue
  <script setup lang="ts">
  import { UserOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons-vue'
  </script>

  <template>
    <a-button type="primary">
      <template #icon>
        <EditOutlined />
      </template>
      编辑
    </a-button>
  </template>
  ```
- **尺寸控制**：通过 `style` 或父级 `font-size` 控制，默认 16px（按钮内）、14px（表单内）
- **禁止**：不使用 `<a-icon type="user" />` 字体图标语法（Ant Design Vue 4.x 已废弃该用法）

#### 8.5.2 用户 APP（Vant 图标）

- **引入方式**：通过 `<van-icon>` 组件引用 Vant 内置图标
  ```vue
  <template>
    <van-tabbar v-model="active">
      <van-tabbar-item icon="home-o">首页</van-tabbar-item>
      <van-tabbar-item icon="cart-o">购物车</van-tabbar-item>
    </van-tabbar>
  </template>
  ```
- **自定义图标**：使用 SVG 内联方式，不引入第三方图标库
  ```vue
  <template>
    <van-icon :name="customIcon" />
  </template>

  <script setup lang="ts">
  const customIcon = 'data:image/svg+xml;base64,...' // SVG 内联 base64 或直接 SVG 标签
  </script>
  ```
- **尺寸控制**：通过 `size` prop 或父级 `font-size` 控制，Tabbar 默认 24px，NavBar 操作默认 20px

### 8.6 跨端禁止规则

- **禁止混用图标库**：
  - 三端后台不得使用 Vant 图标（`van-icon`）
  - 用户 APP 不得使用 `@ant-design/icons-vue` 图标
- **禁止字体图标**：除 Vant 内置图标外，不引入 iconfont、font-awesome 等字体图标库
- **禁止远程图标**：所有图标必须本地打包，不通过 CDN 或远程 URL 引用

### 8.7 业务专用图标

业务专用图标（如支付渠道 logo、品牌 logo、特殊业务符号）统一使用 SVG 内联方式：

- **存放位置**：`src/assets/icons/` 目录，按业务模块分子目录
  ```
  src/assets/icons/
  ├── payment/         # 支付渠道 logo
  │   ├── alipay.svg
  │   ├── wechat-pay.svg
  │   └── unionpay.svg
  ├── brand/           # 品牌 logo
  └── status/          # 业务状态图标
  ```
- **引入方式**：
  - 三端后台：使用 Vue 组件封装 SVG（推荐 `vite-plugin-svg-icons` 或手动封装 `SvgIcon.vue`）
  - 用户 APP：通过 `van-icon` 的 `name` 属性传 SVG 路径，或直接 `<img src="...">` 引用
- **命名约定**：kebab-case，如 `alipay.svg`、`wechat-pay.svg`
- **色彩处理**：彩色 logo 保留原色；单色图标使用 `fill="currentColor"` 以继承父级颜色
