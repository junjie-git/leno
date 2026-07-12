using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Domain.Tests;

public class AuditInfoTests
{
    [Fact]
    public void Approved_ValidParameters_ShouldCreateWithApprovedResult()
    {
        var info = AuditInfo.Approved("op-001", "Operator Zhang");

        info.OperatorId.Should().Be("op-001");
        info.OperatorName.Should().Be("Operator Zhang");
        info.Result.Should().Be("Approved");
        info.Reason.Should().BeNull();
        info.AuditedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Approved_EmptyOperatorId_ShouldThrowException()
    {
        var act = () => AuditInfo.Approved("", "Operator Zhang");

        act.Should().Throw<ArgumentException>().WithMessage("*操作人标识*");
    }

    [Fact]
    public void Approved_EmptyOperatorName_ShouldThrowException()
    {
        var act = () => AuditInfo.Approved("op-001", "");

        act.Should().Throw<ArgumentException>().WithMessage("*操作人名称*");
    }

    [Fact]
    public void Rejected_ValidParameters_ShouldCreateWithRejectedResult()
    {
        var info = AuditInfo.Rejected("op-002", "Operator Li", "质量不合格");

        info.OperatorId.Should().Be("op-002");
        info.OperatorName.Should().Be("Operator Li");
        info.Result.Should().Be("Rejected");
        info.Reason.Should().Be("质量不合格");
        info.AuditedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Rejected_EmptyOperatorId_ShouldThrowException()
    {
        var act = () => AuditInfo.Rejected("", "Operator Li", "reason");

        act.Should().Throw<ArgumentException>().WithMessage("*操作人标识*");
    }

    [Fact]
    public void Rejected_EmptyReason_ShouldThrowException()
    {
        var act = () => AuditInfo.Rejected("op-002", "Operator Li", "");

        act.Should().Throw<ArgumentException>().WithMessage("*驳回原因*");
    }
}