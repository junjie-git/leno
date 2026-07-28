using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>.NET 进程监控抽象。</summary>
public interface IDotNetProcessMonitor
{
    Task<ServerSnapshotDto> GetSnapshotAsync(CancellationToken ct = default);
}
