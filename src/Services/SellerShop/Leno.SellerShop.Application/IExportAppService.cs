using Leno.SellerShop.Application.Dtos;
using Leno.SharedContracts.Responses;

namespace Leno.SellerShop.Application;

/// <summary>
/// 数据导出应用服务，创建导出任务、查询任务列表、获取下载文件路径。
/// 实际文件生成由 ExportBackgroundService 异步完成。
/// </summary>
public interface IExportAppService
{
    /// <summary>创建导出任务（状态 Processing，等待后台作业处理）。</summary>
    Task<ExportTaskDto> CreateTaskAsync(Guid sellerId, CreateExportTaskDto dto, CancellationToken ct = default);

    /// <summary>分页查询导出任务列表。</summary>
    Task<PageResult<ExportTaskDto>> ListTasksAsync(Guid sellerId, ExportTaskQueryParams queryParams, CancellationToken ct = default);

    /// <summary>获取导出任务文件路径（供 Controller 读取 stream 返回）。</summary>
    Task<(string FilePath, string ContentType, string FileName)?> GetDownloadAsync(Guid sellerId, Guid taskId, CancellationToken ct = default);
}
