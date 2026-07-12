using Leno.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHttpClient("health-check");

// 添加 HealthChecksUI 仪表盘
builder.Services.AddLenoHealthChecksUI(builder.Configuration);

// 添加网关自身健康检查
#pragma warning disable CA1861
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(builder.Configuration["HealthChecks:SelfUrl"] ?? "http://localhost:5000"),
        "self",
        tags: new[] { "ready" });
#pragma warning restore CA1861

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/health", async (IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    var services = configuration.GetSection("HealthChecks:Services")
        .Get<Dictionary<string, string>>()
        ?? new Dictionary<string, string>();

    if (services.Count == 0)
    {
        return Results.Ok(new { status = "Healthy" });
    }

    var httpClient = httpClientFactory.CreateClient("health-check");
    httpClient.Timeout = TimeSpan.FromSeconds(5);

    var results = new Dictionary<string, string>();
    var allHealthy = true;

    foreach (var (name, url) in services)
    {
        try
        {
            var response = await httpClient.GetAsync($"{url.TrimEnd('/')}/health/ready");
            results[name] = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy";
            if (!response.IsSuccessStatusCode)
            {
                allHealthy = false;
            }
        }
        catch (HttpRequestException)
        {
            results[name] = "Unhealthy";
            allHealthy = false;
        }
        catch (TaskCanceledException)
        {
            results[name] = "Unhealthy";
            allHealthy = false;
        }
    }

    return allHealthy
        ? Results.Ok(new { status = "Healthy", services = results })
        : Results.Json(new { status = "Unhealthy", services = results }, statusCode: 503);
});

// 映射 HealthChecksUI 仪表盘
app.MapLenoHealthChecks();
app.MapLenoHealthChecksUI();

app.MapReverseProxy();

app.Run();
