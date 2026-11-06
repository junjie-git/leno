-- =============================================================================
-- Leno 最小种子数据（本地开发/联调专用，幂等可重复执行）
--
-- 用途：为 BFF 商品详情聚合端点提供真实可验证的下游数据：
--   * leno_product：1 个类目 + 1 个 SPU + 1 个 SKU + 1 条库存基线
--   * LenoOrder：1 笔已完成订单 + 1 条订单明细
--   * leno_review：1 条已审核通过（Approved=1）的评价
--
-- 账号（Seller/买家）由 scripts/seed-minimal-data.ps1 通过 Identity 注册接口创建，
-- 因为口令为 Argon2 + pepper 哈希，SQL 直插无法生成可登录凭据。
-- 本脚本通过 sqlcmd -v 接收两个账号的 UserId：
--   -v SellerId="..." -v BuyerId="..."
--
-- 主键使用固定 GUID，保证重复执行不产生重复数据。
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 1) 商品域：类目 + SPU + SKU + 库存基线
-- ---------------------------------------------------------------------------
USE leno_product;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM categories WHERE id = '11111111-1111-1111-1111-111111111111')
BEGIN
    INSERT INTO categories (id, name, parent_id, level, sort_order, status, created_at, updated_at, created_by, updated_by)
    VALUES ('11111111-1111-1111-1111-111111111111', N'种子类目', NULL, 1, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed');
END
GO

IF NOT EXISTS (SELECT 1 FROM spus WHERE id = '22222222-2222-2222-2222-222222222222')
BEGIN
    INSERT INTO spus (id, shop_id, seller_id, title, subtitle, main_image_url, category_id, brand_id, status,
                      specs, suspended_by_shop, reviewed_by, audit_history, created_at, updated_at, created_by, updated_by)
    VALUES ('22222222-2222-2222-2222-222222222222',
            '88888888-8888-8888-8888-888888888888',
            '$(SellerId)',
            N'种子商品-机械键盘', N'最小种子数据用于 BFF 聚合联调',
            N'https://example.com/seed-product.jpg',
            '11111111-1111-1111-1111-111111111111', NULL, 2,
            N'["轴体:红轴"]', 0, NULL, N'[]',
            SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed');
END
GO

-- 重跑时把卖家归属对齐到当前 Seller 账号，避免账号重建后数据指向旧 ID
UPDATE spus SET seller_id = '$(SellerId)', specs = N'["轴体:红轴"]',
    updated_at = SYSUTCDATETIME(), updated_by = N'seed'
WHERE id = '22222222-2222-2222-2222-222222222222';
GO

IF NOT EXISTS (SELECT 1 FROM skus WHERE id = '33333333-3333-3333-3333-333333333333')
BEGIN
    INSERT INTO skus (id, spu_id, sku_code, price, currency, stock_qty, spec_attributes, status,
                      image_url, created_at, updated_at, created_by, updated_by)
    VALUES ('33333333-3333-3333-3333-333333333333',
            '22222222-2222-2222-2222-222222222222',
            N'SEED-SKU-001', 199.00, N'CNY', 100, N'[{"Name":"颜色","Value":"黑色"}]', 0,
            N'https://example.com/seed-sku.jpg',
            SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed');
END
GO

-- 重跑时修正 SKU 规格 JSON（SkuSpec 序列化为 SpecAttribute 数组）
UPDATE skus SET spec_attributes = N'[{"Name":"颜色","Value":"黑色"}]',
    updated_at = SYSUTCDATETIME(), updated_by = N'seed'
WHERE id = '33333333-3333-3333-3333-333333333333';
GO

IF NOT EXISTS (SELECT 1 FROM stock_baselines WHERE id = '77777777-7777-7777-7777-777777777777')
BEGIN
    INSERT INTO stock_baselines (id, sku_id, available_qty, reserved_qty, deducted_qty, product_id,
                                 created_at, updated_at, created_by, updated_by)
    VALUES ('77777777-7777-7777-7777-777777777777',
            '33333333-3333-3333-3333-333333333333', 100, 0, 0,
            '22222222-2222-2222-2222-222222222222',
            SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed');
END
GO

-- ---------------------------------------------------------------------------
-- 2) 订单域：1 笔已完成订单 + 1 条订单明细（OrderStatus Completed = 3）
-- ---------------------------------------------------------------------------
USE LenoOrder;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM orders WHERE id = '44444444-4444-4444-4444-444444444444')
BEGIN
    INSERT INTO orders (id, order_no, order_type, user_id, seller_id, items_amount, discount_amount,
                        points_offset_amount, freight_amount, total_amount, status, recipient_name,
                        recipient_phone, province, city, district, address_detail, payment_method,
                        payment_initiated, payment_initiated_at, expire_at, paid_at, payment_id, trade_no,
                        shipped_at, logistics_no, LogisticsCompanyCode, completed_at, cancelled_at,
                        cancel_reason, created_at, updated_at, created_by, updated_by, is_deleted, deleted_at)
    VALUES ('44444444-4444-4444-4444-444444444444', N'SEED0000000001', 0, '$(BuyerId)', '$(SellerId)',
            199.00, 0, 0, 0, 199.00, 3, N'种子买家', N'13800000000',
            N'上海市', N'上海市', N'浦东新区', N'种子路 1 号', 1,
            1, SYSUTCDATETIME(), DATEADD(DAY, 1, SYSUTCDATETIME()), DATEADD(MINUTE, -60, SYSUTCDATETIME()),
            '99999999-9999-9999-9999-999999999999', N'SEEDTRADE0001',
            DATEADD(MINUTE, -30, SYSUTCDATETIME()), N'SEEDLOGISTICS001', N'SF',
            DATEADD(MINUTE, -10, SYSUTCDATETIME()), NULL, NULL,
            SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed', 0, NULL);
END
GO

UPDATE orders SET user_id = '$(BuyerId)', seller_id = '$(SellerId)', updated_at = SYSUTCDATETIME(), updated_by = N'seed'
WHERE id = '44444444-4444-4444-4444-444444444444';
GO

IF NOT EXISTS (SELECT 1 FROM order_items WHERE id = '55555555-5555-5555-5555-555555555555')
BEGIN
    INSERT INTO order_items (id, sku_id, product_sku_id, product_spu_id, product_name, product_sku_name,
                             product_main_image, product_seller_id, unit_price, quantity, discount_allocation,
                             subtotal, source_cart_item_id, OrderId, created_at, updated_at, created_by, updated_by)
    VALUES ('55555555-5555-5555-5555-555555555555',
            '33333333-3333-3333-3333-333333333333',
            '33333333-3333-3333-3333-333333333333',
            '22222222-2222-2222-2222-222222222222',
            N'种子商品-机械键盘', N'黑色/红轴',
            N'https://example.com/seed-sku.jpg', '$(SellerId)', 199.00, 1, 0, 199.00, NULL,
            '44444444-4444-4444-4444-444444444444',
            SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed');
END
GO

-- ---------------------------------------------------------------------------
-- 3) 评价域：1 条已审核通过评价（ReviewStatus Approved = 1）
-- ---------------------------------------------------------------------------
USE leno_review;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM reviews WHERE id = '66666666-6666-6666-6666-666666666666')
BEGIN
    INSERT INTO reviews (id, order_id, order_line_id, spu_id, sku_id, user_id, seller_id, rating, content,
                         status, seller_reply_content, seller_reply_by, seller_reply_at, submitted_at,
                         audited_at, auditor_id, hidden_at, hidden_by, hide_reason, append_content, appended_at,
                         images, append_images, created_at, updated_at, created_by, updated_by)
    VALUES ('66666666-6666-6666-6666-666666666666',
            '44444444-4444-4444-4444-444444444444',
            '55555555-5555-5555-5555-555555555555',
            '22222222-2222-2222-2222-222222222222',
            '33333333-3333-3333-3333-333333333333',
            '$(BuyerId)', '$(SellerId)', 5, N'种子评价：手感很好，物流很快。',
            1, NULL, NULL, NULL, DATEADD(DAY, -1, SYSUTCDATETIME()),
            SYSUTCDATETIME(), '$(SellerId)', NULL, NULL, NULL, NULL, NULL,
            N'[]', N'[]', SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed');
END
GO

UPDATE reviews SET user_id = '$(BuyerId)', seller_id = '$(SellerId)', updated_at = SYSUTCDATETIME(), updated_by = N'seed'
WHERE id = '66666666-6666-6666-6666-666666666666';
GO
