FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Leno.ReviewAfterSales.Api.csproj"
RUN dotnet publish "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Leno.ReviewAfterSales.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker
ENTRYPOINT ["dotnet", "Leno.ReviewAfterSales.Api.dll"]
