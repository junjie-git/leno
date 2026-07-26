# BC{N} {BC 中文名} — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC{N}
- **中文名**：{BC 中文名}
- **英文名**：{BC 英文名}
- **涉及端**：buyer-app / operations / seller / system-admin（勾选实际涉及的）
- **涉及页面数**：{N} 页（来自 feature-list）
- **已实现 API 端点数**：{N} 个（来自源码 Controller 扫描）
- **差异统计**：缺失 {X} / 闲置 {Y} / 路径不一致 {Z} / 能力不匹配 {W}

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| ... | ... | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L1) | ... | ... |

> 来源：grep `src/Services/{BC 目录}/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| ... | ... | [page.md](file:///e:/Leno/docs/design-prompts/{端}/{模块}/page.md) | ... | ✅/🚧/➕ | ... |

> 来源：design-prompts 的「数据与 API」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| ... | ... | [page.md](file:///e:/Leno/docs/design-prompts/{端}/{模块}/page.md) | ... | P0/P1/P2 | ... |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| ... | ... | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L1) | ... | 保留观察/设计稿补调用/后端废弃 |

> 说明：源码有实现但 design-prompts 中无任何页面引用

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| POST→PUT | /api/x → /api/y | [page.md](file:///e:/Leno/docs/design-prompts/{端}/{模块}/page.md) | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L1) | 改文档/改代码 |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 分页+筛选+排序 | 分页 | 缺少筛选与排序 | [page.md](file:///e:/Leno/docs/design-prompts/{端}/{模块}/page.md) | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L1) | 补 query 参数 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异

## 5. 拆分过渡说明

> 仅 BC1 / BC6 / BC7 出现此节。其他 BC 写「本 BC 无拆分过渡」一句话。

- **旧 BC 与新 BC 对照**：
- **双轨期端点引用规范**：
- **待切换端点清单**：

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | ... | ... | ... | ... |
| P1 | ... | ... | ... | ... |
| P2 | ... | ... | ... | ... |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖
- **上游依赖**：本 BC 依赖哪些 BC 的端点/事件
- **下游依赖**：哪些 BC 依赖本 BC 的端点/事件
- **集成事件订阅/发布清单**

## 8. 行动建议
- **立即修复**（P0 缺失/不一致）
- **短期补充**（P1 缺失/不匹配）
- **长期规划**（P2 闲置/废弃）
- **文档同步**（design-prompts API 引用对齐到源码）
