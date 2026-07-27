// web/system-admin/src/modules/02-user-access/api/oauth-clients.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { OAuthClientDto, UpdateOAuthClientDto, ListOAuthClientsParams } from '../types/oauth-client.dto'

// OAuth 客户端管理 API（Identity 域 AdminOAuthClientsController）
export const oauthClientsApi = {
  // 查询所有 OAuth 客户端配置（Secret 掩码）
  list: (params?: ListOAuthClientsParams) =>
    client.get<OAuthClientDto[]>('/admin/oauth-clients', { params }),

  // 新建 OAuth 客户端配置（默认禁用，需显式调用 enable）
  create: (provider: string, body: UpdateOAuthClientDto) =>
    client.post<OAuthClientDto>(`/admin/oauth-clients/${provider}`, body, withIdempotency()),

  // 更新指定提供方配置
  update: (provider: string, body: UpdateOAuthClientDto) =>
    client.put<OAuthClientDto>(`/admin/oauth-clients/${provider}`, body, withIdempotency()),

  // 启用指定提供方（幂等）
  enable: (provider: string) =>
    client.post<OAuthClientDto>(`/admin/oauth-clients/${provider}/enable`, null, withIdempotency()),

  // 禁用指定提供方（幂等）
  disable: (provider: string) =>
    client.post<OAuthClientDto>(`/admin/oauth-clients/${provider}/disable`, null, withIdempotency()),
}
