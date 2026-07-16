-- ============================================================
-- 任务 3.5 + 4.1：user_coupons 历史数据迁移与唯一索引创建
-- 数据库：SQL Server（促销域 PromotionDb）
-- 执行时机：部署 T3（优惠券 Lock 流程贯通）+ T4（领取并发安全）前执行
-- 幂等性：可重复执行（IF NOT EXISTS 守卫，重复清理幂等）
-- ============================================================
-- 背景：
--   T3 修复前，下单流程不调用 UserCoupon.Lock(orderId)，导致同券可被并发订单重复使用且永不核销。
--   T4 修复前，优惠券领取采用 read-then-write（ExistsAsync 检查后 AddAsync），无 DB 唯一约束，
--   并发领取可重复发券。本脚本为 T4.1 唯一索引创建扫清历史重复数据，并回填异常状态券。
--
-- CouponStatus 枚举值：Unused=0, Locked=1, Used=2, Expired=3
-- ============================================================

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. 清理重复领取：同一 (user_id, coupon_id) 存在多条记录时，
    --    保留最早领取（received_at 最小）的一张，删除多余的重复券。
    --    重复券源于 T4 修复前的并发领取漏洞，不清理将导致唯一索引创建失败。
    ;WITH ranked AS (
        SELECT id,
               ROW_NUMBER() OVER (
                   PARTITION BY user_id, coupon_id
                   ORDER BY received_at ASC, id ASC
               ) AS rn
        FROM user_coupons
    )
    DELETE FROM ranked WHERE rn > 1;

    -- 2. 回填历史脏数据：T3 修复前下单不调用 Lock，可能出现"已核销订单但状态仍 Unused"的券。
    --    若 used_order_id 非空（说明曾被订单核销）但 status 仍为 Unused，将其修正为 Used。
    UPDATE user_coupons
    SET status = 2,  -- Used
        used_at = COALESCE(used_at, GETUTCDATE())
    WHERE status = 0  -- Unused
      AND used_order_id IS NOT NULL;

    -- 3. 创建 (user_id, coupon_id) 唯一索引（对应 UserCouponConfiguration T4.1）
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'ux_user_coupons_user_id_coupon_id'
          AND object_id = OBJECT_ID('user_coupons')
    )
    BEGIN
        CREATE UNIQUE INDEX ux_user_coupons_user_id_coupon_id
            ON user_coupons (user_id, coupon_id);
    END

    COMMIT TRANSACTION;
    PRINT '迁移完成：重复券已清理，脏数据已回填，唯一索引已创建。';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT '迁移失败：' + ERROR_MESSAGE();
    THROW;
END CATCH

-- ============================================================
-- 备注：精确回填 locked_order_id
--   spec T3.5 要求"扫描 Unused 但已关联订单的券，回填 LockedOrderId（如有）"。
--   当前下单流程（CreateOrderDto）不传 couponId（T3.4 已说明），订单侧未记录
--   "某订单使用了哪张用户券"的映射，因此无法自动精确回填 locked_order_id。
--   如运营手工核对出特定（用户券, 订单）关联，可执行：
--     UPDATE user_coupons
--     SET status = 1, locked_order_id = '<orderId>'
--     WHERE id = '<userCouponId>' AND status = 0;
--   后续若 CreateOrderDto 扩展 couponId 字段并接入 T3.4 锁定调用，
--   新订单将在下单时自动锁定券，无需本回填。
-- ============================================================
