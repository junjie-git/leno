using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Exceptions;
using Leno.Payment.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Payment.Application.Services;

/// <summary>
/// 支付渠道配置应用服务实现，编排渠道配置的查询、更新、启用/禁用用例。
/// 配置值在响应中脱敏显示，变更时发布 <see cref="PaymentChannelConfigChangedEvent"/>。
/// </summary>
public sealed class PaymentChannelConfigAppService : IPaymentChannelConfigAppService
{
    private readonly IPaymentChannelConfigRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentChannelConfigAppService> _logger;

    public PaymentChannelConfigAppService(
        IPaymentChannelConfigRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<PaymentChannelConfigAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<PaymentChannelConfigDto>> GetAllAsync(CancellationToken ct = default)
    {
        var configs = await _repository.GetAllAsync(ct);
        return configs.ConvertAll(ToDto);
    }

    /// <inheritdoc />
    public async Task<PaymentChannelConfigDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var config = await _repository.GetByIdAsync(id, ct);
        return config is null ? null : ToDto(config);
    }

    /// <inheritdoc />
    public async Task<PaymentChannelConfigDto> UpdateAsync(Guid id, UpdatePaymentChannelConfigDto dto, CancellationToken ct = default)
    {
        var config = await _repository.GetByIdAsync(id, ct)
            ?? throw new PaymentDomainException($"配置项不存在 ConfigId={id}", "CHANNEL_CONFIG_NOT_FOUND", 404);

        config.UpdateConfigValue(dto.ConfigValue);
        if (dto.Description is not null)
        {
            config.Description = dto.Description;
        }

        await _repository.UpdateAsync(config, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("支付渠道配置已更新 ConfigId={ConfigId} Channel={Channel} ConfigName={ConfigName}",
            config.Id, config.Channel, config.ConfigName);

        return ToDto(config);
    }

    /// <inheritdoc />
    public async Task EnableAsync(Guid id, CancellationToken ct = default)
    {
        var config = await _repository.GetByIdAsync(id, ct)
            ?? throw new PaymentDomainException($"配置项不存在 ConfigId={id}", "CHANNEL_CONFIG_NOT_FOUND", 404);

        config.Enable();

        await _repository.UpdateAsync(config, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("支付渠道配置已启用 ConfigId={ConfigId} Channel={Channel} ConfigName={ConfigName}",
            config.Id, config.Channel, config.ConfigName);
    }

    /// <inheritdoc />
    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var config = await _repository.GetByIdAsync(id, ct)
            ?? throw new PaymentDomainException($"配置项不存在 ConfigId={id}", "CHANNEL_CONFIG_NOT_FOUND", 404);

        config.Disable();

        await _repository.UpdateAsync(config, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
        _logger.LogInformation("支付渠道配置已禁用 ConfigId={ConfigId} Channel={Channel} ConfigName={ConfigName}",
            config.Id, config.Channel, config.ConfigName);
    }

    private static PaymentChannelConfigDto ToDto(PaymentChannelConfig config)
    {
        var maskedValue = MaskConfigValue(config.ConfigValue);

        return new PaymentChannelConfigDto
        {
            Id = config.Id,
            Channel = config.Channel.ToString(),
            ConfigName = config.ConfigName,
            ConfigValue = maskedValue,
            Description = config.Description,
            Enabled = config.Enabled,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    /// <summary>
    /// 脱敏显示配置值：仅显示前 4 个字符 + "****"。
    /// </summary>
    private static string MaskConfigValue(string configValue)
    {
        if (string.IsNullOrEmpty(configValue) || configValue.Length <= 4)
        {
            return "****";
        }

        return configValue[..4] + "****";
    }
}