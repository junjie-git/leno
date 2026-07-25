# SKU 管理 - 商家管理后台

## 1. 页面定位
- **所属端**：商家管理后台
- **所属模块**：03-product-management（商品管理）
- **页面类型**：列表页（含表单弹窗）
- **目标用户**：卖家（Seller）
- **核心目标**：卖家管理某商品下的 SKU 集合，新增 SKU 规格组合、调整价格、补货库存，并查看每个 SKU 的可售状态。
- **访问入口**：商品编辑页「管理 SKU」链接；商品列表操作列「SKU」；URL `/products/:id/skus`。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部面包屑 + 商品摘要条 + SKU 表格 + 右侧新增/编辑 SKU 抽屉。
- **关键区域**：
  - 区域 A（面包屑）：首页 / 商品管理 / 商品列表 / SKU 管理。
  - 区域 B（商品摘要条）：`<a-descriptions>` 展示商品标题、状态 StatusTag、分类、品牌、规格维度（如颜色/尺寸）。
  - 区域 C（SKU 表格 `<a-table>`）：列含 SKU 编码、规格组合（SpecAttribute 标签组）、价格、库存、SKU 状态、销量、操作（编辑/调价/补货）。>50 行虚拟滚动。
  - 区域 D（新增/编辑抽屉 `<a-drawer>`）：规格属性表单（多组 Name-Value）、SKU 编码、价格、条形码、重量、SKU 专属图。
- **响应式断点**：≥1200px 表格全宽；992-1199px 表格横向滚动；<992px 不支持。
- **首屏内容**：商品摘要条 + SKU 表格。
- **线框图描述**：
```
┌──────────────────────────────────────────────────────────┐
│ 面包屑：首页 / 商品管理 / 商品列表 / SKU 管理                │
├──────────────────────────────────────────────────────────┤
│ 商品：手机A  [已上架]  分类：数码/手机  品牌：Apple          │
│ 规格维度：颜色 / 存储                                      │
├──────────────────────────────────────────────────────────┤
│                              [+ 新增 SKU]                 │
├──────────────────────────────────────────────────────────┤
│ 编码    | 规格        | 价格   | 库存 | 状态 | 操作        │
│ SKU001 | 黑256G      | ¥3999 | 56  | 可售 | 编辑 调价 补货│
│ SKU002 | 蓝128G      | ¥2999 | 0   | 缺货 | 编辑 调价 补货│
│ SKU003 | 白512G      | ¥4999 | 12  | 可售 | 编辑 调价 补货│
└──────────────────────────────────────────────────────────┘
                      [新增 SKU] →
┌─ 抽屉：新增 SKU ─────────────────────────────────────────┐
│ 规格属性：                                                │
│  颜色 * [黑色▼]   存储 * [256G▼]                          │
│ SKU 编码 * [SKU004    ]                                   │
│ 价格 *     [3999.00] 元                                   │
│ 条形码     [                ]                             │
│ 重量(kg)   [0.5          ]                                │
│ SKU 专属图 [上传]                                         │
│                              [取消] [确认新增]            │
└──────────────────────────────────────────────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/products/{id}` | 查询商品详情（含 SKU 集合） | Seller |
| POST | `/api/products/{id}/skus` | 新增 SKU | Seller |
| PUT | `/api/products/{id}` | 更新商品（含 SKU 编辑） | Seller |
| POST | `/api/products/{id}/skus/{skuId}/price` | 调整 SKU 价格 | Seller |

- **请求参数**：`AddSkuDto` 含 `skuCode`、`specAttributes[]`（Name/Value）、`price`、`currency`、`stockQty?`、`barcode?`、`weight?`、`imageUrl?`。`AdjustPriceDto` 含 `price`、`currency`、`reason?`。
- **响应字段**：`ProductDto.skus[]`（每个 SKU 含 `id`、`skuCode`、`price`、`currency`、`stockQty`、`specAttributes[]`、`status`、`imageUrl`）。前端按 `stockQty > 0` 与 `status` 派生可售/缺货状态。
- **数据加载策略**：进入页面调用 `GET /api/products/{id}` 一次性加载商品与全部 SKU；新增/调价/补货后重新拉取。
- **缓存策略**：不缓存，保证库存与价格时效性。

