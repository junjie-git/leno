FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Services/Promotion/Leno.Promotion.Api/Leno.Promotion.Api.csproj"
RUN dotnet publish "src/Services/Promotion/Leno.Promotion.Api/Leno.Promotion.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker
# P0 安全加固：以镜像自带的非 root 用户运行（UID 1654），预建日志与文件存储可写目录
RUN mkdir -p /app/logs /app/uploads && chown -R $APP_UID:$APP_UID /app/logs /app/uploads
USER $APP_UID
ENTRYPOINT ["dotnet", "Leno.Promotion.Api.dll"]
