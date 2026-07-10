using Leno.Order.Application.DTOs;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Application.Services;

/// <summary>
/// 物流公司应用服务实现，编排运营端物流公司 CRUD 与启停用例。
/// </summary>
public sealed class LogisticsCompanyAppService : ILogisticsCompanyAppService
{
    private readonly ILogisticsCompanyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public LogisticsCompanyAppService(ILogisticsCompanyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<LogisticsCompanyDto> CreateAsync(CreateLogisticsCompanyDto dto, CancellationToken ct = default)
    {
        var entity = LogisticsCompany.Create(
            Guid.NewGuid(), dto.Name, dto.Code, dto.ServicePhone, dto.SupportTracking);
        await _repository.AddAsync(entity, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<LogisticsCompanyDto> UpdateAsync(Guid id, UpdateLogisticsCompanyDto dto, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct);
        entity.Update(dto.Name, dto.Code, dto.ServicePhone, dto.SupportTracking);
        await _unitOfWork.SaveEntitiesAsync(ct);
        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct);
        entity.Enable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await RequireAsync(id, ct);
        entity.Disable();
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<LogisticsCompanyDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var list = await _repository.ListAsync(page, pageSize, ct);
        return list.Select(ToDto).ToList();
    }

    private async Task<LogisticsCompany> RequireAsync(Guid id, CancellationToken ct)
        => await _repository.GetByIdAsync(id, ct)
           ?? throw new OrderDomainException($"物流公司 {id} 不存在", "LOGISTICS_NOT_FOUND", 404);

    private static LogisticsCompanyDto ToDto(LogisticsCompany entity)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            ServicePhone = entity.ServicePhone,
            SupportTracking = entity.SupportTracking,
            Status = entity.Status
        };
}
