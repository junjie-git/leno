# ADR-0003: AntiCorruptionDispatcher 适配器模式

## 状态
已接受（2026-07-19，Task 15 实施时发现）

## 上下文
M4 spec 原设计 `AntiCorruptionDispatcher<TService>` 应实现 `TService` 接口，
业务层直接注入 Dispatcher。但实施时发现：

- `Dispatcher.ExecuteAsync<TResult>(Func<TService, Task<TResult>>...)` 需要返回值
- `TService` 中返回 `Task`（非 `Task<T>`）的方法无法直接适配
- Dispatcher 需要管理熔断器 + 降级逻辑，职责过重
- 强行让 Dispatcher 实现 `TService` 会导致 Dispatcher 与每个具体防腐层接口耦合

## 决策
Dispatcher 仅实现 `IDisposable`，不实现 `TService` 接口。
为每个防腐层创建 `{Service}DispatcherAdapter`：

- 适配器实现 `TService` 接口
- 每个方法委托 `dispatcher.ExecuteAsync(s => s.MethodAsync(...), ct)`
- 对返回 `Task`（非 `Task<T>`）的方法，使用 `ExecuteAsync<int> + return 0` 包装

## 后果

**正面：**
- Dispatcher 职责单一（仅调度 + 熔断 + 降级）
- 适配器可独立测试（mock dispatcher 验证委托调用）
- 业务层无感知（注入 `TService` 接口，仍是防腐层语义）
- Dispatcher 不污染业务接口，可独立演进

**负面：**
- 每个防腐层多一个文件（7 个 `DispatcherAdapter`）
- `void` 方法包装为 `ExecuteAsync<int> + return 0` 略显 hacky

**风险缓解：**
- 适配器代码极简（每个方法仅一行委托），可模板化生成
- `return 0` 包装约定文档化，便于新人理解
- 适配器单元测试仅需验证委托调用，覆盖成本低
