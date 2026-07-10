using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Aggregates;

/// <summary>
/// 物流公司聚合根，维护物流公司基础信息与启停状态。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>LogisticsCompanyId</c>。
/// </summary>
public sealed class LogisticsCompany : AggregateRoot
{
    /// <summary>物流公司名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>物流公司编码（对接第三方时的唯一标识）。</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>客服电话，可为空。</summary>
    public string? ServicePhone { get; private set; }

    /// <summary>是否支持物流轨迹查询。</summary>
    public bool SupportTracking { get; private set; }

    /// <summary>启停状态。</summary>
    public LogisticsCompanyStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private LogisticsCompany() { }

    private LogisticsCompany(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验名称与编码非空，初始状态为 Enabled。
    /// </summary>
    /// <param name="id">物流公司标识，由应用层生成。</param>
    /// <param name="name">公司名称。</param>
    /// <param name="code">公司编码。</param>
    /// <param name="servicePhone">客服电话，可为空。</param>
    /// <param name="supportTracking">是否支持轨迹查询。</param>
    public static LogisticsCompany Create(Guid id, string name, string code, string? servicePhone, bool supportTracking)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new OrderDomainException("物流公司名称不可为空", "LOGISTICS_NAME_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new OrderDomainException("物流公司编码不可为空", "LOGISTICS_CODE_EMPTY");
        }

        return new LogisticsCompany(id == Guid.Empty ? Guid.NewGuid() : id)
        {
            Name = name,
            Code = code,
            ServicePhone = servicePhone,
            SupportTracking = supportTracking,
            Status = LogisticsCompanyStatus.Enabled
        };
    }

    /// <summary>
    /// 更新物流公司可编辑字段。
    /// </summary>
    /// <param name="name">公司名称。</param>
    /// <param name="code">公司编码。</param>
    /// <param name="servicePhone">客服电话，可为空。</param>
    /// <param name="supportTracking">是否支持轨迹查询。</param>
    public void Update(string name, string code, string? servicePhone, bool supportTracking)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new OrderDomainException("物流公司名称不可为空", "LOGISTICS_NAME_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new OrderDomainException("物流公司编码不可为空", "LOGISTICS_CODE_EMPTY");
        }

        Name = name;
        Code = code;
        ServicePhone = servicePhone;
        SupportTracking = supportTracking;
    }

    /// <summary>启用物流公司。</summary>
    public void Enable()
    {
        if (Status == LogisticsCompanyStatus.Enabled)
        {
            throw new OrderDomainException("物流公司已启用", "LOGISTICS_ALREADY_ENABLED");
        }

        Status = LogisticsCompanyStatus.Enabled;
    }

    /// <summary>停用物流公司，停用后不可被选择发货。</summary>
    public void Disable()
    {
        if (Status == LogisticsCompanyStatus.Disabled)
        {
            throw new OrderDomainException("物流公司已停用", "LOGISTICS_ALREADY_DISABLED");
        }

        Status = LogisticsCompanyStatus.Disabled;
    }
}
