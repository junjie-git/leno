/**
 * 08-membership-ops 会员运营模块桶导出
 *
 * - 默认导出：模块路由（懒加载视图，由 app/router 聚合到 BasicLayout children）
 * - 具名导出：三个域的 API 与全部 DTO 类型
 */
export { default } from './routes'
export { memberLevelApi } from './api/memberLevel.api'
export { membershipPackageApi } from './api/membershipPackage.api'
export { pointsRuleApi } from './api/pointsRule.api'
export type {
  MemberLevelStatus,
  MemberLevelDto,
  SaveMemberLevelDto,
  CreateMemberLevelDto,
  UpdateMemberLevelDto,
} from './types/memberLevel.dto'
export type {
  MembershipPackageStatus,
  MembershipBenefit,
  MembershipPackageDto,
  SaveMembershipPackageDto,
  CreateMembershipPackageDto,
  UpdateMembershipPackageDto,
} from './types/membershipPackage.dto'
export type {
  PointsRuleStatus,
  PointsActionType,
  PointsRuleDto,
  SavePointsRuleDto,
  CreatePointsRuleDto,
  UpdatePointsRuleDto,
  AwardPointsDto,
} from './types/pointsRule.dto'
