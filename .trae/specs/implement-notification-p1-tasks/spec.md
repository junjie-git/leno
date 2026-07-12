# Notification P1 Tasks Spec

## Why
Complete the four P1 priority tasks for the Notification bounded context in the Leno e-commerce platform. These tasks provide the core reliability, configurability, and anti-abuse mechanisms for the notification system: retry/dead-letter handling, template rendering, channel configuration management, and rate limiting.

## What Changes
- **NTF-06**: 发送失败重试与死信处理 -- Retry policy with exponential backoff (30s/2min/10min), error classification (retryable vs non-retryable), dead letter queue management with batch resend/discard, and audit logging
- **NTF-07**: 模板渲染服务 -- `{{variable}}` placeholder rendering, required variable validation, HTML escaping for XSS prevention, content snapshot creation, and undefined placeholder detection
- **NTF-08**: 渠道参数配置管理 -- Channel configuration CRUD (Email/SMS), sensitive field encryption/masking, test send verification, and audit logging for config changes
- **NTF-09**: 通知频率限制与防骚扰 -- Redis-based sliding window rate limiting (Email: 10/hr, SMS: 5/hr + 20/day), degradation to allow-on-failure, and runtime config management

## Impact
- Affected specs: Notification domain (retry, template, config, rate-limit capabilities)
- Affected code:
  - Domain: `IRetryPolicy`, `ITemplateRenderService`, `ITemplateRenderer`, `IRateLimiter`, `NotificationRecord` aggregate, `ChannelConfigVO`
  - Infrastructure: `RetryPolicy`, `TemplateRenderer`, `RedisRateLimiter`, `NotificationRetryJob`, `EfCoreNotificationRecordRepository`
  - Application: `DeadLetterAppService`, `NotificationConfigAppService`, `RateLimitAppService`, DTOs
  - API: `DeadLetterController`, `NotificationConfigController`, `NotificationRateLimitsController`
  - Tests: `NotificationRecordTests`, `RetryPolicyTests`

## ADDED Requirements

### Requirement: NTF-06 - Retry Policy and Dead Letter Handling
The system SHALL provide automatic retry for failed notifications with exponential backoff, error classification, and dead letter queue management.

#### Scenario: Retryable error triggers retry scheduling
- **WHEN** a notification send fails with a retryable error code (e.g., SMTP_RETRYABLE, SMS_TIMEOUT)
- **THEN** the system SHALL schedule a retry with exponential backoff: 30s for 1st retry, 2min for 2nd, 10min for 3rd

#### Scenario: Non-retryable error moves directly to dead letter
- **WHEN** a notification send fails with a non-retryable error code (e.g., SMTP_NON_RETRYABLE, EMAIL_EMPTY)
- **THEN** the system SHALL move the notification directly to the dead letter queue without retrying

#### Scenario: Max retry count exceeded moves to dead letter
- **WHEN** a notification has been retried 3 times (DefaultMaxRetry) and still fails
- **THEN** the system SHALL move the notification to the dead letter queue with the reason "超过最大重试次数"

#### Scenario: Operator resends dead letter notification
- **WHEN** an operator manually resends a dead-lettered notification
- **THEN** the system SHALL reset retry count to 0, clear error state, and attempt to resend

#### Scenario: Operator discards dead letter notification
- **WHEN** an operator manually discards a dead-lettered notification with a reason
- **THEN** the system SHALL record the discard reason and log an audit entry

### Requirement: NTF-07 - Template Rendering Service
The system SHALL render notification templates by replacing `{{variable}}` placeholders with actual values, validating required variables, and escaping HTML content.

#### Scenario: Template renders successfully with all required variables
- **WHEN** a template is rendered with all required variables provided
- **THEN** the system SHALL replace all placeholders and return the rendered title and content, with a content snapshot

#### Scenario: Required variable missing throws exception
- **WHEN** a required variable is missing or empty
- **THEN** the system SHALL throw `NotificationDomainException` with error code `TEMPLATE_REQUIRED_VARIABLE_MISSING`

#### Scenario: HTML special characters are escaped in content
- **WHEN** template content contains HTML special characters in variable values
- **THEN** the system SHALL escape them (`&`, `<`, `>`, `"`, `'`) to prevent XSS injection

#### Scenario: Undefined placeholders detected during template validation
- **WHEN** a template body contains `{{placeholder}}` that is not declared in the template's variables list
- **THEN** `ValidateUndefinedPlaceholders` SHALL return the list of undefined placeholder names

### Requirement: NTF-08 - Channel Parameter Configuration Management
The system SHALL provide CRUD operations for Email and SMS channel configurations, with sensitive field masking and test send verification.

#### Scenario: Get channel config with sensitive fields masked
- **WHEN** an operator retrieves the Email channel configuration
- **THEN** the system SHALL return the config with `SmtpPassword` displayed as `******`

#### Scenario: Update channel config with audit logging
- **WHEN** an operator updates any channel configuration
- **THEN** the system SHALL log an audit entry with the operator ID, channel, and changed fields (sensitive fields masked in log)

#### Scenario: Test send verifies channel configuration
- **WHEN** an operator performs a test send to a specific email or phone number
- **THEN** the system SHALL attempt to send a test message through the specified channel and return the result

### Requirement: NTF-09 - Notification Rate Limiting and Anti-Harassment
The system SHALL enforce rate limits on notification sending to prevent harassment, with Redis-based sliding window and degradation to allow-on-failure.

#### Scenario: Rate limit enforced for Email channel
- **WHEN** a user has received 10 email notifications in the past hour
- **THEN** the 11th email SHALL be rejected with error code `RATE_LIMITED`

#### Scenario: Rate limit enforced for SMS channel (hourly)
- **WHEN** a user has received 5 SMS notifications in the past hour
- **THEN** the next SMS SHALL be rejected with error code `RATE_LIMITED`

#### Scenario: Rate limit enforced for SMS channel (daily)
- **WHEN** a user has received 20 SMS notifications in the past 24 hours
- **THEN** the next SMS SHALL be rejected with error code `RATE_LIMITED`

#### Scenario: InApp channel is not rate limited
- **WHEN** a notification is sent through the InApp channel
- **THEN** the system SHALL always allow it regardless of count

#### Scenario: Redis unavailable degrades to allow
- **WHEN** the Redis connection is unavailable during rate limit check
- **THEN** the system SHALL log an error and allow the notification to proceed (degradation)

#### Scenario: Operator updates rate limit configuration
- **WHEN** an operator updates the rate limit for a channel
- **THEN** the system SHALL log an audit entry and update the in-memory configuration

## MODIFIED Requirements
None. All requirements are new additions.

## REMOVED Requirements
None.