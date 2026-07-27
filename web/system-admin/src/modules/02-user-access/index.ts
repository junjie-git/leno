// web/system-admin/src/modules/02-user-access/index.ts

export { default as routes } from './routes'
export { usersApi } from './api/users.api'
export { rolesApi } from './api/roles.api'
export { oauthClientsApi } from './api/oauth-clients.api'
export { operatorsApi } from './api/operators.api'
export { default as RolePermissionMatrix } from './components/RolePermissionMatrix.vue'
export type {
  UserDto,
  UserStatus,
  ListUsersParams,
  AssignUserRolesDto,
  UpdateUserStatusDto,
} from './types/user.dto'
export type {
  RoleDto,
  ListRolesParams,
  SaveRoleDto,
  UpdateRolePermissionsDto,
  PermissionGroupDto,
  PermissionItemDto,
} from './types/role.dto'
export type {
  OAuthClientDto,
  UpdateOAuthClientDto,
  ListOAuthClientsParams,
  OAuthProvider,
} from './types/oauth-client.dto'
export type {
  OperatorDto,
  OperatorStatus,
  OperatorRole,
  ListOperatorsParams,
  SaveOperatorDto,
  AssignOperatorPermissionsDto,
} from './types/operator.dto'
export { SUPPORTED_OAUTH_PROVIDERS, OAUTH_PROVIDER_LABELS } from './types/oauth-client.dto'
export { OPERATOR_ROLE_OPTIONS } from './types/operator.dto'
