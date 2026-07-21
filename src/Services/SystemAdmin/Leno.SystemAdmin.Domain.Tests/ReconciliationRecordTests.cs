using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class ReconciliationRecordTests
{
    private static readonly Guid ValidRecordId = Guid.NewGuid();
    private static readonly ReportPeriod ValidPeriod = new(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
    private static readonly List<MetricItem> ValidAggregatedMetrics = new()
    {
        new("total_orders", 1000m, "单"),
        new("total_gmv", 50000m, "CNY")
    };
    private static readonly List<MetricItem> ValidDomainMetrics = new()
    {
        new("total_orders", 950m, "单"),
        new("total_gmv", 48000m, "CNY")
    };
    private static readonly List<MetricDiscrepancy> ValidDiscrepancies = new()
    {
        new("total_orders", 1000m, 950m),
        new("total_gmv", 50000m, 48000m)
    };

    private static StatisticsSnapshot CreateValidSnapshot() => new(
        ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics, ValidDiscrepancies);

    private static StatisticsSnapshot CreateConsistentSnapshot() => new(
        ReportType.OrderGmv, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics,
        new List<MetricDiscrepancy>());

    #region Create - Happy Path

    [Fact]
    public void Create_WithDiscrepancies_ShouldSetDiscrepancyStatus()
    {
        var snapshot = CreateValidSnapshot();
        var record = ReconciliationRecord.Create(ValidRecordId, snapshot);

        record.RecordId.Should().Be(ValidRecordId);
        record.Id.Should().Be(ValidRecordId);
        record.ReportType.Should().Be(ReportType.OrderGmv);
        record.Snapshot.Should().Be(snapshot);
        record.ReconciledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        record.Status.Should().Be(ReconciliationStatus.DiscrepancyFound);
        record.AlertTriggered.Should().BeFalse();
        record.CorrectionTriggered.Should().BeFalse();
    }

    [Fact]
    public void Create_WithConsistentSnapshot_ShouldSetConsistentStatus()
    {
        var snapshot = CreateConsistentSnapshot();
        var record = ReconciliationRecord.Create(ValidRecordId, snapshot);

        record.Status.Should().Be(ReconciliationStatus.Consistent);
    }

    [Fact]
    public void Create_WithErrorSnapshot_ShouldSetErrorStatus()
    {
        var snapshot = StatisticsSnapshot.CreateError(
            ReportType.OrderGmv, ValidPeriod, "对账异常");
        var record = ReconciliationRecord.Create(ValidRecordId, snapshot);

        record.Status.Should().Be(ReconciliationStatus.Error);
    }

    [Fact]
    public void Create_WithAllReportTypes_ShouldSucceed()
    {
        foreach (var reportType in Enum.GetValues<ReportType>())
        {
            var snapshot = new StatisticsSnapshot(
                reportType, ValidPeriod, ValidAggregatedMetrics, ValidDomainMetrics,
                new List<MetricDiscrepancy>());
            var record = ReconciliationRecord.Create(Guid.NewGuid(), snapshot);

            record.ReportType.Should().Be(reportType);
        }
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyRecordId_ShouldThrowRecordIdEmpty()
    {
        var act = () => ReconciliationRecord.Create(Guid.Empty, CreateValidSnapshot());

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("RECONCILIATION_RECORD_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullSnapshot_ShouldThrowArgumentNull()
    {
        var act = () => ReconciliationRecord.Create(ValidRecordId, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region MarkAlertTriggered

    [Fact]
    public void MarkAlertTriggered_ShouldSetAlertTriggeredToTrue()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkAlertTriggered();

        record.AlertTriggered.Should().BeTrue();
    }

    [Fact]
    public void MarkAlertTriggered_ShouldNotAffectCorrectionTriggered()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkAlertTriggered();

        record.AlertTriggered.Should().BeTrue();
        record.CorrectionTriggered.Should().BeFalse();
    }

    #endregion

    #region MarkCorrectionTriggered

    [Fact]
    public void MarkCorrectionTriggered_ShouldSetCorrectionTriggeredToTrue()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkCorrectionTriggered();

        record.CorrectionTriggered.Should().BeTrue();
    }

    [Fact]
    public void MarkCorrectionTriggered_ShouldNotAffectAlertTriggered()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkCorrectionTriggered();

        record.CorrectionTriggered.Should().BeTrue();
        record.AlertTriggered.Should().BeFalse();
    }

    [Fact]
    public void MarkBothTriggered_ShouldSetBothFlags()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkAlertTriggered();
        record.MarkCorrectionTriggered();

        record.AlertTriggered.Should().BeTrue();
        record.CorrectionTriggered.Should().BeTrue();
    }

    #endregion

    #region Idempotency

    [Fact]
    public void MarkAlertTriggered_CalledTwice_ShouldRemainTrueAndIdempotent()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkAlertTriggered();
        record.MarkAlertTriggered();

        record.AlertTriggered.Should().BeTrue();
        record.CorrectionTriggered.Should().BeFalse();
    }

    [Fact]
    public void MarkCorrectionTriggered_CalledTwice_ShouldRemainTrueAndIdempotent()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkCorrectionTriggered();
        record.MarkCorrectionTriggered();

        record.CorrectionTriggered.Should().BeTrue();
        record.AlertTriggered.Should().BeFalse();
    }

    [Fact]
    public void MarkBothTriggered_CalledTwice_ShouldRemainBothTrueAndIdempotent()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());

        record.MarkAlertTriggered();
        record.MarkCorrectionTriggered();
        record.MarkAlertTriggered();
        record.MarkCorrectionTriggered();

        record.AlertTriggered.Should().BeTrue();
        record.CorrectionTriggered.Should().BeTrue();
    }

    [Fact]
    public void MarkAlertTriggered_OnAlreadyTriggeredRecord_ShouldNotAffectSnapshotFields()
    {
        var record = ReconciliationRecord.Create(ValidRecordId, CreateValidSnapshot());
        var originalSnapshot = record.Snapshot;
        var originalReconciledAt = record.ReconciledAt;
        var originalStatus = record.Status;
        var originalReportType = record.ReportType;

        record.MarkAlertTriggered();
        record.MarkAlertTriggered();

        record.Snapshot.Should().BeSameAs(originalSnapshot);
        record.ReconciledAt.Should().Be(originalReconciledAt);
        record.Status.Should().Be(originalStatus);
        record.ReportType.Should().Be(originalReportType);
    }

    #endregion

    #region Immutability

    [Fact]
    public void ReconciliationRecord_ShouldBeImmutable_NoUpdateMethods()
    {
        var recordType = typeof(ReconciliationRecord);
        var methods = recordType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == recordType);

        var instanceMethods = methods.Where(m => !m.Name.Contains("get_") && !m.Name.Contains("set_"));
        var updateMethods = instanceMethods.Where(m => m.Name != "MarkAlertTriggered" && m.Name != "MarkCorrectionTriggered");

        updateMethods.Should().BeEmpty("ReconciliationRecord should have no public instance update methods beyond MarkAlertTriggered/MarkCorrectionTriggered");
    }

    #endregion
}