# Verification Checklist

## Build Verification
- [x] All Notification projects compile without errors (Domain, Application, Infrastructure, API)
- [x] No CA1716 or other naming conflict warnings remain
- [x] No missing assembly references or type resolution errors

## NTF-06: Retry and Dead Letter
- [x] `IRetryPolicy` interface is defined in Domain layer with `ShouldRetry` and `NextDelay` methods
- [x] `RetryPolicy` implementation correctly classifies retryable vs non-retryable error codes
- [x] `RetryPolicy` provides exponential backoff delays: 30s, 2min, 10min
- [x] `NotificationRetryJob` processes failed records in two phases (classification + scheduled retry)
- [x] `NotificationRetryJob` handles non-retryable errors by moving directly to dead letter
- [x] `DeadLetterAppService` supports list, batch resend, and batch discard operations
- [x] `DeadLetterAppService` logs audit entries for batch operations
- [x] `NotificationRecord.MarkResend()` resets retry count and error state from DeadLettered
- [x] `NotificationRecord.MarkDiscarded()` records discard reason with validation
- [x] `NotificationRecord` state machine: Pending -> Sending -> Succeeded/Failed -> Retried -> DeadLettered
- [x] `EfCoreNotificationRecordRepository` supports all dead letter and retry query methods
- [x] `RetryPolicyTests` cover all retryable/non-retryable codes and delay calculations
- [x] `NotificationRecordTests` cover MarkResend, MarkDiscarded, and full state machine lifecycle

## NTF-07: Template Rendering
- [x] `ITemplateRenderService` interface is defined in Domain layer
- [x] `ITemplateRenderer` interface is defined in Domain layer (sync Render method)
- [x] `TemplateRenderer` implements both interfaces with variable validation, HTML escaping, and snapshot creation
- [x] `TemplateRenderer.RenderAsync()` validates required variables before rendering
- [x] `TemplateRenderer.RenderAsync()` escapes HTML special characters in content
- [x] `TemplateRenderer.RenderAsync()` creates a JSON content snapshot
- [x] `TemplateRenderer.ValidateUndefinedPlaceholders()` detects undeclared `{{placeholders}}`
- [x] Template rendering tests cover all scenarios

## NTF-08: Channel Configuration Management
- [x] `INotificationConfigAppService` interface is defined in Application layer
- [x] `NotificationConfigAppService` returns masked sensitive fields (password, secret)
- [x] `NotificationConfigAppService` logs audit entries for configuration changes
- [x] `NotificationConfigAppService` supports test send verification
- [x] `NotificationConfigController` exposes GET/PUT config and POST test-send endpoints
- [x] `ChannelConfigVO` value object is defined in Domain layer
- [x] DTOs for config, test send, and rate limits are defined in Application layer
- [x] Channel config tests cover masking, audit logging, and test send

## NTF-09: Rate Limiting
- [x] `IRateLimiter` interface is defined in Domain layer with `AcquireAsync` method
- [x] `RedisRateLimiter` implements sliding window rate limiting with Redis Sorted Sets
- [x] `RedisRateLimiter` enforces Email: 10/hr, SMS: 5/hr + 20/day, InApp: unlimited
- [x] `RedisRateLimiter` degrades to allow when Redis is unavailable
- [x] `IRateLimitAppService` interface is defined in Application layer
- [x] `RateLimitAppService` supports get and update rate limit configs with audit logging
- [x] `NotificationRateLimitsController` exposes GET/PUT rate limit endpoints
- [x] Rate limit tests cover all channels and degradation behavior

## Test Coverage
- [x] Domain layer tests: >= 80% code coverage on domain logic (151 tests)
- [x] All existing tests (NotificationRecordTests, RetryPolicyTests, etc.) pass
- [x] New tests for TemplateRenderer, RateLimiter, NotificationConfigAppService pass (33 new tests)
- [x] Total test count >= 80 (baseline from previous phase): 259 total (151 Domain + 108 Application)