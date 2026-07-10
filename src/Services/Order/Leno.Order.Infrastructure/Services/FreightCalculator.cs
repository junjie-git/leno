using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 运费计算器实现，加载卖家运费模板并委托其 CalculateFreight 计价。
/// 卖家未配置模板时返回 0（免运费）。
/// </summary>
public sealed class FreightCalculator : IFreightCalculator
{
    private readonly IFreightTemplateRepository _freightTemplateRepository;

    public FreightCalculator(IFreightTemplateRepository freightTemplateRepository)
    {
        ArgumentNullException.ThrowIfNull(freightTemplateRepository);
        _freightTemplateRepository = freightTemplateRepository;
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateAsync(Guid sellerId, string regionCode, int quantity, decimal orderAmount, CancellationToken ct = default)
    {
        var template = await _freightTemplateRepository.GetBySellerIdAsync(sellerId, ct);
        if (template is null)
        {
            return 0;
        }

        return template.CalculateFreight(regionCode, quantity, orderAmount);
    }
}
