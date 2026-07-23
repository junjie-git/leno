using System.Security.Cryptography;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 双因子认证应用服务（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// <para>
/// 设计：通过 <see cref="TwoFactorSession"/> 聚合承载 2FA 待验证会话状态，
/// <see cref="TwoFactorSession.TempToken"/> 同时承载 6 位数字验证码（短信/邮件验证码模式）。
/// 会话默认 5 分钟 TTL，最大尝试 5 次，防止暴力破解。
/// </para>
/// <para>
/// 注意：与 <see cref="Leno.Identity.Domain.Services.ITokenVerifier"/> 承载的 TOTP（基于共享密钥 + 时间窗口）
/// 模式不同，本服务面向短信/邮件验证码场景。TOTP 模式由 <c>User.EnableTwoFactor</c> /
/// <c>User.VerifyTwoFactorCode</c> 等聚合方法直接调用 <see cref="Leno.Identity.Domain.Services.ITokenVerifier"/> 完成。
/// </para>
/// </summary>
public sealed class TwoFactorAppService
{
    /// <summary>6 位数字验证码长度。</summary>
    private const int CodeLength = 6;

    /// <summary>会话默认 TTL：5 分钟。</summary>
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(5);

    /// <summary>验证码数字字符范围（0-9）。</summary>
    private const int DigitRadix = 10;

    private readonly ITwoFactorSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TwoFactorAppService> _logger;

    public TwoFactorAppService(
        ITwoFactorSessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<TwoFactorAppService> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 启动双因子认证流程：生成 6 位数字验证码，创建 <see cref="TwoFactorSession"/> 聚合并持久化。
    /// 实际场景中调用方应在持久化后通过短信/邮件渠道下发验证码（本服务仅生成与持久化，不直接发送）。
    /// </summary>
    /// <param name="userId">待验证用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>始终返回 <c>true</c>（会话创建成功）。</returns>
    public async Task<bool> InitiateAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        // 清理该用户过期或已结束的历史会话，避免同一用户累积过多会话记录
        await _sessionRepository.CleanupExpiredByUserAsync(userId, ct).ConfigureAwait(false);

        var code = GenerateNumericCode();
        var session = TwoFactorSession.Create(
            id: Guid.NewGuid(),
            tempToken: code,
            userId: userId,
            ttl: SessionTtl);

        await _sessionRepository.AddAsync(session, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("已为用户 {UserId} 创建双因子会话，SessionId={SessionId}",
            userId, session.Id);

        return true;
    }

    /// <summary>
    /// 校验双因子验证码：按验证码查询会话，校验用户匹配与会话可验证状态，标记会话为已验证。
    /// </summary>
    /// <param name="userId">待验证用户标识。</param>
    /// <param name="code">用户输入的 6 位数字验证码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>验证通过返回 <c>true</c>；会话不存在、用户不匹配、已达最大尝试次数或已过期返回 <c>false</c>。</returns>
    public async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("验证码不可为空", "TWO_FACTOR_CODE_EMPTY");
        }

        var trimmedCode = code.Trim();
        var session = await _sessionRepository.GetByTempTokenAsync(trimmedCode, ct)
            .ConfigureAwait(false);
        if (session is null)
        {
            _logger.LogWarning("双因子会话未找到，Code={Code}", trimmedCode);
            return false;
        }

        if (session.UserId != userId)
        {
            _logger.LogWarning("双因子会话用户不匹配，ExpectedUserId={Expected}, ActualUserId={Actual}",
                userId, session.UserId);
            return false;
        }

        if (!session.CanVerify)
        {
            // 仍尝试持久化状态变更（如已过期会话被标记为 Expired）
            try
            {
                await _sessionRepository.UpdateAsync(session, ct).ConfigureAwait(false);
                await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "持久化会话终态时异常，SessionId={SessionId}", session.Id);
            }

            _logger.LogWarning("双因子会话不可验证，UserId={UserId}, Status={Status}, AttemptCount={AttemptCount}",
                userId, session.Status, session.AttemptCount);
            return false;
        }

        // 记录一次尝试，达上限会返回 false 并置 MaxAttemptsExceeded
        if (!session.RecordAttempt())
        {
            await _sessionRepository.UpdateAsync(session, ct).ConfigureAwait(false);
            await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

            _logger.LogWarning("双因子会话已达最大尝试次数或已过期，UserId={UserId}, Status={Status}",
                userId, session.Status);
            return false;
        }

        // 验证码与会话 TempToken 一致即视为通过
        session.MarkVerified();

        await _sessionRepository.UpdateAsync(session, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("双因子验证成功，UserId={UserId}, SessionId={SessionId}",
            userId, session.Id);

        return true;
    }

    /// <summary>
    /// 生成 6 位数字验证码，使用 <see cref="RandomNumberGenerator"/> 加密安全随机源。
    /// 首位允许为 0，结果固定 6 字符长度。
    /// </summary>
    private static string GenerateNumericCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, DigitRadix));
        }
        return new string(chars);
    }
}
