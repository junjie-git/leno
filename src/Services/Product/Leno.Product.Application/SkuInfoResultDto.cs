namespace Leno.Product.Application;

/// <summary>SKU 概要信息，供跨域查询使用。</summary>
public sealed class SkuInfoResultDto
{
    public Guid SkuId { get; set; }

    public Guid SpuId { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "CNY";

    public bool Available { get; set; }

    public int Stock { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string MainImageUrl { get; set; } = string.Empty;

    public Guid SellerId { get; set; }

    public Guid? ShopId { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