## 4. 交互流程
- **主流程**：
  1. 卖家进入页面 → 调用 `GET /api/products/{id}` → 渲染商品摘要与 SKU 表格。
  2. 卖家点击「新增 SKU」→ 右侧抽屉滑出 → 填写规格属性、编码、价格、库存等 → 校验规格组合唯一性（前端比对现有 SKU）→ 点击「确认新增」→ 调用 `POST /api/products/{id}/skus` → `message.success('SKU 新增成功')` → 关闭抽屉 → 刷新列表。
  3. 卖家点击「编辑」→ 抽屉回填该 SKU 数据 → 修改后「确认」→ 调用 `PUT /api/products/{id}`（整体更新含 SKU 集合）→ `message.success('已保存')` → 刷新。
  4. 卖家点击「调价」→ 弹出调价 Modal（输入新价格 + 变更原因）→ 调用 `POST /api/products/{id}/skus/{skuId}/price` → `message.success('价格调整成功')` → 刷新。
  5. 卖家点击「补货」→ 弹出补货 Modal（输入补货数量）→ 调用 `PUT /api/products/{id}` 更新库存 → `message.success('补货成功')` → 刷新。
- **分支流程**：
  - 规格组合重复：前端校验拦截，提示「该规格组合已存在，请勿重复添加」。
  - 价格 ≤0：前端校验拦截。
  - 商品为已上架态：新增/编辑 SKU 后产生 `ProductUpdatedEvent`，买家侧实时生效。
- **跨页面流转**：面包屑返回商品列表；调价记录跳转价格历史页。
- **状态机可视化**：SKU 状态：可售（stockQty > 0 且 status=Active）/ 缺货（stockQty = 0）/ 停用（status=Inactive）。

## 5. 组件清单
- **基础组件**：`<a-descriptions>`、`<a-table>`、`<a-drawer>`、`<a-form>`、`<a-input>`、`<a-input-number>`、`<a-select>`、`<a-modal>`、`<a-tag>`、`<a-upload>`。
- **业务组件**：`StatusTag`（见 shared/components.md §1，type="product"）— 商品状态与 SKU 状态；`IdempotencyButton`（见 shared/components.md §2）— 新增/调价/补货确认按钮；`DataTable`（见 shared/components.md §6）— SKU 表格；`EmptyState`（见 shared/components.md §5）— 无 SKU 占位。
- **图表组件**：无。
- **图标使用**：`PlusOutlined`（新增）、`EditOutlined`（编辑）、`DollarOutlined`（调价）、`StockOutlined`（补货）。
- **空状态**：`<EmptyState title="暂无 SKU" description="该商品尚未添加 SKU，点击「新增 SKU」开始配置规格" ctaText="新增 SKU" @cta-click="openAdd" />`。

## 6. 视觉规范
- **主色应用**：「新增 SKU」主按钮、调价链接使用主色 `#1677FF`；规格属性标签组使用主色边框。
- **状态色**：可售绿 `#52C41A`、缺货黄 `#FAAD14`、停用灰 `#8C8C8C`。
- **间距**：摘要条与表格间距 `16px`，表格行高 `56px`，抽屉宽度 `480px`，抽屉表单项间距 `16px`。
- **字体**：页面标题 `20px` medium，摘要 `14px` normal，表格 `14px` normal，价格 `16px` semibold。
- **图标尺寸**：操作图标 `16px`，规格标签 `default` 尺寸。

## 7. 异常处理与边界
- **加载态**：进入页面 `<a-skeleton>` 模拟表格；新增/调价/补货时按钮 loading。
- **空数据**：`<EmptyState>` 引导新增首个 SKU。
- **错误态**：网络错误 `message.error('网络异常')`；403 `message.error('无权限访问')`；规格组合重复 409，提示「规格组合已存在」。
- **权限控制**：需卖家登录态；后端校验商品归属，非本店商品返回 403。
- **并发与乐观锁**：`PUT /api/products/{id}` 携带 version；冲突返回 409，提示「商品已被他人修改，请刷新后重试」。
- **危险操作确认**：调价幅度 >20% 时 `Modal.confirm` 提示「价格变动幅度较大（涨幅/降幅 N%），确认调整？」；新增/编辑/补货无二次确认。

## 8. 验收要点
- [ ] 商品摘要条正确展示标题、状态、分类、品牌、规格维度
- [ ] SKU 表格展示编码、规格标签组、价格、库存、状态
- [ ] 新增 SKU 时前端校验规格组合唯一性
- [ ] 调价 Modal 记录变更原因，幅度 >20% 二次确认
- [ ] 补货后库存数值实时刷新
- **性能要求**：首屏加载 < 1.5s；SKU 数 >50 行虚拟滚动；调价/补货响应 < 1s。
- **可访问性**：表格有 `aria-label="SKU 列表"`；抽屉键盘可关闭；价格输入有 `aria-label`；对比度满足 WCAG AA。
