using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Leno.Payment.Domain.Aggregates;
using Leno.Payment.Domain.Repositories;
using Leno.Payment.Domain.ValueObjects;
using Leno.Payment.Infrastructure.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Payment.Infrastructure.Tests;

/// <summary>
/// 对账服务分页查询与差异比对单元测试（P1-B.1 Task 6）。
/// 验证 ReconcileAsync 在大数据量场景下采用 PageSize=500 分页循环，
/// 避免一次性 QueryAsync(..., 1, 10000) 漏对账；同时验证 CompareReconciliation
/// 在重构为字典入参后仍能正确识别 ChannelOnly / SystemOnly / AmountMismatch。
/// </summary>
public class ReconciliationServiceTests
{
    /// <summary>
    /// ReconcileAsync 分页循环测试：mock 仓储与服务范围工厂，
    /// 通过 IServiceScopeFactory 返回测试替身，断言 QueryAsync 调用次数符合分页预期。
    /// </summary>
    public class ReconcileAsyncPaginationTests
    {
        private readonly Mock<IPaymentOrderRepository> _paymentRepoMock = new();
        private readonly Mock<IReconciliationDiffRepository> _diffRepoMock = new();
        private readonly Mock<IUnitOfWork> _uowMock = new();
        private readonly ReconciliationService _sut;

