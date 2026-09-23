param(
    [string] $GrpcEndpoint = "http://localhost:5265",
    [string] $SqlConnectionString = "Server=localhost,1433;Database=leno_inventory;User Id=sa;Password=Leno_Local_2026!x;TrustServerCertificate=true",
    [string] $InternalApiKey = "local-dev-internal-api-key-32bytes!!"
)

# 库存 staging E2E（阶段 5）：gRPC 预占/确认/释放/查询 + SQL 台账断言。
# 本地 compose 模式：-GrpcEndpoint http://localhost:5265（默认）；
# 真 staging：-GrpcEndpoint http://<staging-inventory>:5265（其余参数同步替换）。
dotnet run --project "$PSScriptRoot/InventoryE2E" -- --grpc $GrpcEndpoint --sql $SqlConnectionString --api-key $InternalApiKey
exit $LASTEXITCODE
