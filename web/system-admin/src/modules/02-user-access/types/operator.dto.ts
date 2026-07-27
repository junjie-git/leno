// web/system-admin/src/modules/02-user-access/types/operator.dto.ts

// 运营人员状态
export type OperatorStatus = 'Active' | 'Inactive'

// 运营人员角色（后端枚举，对应 OperatorRole）
export type OperatorRole = 'Operator' | 'SeniorOperator' | 'Manager'

// 运营人员实体（对应后端 OperatorDto）
export interface OperatorDto {
  operatorId: string
  username: string
  name: string
  email: string
  role: OperatorRole
  status: OperatorStatus
  permissions: string[]           // 权限码列表
  createdAt: string
  lastLoginAt: string | null
}

// 列表查询参数
export interface ListOperatorsParams {
  role?: OperatorRole
  status?: OperatorStatus
}

// 创建运营人员入参（POST /admin/operators）
export interface SaveOperatorDto {
  username: string
  name: string
  email: string
  password: string                // 初始密码
  role: OperatorRole
}

// 权限分配入参（PUT /admin/operators/{id}/permissions，合并新增）
export interface AssignOperatorPermissionsDto {
  permissions: string[]
}

// 运营角色下拉选项（视图层复用）
export const OPERATOR_ROLE_OPTIONS: { label: string; value: OperatorRole }[] = [
  { label: '运营', value: 'Operator' },
  { label: '高级运营', value: 'SeniorOperator' },
  { label: '主管', value: 'Manager' },
]
