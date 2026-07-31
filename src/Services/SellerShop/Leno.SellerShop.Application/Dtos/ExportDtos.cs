namespace Leno.SellerShop.Application.Dtos;

/// <summary>
/// 创建导出任务请求 DTO。
/// </summary>
public sealed class CreateExportTaskDto
{
    public string ReportType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Format { get; set; } = string.Empty;
}

/// <summary>
/// 导出任务 DTO（API 响应）。
/// </summary>
public sealed class ExportTaskDto
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? RecordCount { get; set; }
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 导出任务列表查询参数。
/// </summary>
public sealed class ExportTaskQueryParams
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
