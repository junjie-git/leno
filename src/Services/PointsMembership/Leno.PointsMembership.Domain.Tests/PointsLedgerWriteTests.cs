using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Domain.Tests;

/// <summary>
/// 验证 <see cref="PointsAccount"/> 七个状态变更方法在同事务内追加 <see cref="PointsLedger"/> 流水，
/// 保证 <c>points_ledgers</c> 表写入路径打通，<c>PointsExpiryService</c> 不再返回 0 积分。
/// 关联审计 PM-H02。
/// </summary>
public sealed class PointsLedgerWriteTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public void Earn_Should_Write_PointsLedger_With_Earn_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        account.Earn(PointsSource.CheckIn, 50, "签到返积分");

        var ledger = account.Ledgers.Should().ContainSingle().Subject;
        ledger.AccountId.Should().Be(AccountId);
        ledger.TxType.Should().Be(PointsTxType.Earn);
        ledger.Amount.Should().Be(50);
        ledger.BalanceAfter.Should().Be(50);
        ledger.Source.Should().Be(PointsSource.CheckIn);
        ledger.Reason.Should().Be("签到返积分");
    }

    [Fact]
    public void Freeze_Should_Write_PointsLedger_With_Freeze_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");

        var orderId = Guid.NewGuid();
        account.Freeze(100, orderId);

        var freezeLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Freeze);
        freezeLedger.Amount.Should().Be(100);
        freezeLedger.BalanceAfter.Should().Be(100);
        freezeLedger.ReferenceId.Should().Be(orderId);
    }

    [Fact]
    public void ConfirmDeduct_Should_Write_PointsLedger_With_Consume_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");
        var orderId = Guid.NewGuid();
        account.Freeze(100, orderId);

        account.ConfirmDeduct(orderId);

        var consumeLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Consume);
        consumeLedger.Amount.Should().Be(100);
        consumeLedger.BalanceAfter.Should().Be(0);
    }

    [Fact]
    public void Release_Should_Write_PointsLedger_With_Release_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");
        var orderId = Guid.NewGuid();
        account.Freeze(100, orderId);

        account.Release(orderId);

        var releaseLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Release);
        releaseLedger.Amount.Should().Be(100);
        releaseLedger.BalanceAfter.Should().Be(200);
    }

    [Fact]
    public void ConsumePoints_Should_Write_PointsLedger_With_Consume_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");

        var referenceId = Guid.NewGuid();
        account.ConsumePoints(80, referenceId, "兑换礼品");

        var consumeLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Consume && l.ReferenceId == referenceId);
        consumeLedger.Amount.Should().Be(80);
        consumeLedger.BalanceAfter.Should().Be(120);
        consumeLedger.Source.Should().Be(PointsSource.Offset);
    }

    [Fact]
    public void RevertPoints_Should_Write_PointsLedger_With_Revert_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");

        var referenceId = Guid.NewGuid();
        account.RevertPoints(30, referenceId, "退款扣回");

        var revertLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Revert);
        revertLedger.Amount.Should().Be(30);
        revertLedger.BalanceAfter.Should().Be(170);
        revertLedger.Source.Should().Be(PointsSource.Refund);
    }

    [Fact]
    public void ExpirePoints_Should_Write_PointsLedger_With_Expire_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 100, "种子积分");

        account.ExpirePoints(30);

        var expireLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Expire);
        expireLedger.Amount.Should().Be(30);
        expireLedger.BalanceAfter.Should().Be(70);
    }
}
