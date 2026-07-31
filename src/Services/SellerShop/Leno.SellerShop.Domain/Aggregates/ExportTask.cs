using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Aggregates;

/// <summary>
/// 导出任务聚合根，记录卖家数据导出任务的生命周期。
/// 状态机：Processing → Completed | Failed。
/// 由 ExportAppService 创建（Processing），由 ExportBackgroundService 处理后标记终态。
/// </summary>
public sealed class ExportTask : AggregateRoot
{
    /// <summary>所属店铺标识。</summary>
    public Guid ShopId { get; private set; }

    /// <summary>卖家标识。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>报表类型（SalesSummary/OrderDetail/ProductSales）。</summary>
    public string ReportType { get; private set; } = string.Empty;

    /// <summary>导出起始日期（UTC）。</summary>
    public DateTime StartDate { get; private set; }

    /// <summary>导出结束日期（UTC）。</summary>
    public DateTime EndDate { get; private set; }

    /// <summary>导出格式（Excel/CSV）。</summary>
    public string Format { get; private set; } = string.Empty;

    /// <summary>任务状态（Processing/Completed/Failed）。</summary>
    public string Status { get; private set; } = "Processing";

    /// <summary>记录数（完成后填充）。</summary>
    public int? RecordCount { get; private set; }

    /// <summary>文件大小（字节，完成后填充）。</summary>
    public long? FileSize { get; private set; }

    /// <summary>文件路径（完成后填充，相对 IFileStorageService 路径）。</summary>
    public string? FilePath { get; private set; }

    /// <summary>错误信息（失败时填充）。</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>完成时间（UTC）。</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ExportTask() { }

    private ExportTask(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建导出任务（初始状态 Processing）。
    /// </summary>
    public static ExportTask Create(
        Guid taskId,
        Guid shopId,
        Guid sellerId,
        string reportType,
        DateTime startDate,
        DateTime endDate,
        string format)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("任务标识不可为空", nameof(taskId));
        if (shopId == Guid.Empty)
            throw new ArgumentException("店铺标识不可为空", nameof(shopId));
        if (sellerId == Guid.Empty)
            throw new ArgumentException("卖家标识不可为空", nameof(sellerId));
        if (string.IsNullOrWhiteSpace(reportType))
            throw new ArgumentException("报表类型不可为空", nameof(reportType));
        if (string.IsNullOrWhiteSpace(format))
            throw new ArgumentException("导出格式不可为空", nameof(format));
        if (endDate < startDate)
            throw new ArgumentException("结束时间不能早于开始时间");

        return new ExportTask(taskId)
        {
            ShopId = shopId,
            SellerId = sellerId,
            ReportType = reportType,
            StartDate = startDate,
            EndDate = endDate,
            Format = format,
            Status = "Processing"
        };
    }

    /// <summary>标记任务完成。</summary>
    public void MarkCompleted(int recordCount, long fileSize, string filePath)
    {
        if (Status != "Processing")
            throw new InvalidOperationException($"任务已处于终态 {Status}，不可标记完成");

        Status = "Completed";
        RecordCount = recordCount;
        FileSize = fileSize;
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>标记任务失败。</summary>
    public void MarkFailed(string errorMessage)
    {
        if (Status != "Processing")
            throw new InvalidOperationException($"任务已处于终态 {Status}，不可标记失败");

        Status = "Failed";
        ErrorMessage = errorMessage ?? "未知错误";
        CompletedAt = DateTime.UtcNow;
    }
}
