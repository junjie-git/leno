using Leno.Order.Domain.ValueObjects;

namespace Leno.Order.Application.DTOs;

/// <summary>
/// 物流公司 DTO。
/// </summary>
public sealed class LogisticsCompanyDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string? ServicePhone { get; init; }

    public bool SupportTracking { get; init; }

    public LogisticsCompanyStatus Status { get; init; }
}

/// <summary>
/// 创建物流公司 DTO。
/// </summary>
public sealed class CreateLogisticsCompanyDto
{
    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string? ServicePhone { get; init; }

    public bool SupportTracking { get; init; }
}

/// <summary>
/// 更新物流公司 DTO。
/// </summary>
public sealed class UpdateLogisticsCompanyDto
{
    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string? ServicePhone { get; init; }

    public bool SupportTracking { get; init; }
}

/// <summary>
/// 运费模板 DTO。
/// </summary>
public sealed class FreightTemplateDto
{
    public Guid Id { get; init; }

    public Guid SellerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public FreightTemplateType Type { get; init; }

    public decimal? FreeShippingThreshold { get; init; }

    public FreightTemplateStatus Status { get; init; }

    public List<FreightRegionRuleDto> RegionRules { get; init; } = new();
}

/// <summary>
/// 区域运费规则 DTO。
/// </summary>
public sealed class FreightRegionRuleDto
{
    public string RegionCode { get; init; } = string.Empty;

    public int FirstUnit { get; init; }

    public decimal FirstPrice { get; init; }

    public int AdditionalUnit { get; init; }

    public decimal AdditionalPrice { get; init; }
}

/// <summary>
/// 创建运费模板 DTO。
/// </summary>
public sealed class CreateFreightTemplateDto
{
    public Guid SellerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public FreightTemplateType Type { get; init; }

    public decimal? FreeShippingThreshold { get; init; }

    public List<FreightRegionRuleDto> RegionRules { get; init; } = new();
}

/// <summary>
/// 更新运费模板区域规则 DTO。
/// </summary>
public sealed class UpdateFreightTemplateRulesDto
{
    public List<FreightRegionRuleDto> RegionRules { get; init; } = new();
}

/// <summary>
/// 物流轨迹 DTO。
/// </summary>
public sealed class LogisticsTrackingDto
{
    public string LogisticsNo { get; init; } = string.Empty;

    public string CompanyCode { get; init; } = string.Empty;

    public List<LogisticsTrackingNode> Nodes { get; init; } = new();

    /// <summary>是否来自缓存。</summary>
    public bool IsFromCache { get; init; }

    /// <summary>是否带有警告标识（查询失败时返回缓存数据）。</summary>
    public bool HasWarning { get; init; }
}

/// <summary>
/// 物流轨迹节点 DTO。
/// </summary>
public sealed class LogisticsTrackingNode
{
    public string Description { get; init; } = string.Empty;

    public DateTime OccurredAt { get; init; }

    public string Location { get; init; } = string.Empty;
}
