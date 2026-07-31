using Leno.SellerShop.Application.Dtos;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 导出应用服务实现。创建任务时校验 90 天范围约束，查询任务列表映射 DTO。
/// </summary>
public sealed class ExportAppService : IExportAppService
{
    private const int MaxRangeDays = 90;
    private readonly IExportTaskRepository _taskRepository;
    private readonly IShopAppService _shopAppService;
    private readonly IUnitOfWork _unitOfWork;

    public ExportAppService(
        IExportTaskRepository taskRepository,
        IShopAppService shopAppService,
        IUnitOfWork unitOfWork)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _shopAppService = shopAppService ?? throw new ArgumentNullException(nameof(shopAppService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<ExportTaskDto> CreateTaskAsync(Guid sellerId, CreateExportTaskDto dto, CancellationToken ct = default)
    {
        ValidateDto(dto);

        var shop = await _shopAppService.GetMyShopAsync(sellerId, ct);
        var task = ExportTask.Create(
            Guid.NewGuid(),
            shop.Id,
            sellerId,
            dto.ReportType,
            dto.StartDate,
            dto.EndDate,
            dto.Format);

        await _taskRepository.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(task);
    }

    /// <inheritdoc />
    public async Task<PageResult<ExportTaskDto>> ListTasksAsync(Guid sellerId, ExportTaskQueryParams queryParams, CancellationToken ct = default)
    {
        var shop = await _shopAppService.GetMyShopAsync(sellerId, ct);
        var (items, total) = await _taskRepository.ListByShopAsync(
            shop.Id, queryParams.Status, queryParams.Page, queryParams.PageSize, ct);

        var dtos = items.Select(ToDto).ToList();
        return new PageResult<ExportTaskDto>(dtos, total, queryParams.Page, queryParams.PageSize);
    }

    /// <inheritdoc />
    public async Task<(string FilePath, string ContentType, string FileName)?> GetDownloadAsync(Guid sellerId, Guid taskId, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct);
        if (task is null || task.SellerId != sellerId)
        {
            return null;
        }
        if (task.Status != "Completed" || string.IsNullOrEmpty(task.FilePath))
        {
            return null;
        }

        var ext = task.Format == "Excel" ? "xlsx" : "csv";
        var contentType = task.Format == "Excel"
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var fileName = $"{task.ReportType}-{task.Id}.{ext}";

        return (task.FilePath, contentType, fileName);
    }

    private static void ValidateDto(CreateExportTaskDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ReportType))
            throw new ArgumentException("报表类型不可为空");
        if (string.IsNullOrWhiteSpace(dto.Format))
            throw new ArgumentException("导出格式不可为空");
        if (dto.EndDate < dto.StartDate)
            throw new ArgumentException("结束时间不能早于开始时间");
        if ((dto.EndDate - dto.StartDate).TotalDays > MaxRangeDays)
            throw new ArgumentException($"时间范围不能超过 {MaxRangeDays} 天");
    }

    private static ExportTaskDto ToDto(ExportTask task) => new()
    {
        Id = task.Id,
        ReportType = task.ReportType,
        StartDate = task.StartDate,
        EndDate = task.EndDate,
        Format = task.Format,
        Status = task.Status,
        RecordCount = task.RecordCount,
        FileSize = task.FileSize,
        CreatedAt = task.CreatedAt,
        CompletedAt = task.CompletedAt,
        ErrorMessage = task.ErrorMessage
    };
}
