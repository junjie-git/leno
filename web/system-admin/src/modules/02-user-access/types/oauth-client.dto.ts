// web/system-admin/src/modules/02-user-access/types/oauth-client.dto.ts

// OAuth 客户端配置（对应后端 OAuthClientDto，Secret 始终掩码）
export interface OAuthClientDto {
  provider: string                 // github / google / wechat / qq / alipay
  clientId: string
  clientSecretMasked: string       // 形如 ******** 后4位
  scopes: string[]
  authorizationEndpoint: string
  tokenEndpoint: string
  userInfoEndpoint: string
  redirectUri: string              // 回调 URL
  enabled: boolean
}

// 新建/编辑入参（POST/PUT /admin/oauth-clients/{provider}）
export interface UpdateOAuthClientDto {
  clientId: string
  clientSecret: string             // 编辑时若留空则后端保留原密钥
  scopes: string[]
  authorizationEndpoint: string
  tokenEndpoint: string
  userInfoEndpoint: string
  redirectUri: string
}

// 列表筛选参数
export interface ListOAuthClientsParams {
  enabled?: boolean                // undefined=全部
}

// 受支持的 OAuth 提供方白名单（新建时下拉选项）
export const SUPPORTED_OAUTH_PROVIDERS = [
  'github',
  'google',
  'wechat',
  'qq',
  'alipay',
] as const

export type OAuthProvider = typeof SUPPORTED_OAUTH_PROVIDERS[number]

// 提供方中文标签映射（用于表格与下拉展示）
export const OAUTH_PROVIDER_LABELS: Record<string, string> = {
  github: 'GitHub',
  google: 'Google',
  wechat: '微信',
  qq: 'QQ',
  alipay: '支付宝',
}