        public ReconcileAsyncPaginationTests()
        {
            // 模拟 IServiceScopeFactory → IServiceScope → IServiceProvider 链路
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IPaymentOrderRepository)))
                .Returns(_paymentRepoMock.Object);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IReconciliationDiffRepository)))
                .Returns(_diffRepoMock.Object);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IUnitOfWork)))
                .Returns(_uowMock.Object);

            var scopeMock = new Mock<IServiceScope>();
            scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var loggerMock = new Mock<ILogger<ReconciliationService>>();
            _sut = new ReconciliationService(scopeFactoryMock.Object, loggerMock.Object);

            // 差异仓储与工作单元默认成功
            _diffRepoMock
                .Setup(d => d.AddAsync(It.IsAny<ReconciliationDiff>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _uowMock
                .Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        /// <summary>
        /// 构造指定数量的测试支付单，OutTradeNo 与 ChannelTradeNo 唯一，
        /// 金额统一 100 元，避免对账差异处理干扰分页调用次数断言。
        /// </summary>
        private static List<PaymentOrder> CreateOrders(int count, PaymentChannel channel)
        {
            var orders = new List<PaymentOrder>(count);
            for (var i = 0; i < count; i++)
            {
                var order = PaymentOrder.Create(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                    100m, "CNY", channel);
                // OutTradeNo 由工厂生成，但这里需要可控以便与渠道账单对账
                typeof(PaymentOrder).GetProperty("OutTradeNo")!.SetValue(order, $"PAY{i:D6}");
                typeof(PaymentOrder).GetProperty("ChannelTradeNo")!.SetValue(order, $"{channel}_TXN_{i:D6}");
                orders.Add(order);
            }
            return orders;
        }

        /// <summary>
        /// 为指定渠道配置 SetupSequence 分页返回。
        /// 序列：第 1 页 500 条 → 第 2 页 500 条 → 第 3 页 500 条 → 第 4 页空。
        /// 期望：每渠道调用 4 次（最后一页正好 PageSize 必须查询下一页确认空）。
        /// </summary>
        private void SetupMultiPageSequence(PaymentChannel channel)
        {
            _paymentRepoMock
                .SetupSequence(r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    channel,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateOrders(500, channel))
                .ReturnsAsync(CreateOrders(500, channel))
                .ReturnsAsync(CreateOrders(500, channel))
                .ReturnsAsync(new List<PaymentOrder>());
        }

        /// <summary>
        /// 1500 条数据（500+500+500+空）应触发每渠道 4 次查询。
        /// 第 3 页正好 500 条（等于 PageSize），必须查询第 4 页确认空才退出。
        /// 旧实现一次性 pageSize=10000 只查询 1 次，本测试应失败。
        /// </summary>
        [Fact]
        public async Task ReconcileAsync_MoreThanPageSize_ShouldQueryMultiplePages()
        {
            // 安排
            SetupMultiPageSequence(PaymentChannel.WeChatPay);
            SetupMultiPageSequence(PaymentChannel.Alipay);

            // 行动
            await _sut.ReconcileAsync(new DateTime(2026, 7, 19), CancellationToken.None);

            // 断言：每渠道调用 4 次
            _paymentRepoMock.Verify(
                r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.WeChatPay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(4));
            _paymentRepoMock.Verify(
                r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.Alipay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(4));
        }

        /// <summary>
        /// 100 条数据（小于 PageSize=500）应只查询 1 次/渠道。
        /// 当前实现也是 1 次/渠道，本测试同时通过新旧实现。
        /// </summary>
        [Fact]
        public async Task ReconcileAsync_LessThanPageSize_ShouldQueryOnce()
        {
            // 安排：每渠道只返回 100 条
            _paymentRepoMock
                .SetupSequence(r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.WeChatPay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateOrders(100, PaymentChannel.WeChatPay));
            _paymentRepoMock
                .SetupSequence(r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.Alipay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateOrders(100, PaymentChannel.Alipay));

            // 行动
            await _sut.ReconcileAsync(new DateTime(2026, 7, 19), CancellationToken.None);

            // 断言：每渠道调用 1 次
            _paymentRepoMock.Verify(
                r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.WeChatPay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _paymentRepoMock.Verify(
                r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.Alipay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// 恰好一页（500 条 + 空页）应查询 2 次/渠道。
        /// 边界条件：当 batch.Count == PageSize 时必须查询下一页确认无更多。
        /// 当前实现只查询 1 次，本测试应失败。
        /// </summary>
        [Fact]
        public async Task ReconcileAsync_ExactlyPageSize_ShouldQueryNextPage()
        {
            // 安排：每渠道 500+空
            _paymentRepoMock
                .SetupSequence(r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.WeChatPay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateOrders(500, PaymentChannel.WeChatPay))
                .ReturnsAsync(new List<PaymentOrder>());
            _paymentRepoMock
                .SetupSequence(r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.Alipay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateOrders(500, PaymentChannel.Alipay))
                .ReturnsAsync(new List<PaymentOrder>());

            // 行动
            await _sut.ReconcileAsync(new DateTime(2026, 7, 19), CancellationToken.None);

            // 断言：每渠道调用 2 次
            _paymentRepoMock.Verify(
                r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.WeChatPay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _paymentRepoMock.Verify(
                r => r.QueryAsync(
                    It.IsAny<Guid?>(),
                    PaymentChannel.Alipay,
                    It.IsAny<PaymentStatus?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }
    }

    /// <summary>
    /// CompareReconciliation 字典入参直测：验证重构后的字典签名仍正确识别三类差异。
    /// </summary>
    public class CompareReconciliationDictionaryTests
    {
        private static PaymentOrder CreateOrder(string outTradeNo, decimal amount, string? channelTradeNo = null)
        {
            var order = PaymentOrder.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                amount, "CNY", PaymentChannel.WeChatPay);
            typeof(PaymentOrder).GetProperty("OutTradeNo")!.SetValue(order, outTradeNo);
            if (channelTradeNo is not null)
            {
                typeof(PaymentOrder).GetProperty("ChannelTradeNo")!.SetValue(order, channelTradeNo);
            }
            return order;
        }

        /// <summary>
        /// 构建系统订单字典：byOutTradeNo 以 OutTradeNo 为键，byChannelTradeNo 以 ChannelTradeNo 为键。
        /// 与 ReconcileAsync 内部字典构建逻辑保持一致。
        /// </summary>
        private static (IReadOnlyDictionary<string, PaymentOrder> byOutTradeNo,
                        IReadOnlyDictionary<string, PaymentOrder> byChannelTradeNo) BuildDictionaries(
            IReadOnlyList<PaymentOrder> orders)
        {
            var byOut = new Dictionary<string, PaymentOrder>();
            var byChannel = new Dictionary<string, PaymentOrder>();
            foreach (var o in orders)
            {
                if (!string.IsNullOrEmpty(o.OutTradeNo))
                {
                    byOut[o.OutTradeNo] = o;
                }
                if (!string.IsNullOrEmpty(o.ChannelTradeNo))
                {
                    byChannel[o.ChannelTradeNo!] = o;
                }
            }
            return (byOut, byChannel);
        }

        [Fact]
        public void CompareReconciliation_AmountMismatch_ShouldReportDiff()
        {
            // 安排：渠道 100 元，系统 90 元
            var billDate = new DateTime(2026, 7, 19);
            var channel = PaymentChannel.WeChatPay;

            var channelRecords = new List<BillRecord>
            {
                new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m }
            };
            var systemOrders = new List<PaymentOrder>
            {
                CreateOrder("PAY001", 90m, "WX_TXN_001")
            };
            var (byOut, byChannel) = BuildDictionaries(systemOrders);

            // 行动
            var diffs = ReconciliationService.CompareReconciliation(
                billDate, channel, channelRecords, byOut, byChannel);

            // 断言：报告 AmountMismatch
            var amountDiffs = diffs.Where(d => d.DiffType == ReconciliationDiffType.AmountMismatch).ToList();
            amountDiffs.Should().HaveCount(1);
            amountDiffs[0].ChannelAmount.Should().Be(100m);
            amountDiffs[0].SystemAmount.Should().Be(90m);
            amountDiffs[0].ChannelTransactionNo.Should().Be("WX_TXN_001");
            amountDiffs[0].SystemTransactionNo.Should().Be("PAY001");
        }

        [Fact]
        public void CompareReconciliation_ChannelOnly_ShouldReportDiff()
        {
            // 安排：渠道有 WX_TXN_002，系统无
            var billDate = new DateTime(2026, 7, 19);
            var channel = PaymentChannel.WeChatPay;

            var channelRecords = new List<BillRecord>
            {
                new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m },
                new() { ChannelTransactionNo = "WX_TXN_002", OutTradeNo = "PAY002", Amount = 200m }
            };
            var systemOrders = new List<PaymentOrder>
            {
                CreateOrder("PAY001", 100m, "WX_TXN_001")
            };
            var (byOut, byChannel) = BuildDictionaries(systemOrders);

            // 行动
            var diffs = ReconciliationService.CompareReconciliation(
                billDate, channel, channelRecords, byOut, byChannel);

            // 断言：报告 ChannelOnly，渠道交易号为 WX_TXN_002
            var channelOnlyDiffs = diffs.Where(d => d.DiffType == ReconciliationDiffType.ChannelOnly).ToList();
            channelOnlyDiffs.Should().HaveCount(1);
            channelOnlyDiffs[0].ChannelTransactionNo.Should().Be("WX_TXN_002");
            channelOnlyDiffs[0].ChannelAmount.Should().Be(200m);
        }

        [Fact]
        public void CompareReconciliation_SystemOnly_ShouldReportDiff()
        {
            // 安排：系统有 PAY002，渠道无
            var billDate = new DateTime(2026, 7, 19);
            var channel = PaymentChannel.WeChatPay;

            var channelRecords = new List<BillRecord>
            {
                new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m }
            };
            var systemOrders = new List<PaymentOrder>
            {
                CreateOrder("PAY001", 100m, "WX_TXN_001"),
                CreateOrder("PAY002", 200m, "WX_TXN_002")
            };
            var (byOut, byChannel) = BuildDictionaries(systemOrders);

            // 行动
            var diffs = ReconciliationService.CompareReconciliation(
                billDate, channel, channelRecords, byOut, byChannel);

            // 断言：报告 SystemOnly，系统商户单号为 PAY002
            var systemOnlyDiffs = diffs.Where(d => d.DiffType == ReconciliationDiffType.SystemOnly).ToList();
            systemOnlyDiffs.Should().HaveCount(1);
            systemOnlyDiffs[0].SystemTransactionNo.Should().Be("PAY002");
            systemOnlyDiffs[0].SystemAmount.Should().Be(200m);
        }

        [Fact]
        public void CompareReconciliation_AllMatch_ShouldReportNoDiff()
        {
            // 安排：渠道与系统完全匹配
            var billDate = new DateTime(2026, 7, 19);
            var channel = PaymentChannel.WeChatPay;

            var channelRecords = new List<BillRecord>
            {
                new() { ChannelTransactionNo = "WX_TXN_001", OutTradeNo = "PAY001", Amount = 100m },
                new() { ChannelTransactionNo = "WX_TXN_002", OutTradeNo = "PAY002", Amount = 200m }
            };
            var systemOrders = new List<PaymentOrder>
            {
                CreateOrder("PAY001", 100m, "WX_TXN_001"),
                CreateOrder("PAY002", 200m, "WX_TXN_002")
            };
            var (byOut, byChannel) = BuildDictionaries(systemOrders);

            // 行动
            var diffs = ReconciliationService.CompareReconciliation(
                billDate, channel, channelRecords, byOut, byChannel);

            // 断言：无差异
            diffs.Should().BeEmpty();
        }
    }
}
