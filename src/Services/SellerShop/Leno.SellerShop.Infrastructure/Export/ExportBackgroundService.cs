using Leno.Infrastructure.Abstractions;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.Export;

/// <summary>
/// 导出后台作业，轮询 Processing 状态任务并生成文件。
/// 每 5 秒轮询一次，每次处理 1 个任务。
/// </summary>
public sealed class ExportBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExportBackgroundService> _logger;

    public ExportBackgroundService(IServiceProvider serviceProvider, ILogger<ExportBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExportBackgroundService 启动");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var taskRepository = scope.ServiceProvider.GetRequiredService<IExportTaskRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var fileGenerator = scope.ServiceProvider.GetRequiredService<ExportFileGenerator>();
                var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                var productAcl = scope.ServiceProvider.GetRequiredService<IProductAntiCorruptionService>();
                var orderAcl = scope.ServiceProvider.GetRequiredService<IOrderAntiCorruptionService>();

                var task = await taskRepository.GetOldestProcessingAsync(stoppingToken);
                if (task is not null)
                {
                    await ProcessTaskAsync(task, taskRepository, unitOfWork, fileGenerator, fileStorage, productAcl, orderAcl, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "ExportBackgroundService 轮询异常");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        _logger.LogInformation("ExportBackgroundService 停止");
    }

    private async Task ProcessTaskAsync(
        ExportTask task,
        IExportTaskRepository taskRepository,
        IUnitOfWork unitOfWork,
        ExportFileGenerator fileGenerator,
        IFileStorageService fileStorage,
        IProductAntiCorruptionService productAcl,
        IOrderAntiCorruptionService orderAcl,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("开始处理导出任务 TaskId={TaskId} Type={ReportType}", task.Id, task.ReportType);

            var (headers, rows) = task.ReportType switch
            {
                "SalesSummary" => await orderAcl.GetSalesSummaryAsync(task.ShopId, task.StartDate, task.EndDate, ct),
                "OrderDetail" => await orderAcl.GetOrderDetailForExportAsync(task.ShopId, task.StartDate, task.EndDate, ct),
                "ProductSales" => await productAcl.GetProductSalesAsync(task.ShopId, task.StartDate, task.EndDate, ct),
                _ => (new List<string>(), new List<IReadOnlyDictionary<string, object?>>())
            };

            var bytes = task.Format == "Excel"
                ? fileGenerator.GenerateExcel(task.ReportType, headers, rows)
                : fileGenerator.GenerateCsv(headers, rows);

            var ext = task.Format == "Excel" ? "xlsx" : "csv";
            var fileName = $"{task.Id}.{ext}";
            var contentType = task.Format == "Excel"
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "text/csv";

            using var stream = new MemoryStream(bytes);
            var uploadResult = await fileStorage.UploadAsync(stream, fileName, contentType, "export", ct);

            task.MarkCompleted(rows.Count, uploadResult.Size, uploadResult.Url);
            await unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation("导出任务完成 TaskId={TaskId} Records={Count}", task.Id, rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出任务失败 TaskId={TaskId}", task.Id);
            task.MarkFailed(ex.Message);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
