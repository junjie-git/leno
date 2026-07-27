// web/system-admin/src/modules/02-user-access/types/role.dto.ts

// 角色实体（对应后端 RoleDto）
export interface RoleDto {
  id: string
  name: string
  description: string
  isBuiltIn: boolean               // 内置角色不可删、名不可改
  createdAt: string
  createdBy: string
  userCount: number                // 该角色下用户数
}

// 列表查询参数
export interface ListRolesParams {
  keyword?: string
}

// 创建/编辑入参（POST/PUT /admin/roles[/{id}]）
export interface SaveRoleDto {
  name: string
  description: string
}

// 权限更新入参（PUT /admin/roles/{id}/permissions，全量替换）
export interface UpdateRolePermissionsDto {
  permissions: string[]
}

// 权限目录中的单个权限项
export interface PermissionItemDto {
  code: string                     // 如 user:read
  label: string                    // 中文标签，如「查看用户」
}

// 权限目录按模块分组（GET /admin/roles/permissions/catalog 返回）
export interface PermissionGroupDto {
  module: string                   // 模块标识，如 user
  moduleLabel: string              // 模块中文名，如「用户管理」
  permissions: PermissionItemDto[]
}
