namespace Leno.Product.Infrastructure.ReadModels;

/// <summary>
/// 商品 ES 读模型文档，供买家端全文搜索与多视角查询。
/// 写侧 SPU 上架时经 <see cref="ProductPublishedReadModelSyncConsumer"/> 索引；下架时删除。
/// 字段冗余以便检索，价格区间由 SKU 集合预聚合。
/// </summary>
public sealed class ProductReadModel
{
    /// <summary>商品（SPU）标识，作为 ES 文档 _id。</summary>
    public Guid Id { get; init; }

    /// <summary>商品标题，ik_max_word 分词索引。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>副标题/卖点。</summary>
    public string? Subtitle { get; init; }

    /// <summary>主图 URL。</summary>
    public string MainImageUrl { get; init; } = string.Empty;

    /// <summary>所属分类标识。</summary>
    public Guid CategoryId { get; init; }

    /// <summary>所属品牌标识，可空。</summary>
    public Guid? BrandId { get; init; }

    /// <summary>所属店铺标识。</summary>
    public Guid ShopId { get; init; }

    /// <summary>商品状态名称（OnSale 等）。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>规格维度名集合。</summary>
    public IReadOnlyList<string> Specs { get; init; } = Array.Empty<string>();

    /// <summary>最低 SKU 价格（价格区间下界）。</summary>
    public decimal MinPrice { get; init; }

    /// <summary>最高 SKU 价格（价格区间上界）。</summary>
    public decimal MaxPrice { get; init; }

    /// <summary>币种（ISO 4217），取首个 SKU 币种作为默认展示，向后兼容。</summary>
    public string Currency { get; init; } = "CNY";

    /// <summary>
    /// 所有 SKU 的币种集合（去重）。修复审计 #15：原实现仅取首个 SKU 币种或硬编码 "CNY"，
    /// 多币种店铺无法正确展示。现维护完整币种集合，支持跨境交易场景。
    /// </summary>
    public IReadOnlyList<string> Currencies { get; init; } = Array.Empty<string>();

    /// <summary>索引时间（UTC）。</summary>
    public DateTime IndexedAt { get; init; }

    /// <summary>当前加权平均评分（基于所有可见评价计算），由评价评分消费者增量维护。</summary>
    public double Score { get; set; }

    /// <summary>
    /// 累计原始总评分（未 round），由评价评分消费者增量维护。
    /// 修复审计 #9：原实现每次增量后 Math.Round(Score, 2) 存回，千次评价后浮点漂移 ±0.05。
    /// 现维护原始累计值，展示时按 TotalScore / ReviewCount 计算 Score，消除漂移。
    /// </summary>
    public double TotalScore { get; set; }

    /// <summary>可见评价总数，由评价评分消费者增量维护。</summary>
    public int ReviewCount { get; set; }

    /// <summary>评分最近一次更新时间（UTC），用于排查评分同步延迟。</summary>
    public DateTime ScoreUpdatedAt { get; set; }
}
