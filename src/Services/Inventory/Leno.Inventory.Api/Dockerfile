# A3 部署物料（双轨下线 DEC-4 决策：5 个 BC 全部部署，2026-09-21）
# 与 Identity/AccessControl/UserCenter 的 Dockerfile 保持同一构建方式：
# 全上下文 COPY + restore + publish（仓库根为构建上下文，CI docker-build 已如此配置）
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Services/Inventory/Leno.Inventory.Api/Leno.Inventory.Api.csproj"
RUN dotnet publish "src/Services/Inventory/Leno.Inventory.Api/Leno.Inventory.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
# gRPC 内部服务端口（InventoryInternalService，Order BC 同步预占/查询经此调用）
EXPOSE 5265
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080;http://+:5265
ENV ASPNETCORE_ENVIRONMENT=Docker
ENTRYPOINT ["dotnet", "Leno.Inventory.Api.dll"]
