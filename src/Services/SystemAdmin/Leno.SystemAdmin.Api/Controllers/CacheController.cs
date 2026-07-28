using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 缓存监控控制器（5 Endpoints）：INFO 概览、keyspace、key 列表、key 详情、删除 key。
/// Redis 不可用时返回 503；db 越界、pattern/key 非法返回 400。
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class CacheController : SystemAdminControllerBase
{
    private readonly ICacheMonitorAppService _cacheMonitorAppService;

    public CacheController(
        ICurrentUserContext currentUser,
        ICacheMonitorAppService cacheMonitorAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(cacheMonitorAppService);
        _cacheMonitorAppService = cacheMonitorAppService;
    }

    /// <summary>获取 Redis INFO 概览。</summary>
    [HttpGet("api/admin/cache/info")]
    [ProducesResponseType(typeof(ApiResponse<RedisInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetInfoAsync(CancellationToken ct)
    {
        var info = await _cacheMonitorAppService.GetRedisInfoAsync(ct);
        return Ok(ApiResponse.Success(info));
    }

    /// <summary>获取 16 个 db 的 keyspace 信息。</summary>
    [HttpGet("api/admin/cache/keyspaces")]
    [ProducesResponseType(typeof(ApiResponse<List<KeyspaceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetKeyspacesAsync(CancellationToken ct)
    {
        var keyspaces = await _cacheMonitorAppService.GetKeyspacesAsync(ct);
        return Ok(ApiResponse.Success(keyspaces));
    }

    /// <summary>分页查询 key 列表（SCAN + TYPE 过滤）。</summary>
    [HttpGet("api/admin/cache/keys")]
    [ProducesResponseType(typeof(ApiResponse<CacheKeyQueryResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> QueryKeysAsync(
        [FromQuery] int db = 0,
        [FromQuery] string pattern = "*",
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _cacheMonitorAppService.QueryKeysAsync(db, pattern, type, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取单个 key 详情（含 value，大 key 截断）。</summary>
    [HttpGet("api/admin/cache/keys/{key}")]
    [ProducesResponseType(typeof(ApiResponse<RedisKeyDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetKeyDetailAsync(string key, [FromQuery] int db = 0, CancellationToken ct = default)
    {
        var detail = await _cacheMonitorAppService.GetKeyDetailAsync(key, db, ct);
        if (detail is null)
        {
            return NotFound(ApiResponse.Fail(404, "缓存 key 不存在"));
        }
        return Ok(ApiResponse.Success(detail));
    }

    /// <summary>删除缓存 key（危险操作，由 [AuditLog] 记录）。</summary>
    [HttpDelete("api/admin/cache/keys/{key}")]
    [ProducesResponseType(typeof(ApiResponse<CacheKeyDeleteResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteKeyAsync(string key, [FromQuery] int db = 0, CancellationToken ct = default)
    {
        var result = await _cacheMonitorAppService.DeleteKeyAsync(key, db, ct);
        return Ok(ApiResponse.Success(result));
    }
}
