# Tasks

## Task 1: Fix build errors and verify existing code compiles
- [x] Run `dotnet build` on the Notification solution to identify any remaining build errors
- [x] Fix any compilation errors found (e.g., parameter naming conflicts, missing references)
- [x] Verify all projects build successfully: Domain, Application, Infrastructure, API

## Task 2: Add missing unit tests for NTF-06 (Retry and Dead Letter)
- [x] Add `TemplateRendererTests` in `Leno.Notification.Application.Tests` covering:
  - RenderAsync with all required variables - success
  - RenderAsync with missing required variable - throws NotificationDomainException
  - RenderAsync with HTML special characters - properly escaped
  - RenderAsync with optional variables missing - renders with placeholders preserved
  - ValidateUndefinedPlaceholders - detects undefined placeholders
  - ValidateUndefinedPlaceholders - returns empty when all defined
  - Content snapshot creation and format
- [x] Add `RateLimiterTests` in `Leno.Notification.Application.Tests` covering:
  - RedisRateLimiter dependency and interaction patterns
  - Rate limit check logic for each channel
  - Degradation behavior when Redis unavailable
- [x] Add `NotificationConfigAppServiceTests` in `Leno.Notification.Application.Tests` covering:
  - GetConfigAsync returns masked sensitive fields
  - UpdateConfigAsync logs audit entries
  - TestSendAsync with valid channel
  - TestSendAsync with unknown channel

## Task 3: Run all unit tests and verify they pass
- [x] Run `dotnet test "e:\Leno\src\Services\Notification\Leno.Notification.Domain.Tests"` - 151 pass, 0 fail
- [x] Run `dotnet test "e:\Leno\src\Services\Notification\Leno.Notification.Application.Tests"` - 108 pass, 0 fail
- [x] Ensure total test count meets or exceeds previous baseline (80+ tests): 259 total

## Task 4: Verify Application and Infrastructure layer test coverage
- [x] Confirm Domain layer tests have >=80% coverage of domain logic (151 tests)
- [x] Confirm all four P1 tasks (NTF-06 through NTF-09) have test coverage
- [x] Verify no test failures or skipped tests

# Task Dependencies
- Task 2 depends on Task 1 (must build before adding tests)
- Task 3 depends on Task 2 (tests must exist before running)
- Task 4 depends on Task 3 (test results needed for coverage verification)