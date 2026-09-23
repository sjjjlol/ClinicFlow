# Development progress

Target: origin/main → git@github.com:sjjjlol/ClinicFlow.git. Per-slice commit/push authorized.

| Slice | Status | Evidence |
|---|---|---|
| M0 | Verified, ready to commit | 2026-09-23: SDK 10.0.401; MySQL 8.4.8 arm64; compose --wait healthy; initial migration and /api/health ready; dotnet build 0 warnings; npm build passed; npm audit 0 vulnerabilities |
| M1–M8 | Pending | See spec/03-开发与Git交付.md |

UI decision: Apple-inspired light surface, restrained blue, rounded cards, Chinese labels and English domain terms. No clinical claims.

## M1 — Identity and workspace

2026-09-23: cookie authentication, hashed seeded credentials, CSRF and per-IP login limiting; three role policies; protected catalog reads; Apple-inspired Chinese workspace.

Evidence: `./scripts/http-tests.sh` passed A01/A18 authentication subset (three roles, invalid password, missing CSRF, protected reads, logout); `PW_CHANNEL=chrome npm run test:e2e` passed login/catalog/logout in Chrome, screenshot inspected at 1440×1000; `npm run build` passed; DemoIdentity migration applied to real MySQL. Business-write authorization tests will be added with their endpoints. Bundled Chromium download was still in progress, so local verification used installed Chrome.

M0: f0a64f2 pushed to origin/main, GitHub Actions succeeded. M1: verified, ready to commit. Next: M2 atomic multi-slot bookings, idempotency, audit and Outbox persistence.

## M2 — Atomic creation and queries

2026-09-23: multi-slot creation, transactional audit/Outbox/tasks, request fingerprint/replay, paginated list/detail/occupied-slot query, Chinese create/detail/conflict UI. All API DateTime values serialize with UTC Z.

Evidence: `./scripts/test.sh` 6/6 passed on isolated real MySQL (A02/A03/A04/A10/A11/A19); `./scripts/http-tests.sh` passed authentication plus booking/replay/403/pagination/time-zone contracts; `PW_CHANNEL=chrome npm run test:e2e` 2/2 passed; frontend build passed. Initial test-project compile exposed a transitive EF patch mismatch; fixed by directly pinning EF Relational 9.0.20 and regenerating lockfiles. CI now runs real MySQL and HTTP tests. M1 17dcf41 pushed, remote CI succeeded. M2 ready to commit; next M3 reschedule/cancel/version conflict.
