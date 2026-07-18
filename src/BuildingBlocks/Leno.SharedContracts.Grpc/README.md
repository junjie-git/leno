# Leno.SharedContracts.Grpc

此项目承载由 buf generate 生成的 gRPC C# 代码。

## 生成方式

```bash
cd src/BuildingBlocks/Leno.SharedContracts
buf generate
```

生成结果输出到 `../Leno.SharedContracts.Grpc/Generated/` 目录。

## 注意

- 不要手动编辑 `Generated/` 目录下的文件
- .proto 文件变更后需重新运行 `buf generate`
- CI 已集成 `buf lint` + `buf breaking` 校验
