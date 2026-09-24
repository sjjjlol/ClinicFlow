# Development progress

Target: origin/main → git@github.com:sjjjlol/ClinicFlow.git. Per-slice commit/push authorized.

| Slice | Status | Evidence |
|---|---|---|
| M0 | Delivered | f0a64f2; remote CI passed |
| M1 | Delivered | 17dcf41; remote CI passed |
| M2 | Delivered | 097b42f; remote CI passed |
| M3 | Delivered | df9bdbc; remote CI passed |
| M4 | Delivered | b6f1547; remote CI passed |
| M5 | Delivered | 99d8a70; remote CI passed |
| M6 | Delivered | e0c7024; remote CI passed |
| M7 | Delivered | dbeff8f; remote CI passed |
| M8a | Delivered | e80c473; expanded remote CI passed, portable artifact uploaded |
| M8b | Verified, ready to commit | Fresh-volume readiness fix, clean deployment, response-loss and failure-UI regression tests; final commit CI tracked in GitHub Actions |

UI decision: Apple-inspired light surface, restrained blue, rounded cards, Chinese labels and English domain terms. No clinical claims.

## M1 — Identity and workspace

2026-09-23: cookie authentication, hashed seeded credentials, CSRF and per-IP login limiting; three role policies; protected catalog reads; Apple-inspired Chinese workspace.

Evidence: `./scripts/http-tests.sh` passed A01/A18 authentication subset (three roles, invalid password, missing CSRF, protected reads, logout); `PW_CHANNEL=chrome npm run test:e2e` passed login/catalog/logout in Chrome, screenshot inspected at 1440×1000; `npm run build` passed; DemoIdentity migration applied to real MySQL. Business-write authorization tests will be added with their endpoints. Bundled Chromium download was still in progress, so local verification used installed Chrome.

M0: f0a64f2 pushed to origin/main, GitHub Actions succeeded. M1: verified, ready to commit. Next: M2 atomic multi-slot bookings, idempotency, audit and Outbox persistence.

## M2 — Atomic creation and queries

2026-09-23: multi-slot creation, transactional audit/Outbox/tasks, request fingerprint/replay, paginated list/detail/occupied-slot query, Chinese create/detail/conflict UI. All API DateTime values serialize with UTC Z.

Evidence: `./scripts/test.sh` 6/6 passed on isolated real MySQL (A02/A03/A04/A10/A11/A19); `./scripts/http-tests.sh` passed authentication plus booking/replay/403/pagination/time-zone contracts; `PW_CHANNEL=chrome npm run test:e2e` 2/2 passed; frontend build passed. Initial test-project compile exposed a transitive EF patch mismatch; fixed by directly pinning EF Relational 9.0.20 and regenerating lockfiles. CI now runs real MySQL and HTTP tests. M1 17dcf41 pushed, remote CI succeeded. M2 ready to commit; next M3 reschedule/cancel/version conflict.

## M3 — Reschedule, cancel and optimistic version checks

2026-09-24: ordered resource locks plus appointment lock/revalidation, self-overlap rescheduling, task reset, cancellation and slot release, actionable version conflict UI.

Evidence: `./scripts/test.sh` 11/11 passed (new A05–A08/A12 tests, all M2 regressions). Injected exception after old-claim deletion restores old time/state/version/claims and leaves no extra audit, Outbox or idempotency result. Controlled stale pre-read test moves to another resource while the first request waits and correctly rejects the stale command. HTTP test passes mutation role restrictions, stale version, reschedule and cancel. Frontend build passed. Browser test covers create → resource change → cancel. Initial browser test selectors were tightened to the native combobox role. M2 097b42f pushed, remote CI succeeded.

An IDE-generated `ClinicFlow .sln` appeared during development; preserved as a local user file, excluded from this feature commit.

## M4 — Prerequisites and confirmation

2026-09-24: TaskOperator completes prerequisites; Scheduler confirms only when all current tasks are complete; all operations share the appointment version boundary. Reschedule resets live tasks while audit retains completion details. Detail deep links use `?appointment=<id>`.

Evidence: `./scripts/test.sh` 13/13 passed, including A09 task/confirm/reset/terminal-state rules and A08 concurrent task versus cancellation; HTTP A09/A18 role matrix passed. Browser A21 passed create → reject premature confirm → switch to TaskOperator → complete both tasks → switch to Scheduler → confirm → reschedule/reset → cancel. Three existing browser tests passed. The first A21 attempt navigated before login finished; the test now waits for the authenticated workspace before navigation. Build passed. M3 df9bdbc pushed and remote CI succeeded. Next M5 integration worker/receiver/lease/failure UI.

## M5 — Durable external synchronization

2026-09-24: scoped Outbox worker, persistent attempts, token/expiry guarded leases, bounded retry with jitter, permanent-error classification, admin idempotent retry and sync UI. Mock receiver persists receipts and monotonic snapshots in a separate SQLite volume.

Evidence: 18 distinct xUnit/MySQL tests passed; isolated receiver contract passed A14/A15 (save then disconnect, process restart, duplicate receipt, delayed old snapshot); real `tests/http-integration.mjs` passed A13 by stopping the Docker receiver, creating a local booking, observing persisted retry error, restarting the receiver, and verifying same MessageId and one receipt. Admin browser queue/history passed. Frontend build passed. A16 tests use two independent DB connections and an injected clock to reclaim a lease and reject stale completion. A17 proves five attempts, Failed, same-ID manual retry and retained history. All fault modes are disabled in normal Compose.

M4 b6f1547 pushed, remote CI succeeded. M5 verified and ready to commit. Next M6 bounded FHIR R4 read adapter.

## M6 — FHIR R4 read adapter

2026-09-24: fixed HL7 FHIR 4.0.1, bounded Patient/Appointment reads, CapabilityStatement, OperationOutcome errors, UTC instants and appointment version headers. Custom Outbox remains a separate protocol. Limits and official sources are documented in docs/fhir-r4.md.

Evidence: FhirTests 4/4 passed (all three state mappings and Patient fields); HTTP A20 passed metadata/read/auth/invalid id/not found/unsupported query/write. M5 99d8a70 pushed, remote CI succeeded. M6 ready to commit; next M7 isolated labs and RCA.

## M7 — Isolated failure labs

2026-09-24 Asia/Shanghai (evidence timestamps use UTC). L1 normal and fixed runs deterministically produced [1,1]/last-write-wins versus [1,0]/version conflict. L2 response loss produced two side effects without deduplication versus one with it. L3 ran 100,000 generated rows, resource=42, offset=5, limit=50, five query samples per variant: median 9.576ms before and 0.999ms after; plan changes from 100,000-row table scan/sort to covering index range scan; ordered IDs identical. Hardware/runtime details and full plans retained in docs/evidence. No production benefit claims.

L1/L3 use only clinicflow_labs; L2 uses temporary SQLite and a loopback server. Each rerun resets its own experiment. Task instructions and separate answer document added, plus RCA/English defect update and UI entry. Normal xUnit suite rerun after labs (22 tests) and frontend build passed. M6 e0c7024 pushed, remote CI succeeded. Next M8 migration/backup drill, packaging, learning materials and complete acceptance review.


## M8a — Packaging, upgrades and learning delivery

2026-09-24: multi-stage pinned container, same-origin bundled React UI, generated authenticated OpenAPI, request-header documentation, bounded pagination, readable C#/TypeScript formatting, keyboard dialog focus/Escape handling, desktop/mobile verification, architecture/Java reading guide/interview/independent task materials and acceptance map.

Actual upgrade drill retained appointment identity and all six record counts, then restored an old-schema mysqldump backup. The first generated migration incorrectly dropped an FK-supporting index; it was corrected to retain the old index and add the new one. Full rerun and EF pending-model check passed (details in upgrade-checklist.md).

Verification: locked .NET restore; 23/23 xUnit tests; receiver response-loss/restart contract; full HTTP suite including generated OpenAPI; actual Docker outage/recovery; 6/6 browser tests against http://127.0.0.1:5080 bundled container (including complete role lifecycle and mobile keyboard interaction). Docker image build and scripts/start.sh succeeded. Desktop and 390px mobile screenshots inspected. No public deployment. Expanded CI now includes browser/labs/upgrade verification and a portable artifact. M7 dbeff8f remote CI confirmed success. Next: clean checkout with fresh volumes and final CI result.


## M8b — Clean bootstrap regression and final acceptance

Fresh clone testing found a MySQL initialization readiness race. The socket ping was replaced by a TCP application-user query; disposable volumes were removed and the complete start command passed from empty volumes. All HTTP checks and 6 browser workflows passed on the clean container, and the added sync-failure/network-retry UI test passed separately. A new loopback HTTP proxy test proves an actual API response disconnect after success still replays one business effect. CI now repeats fresh-volume container startup in its own job. Details: docs/evidence/clean-start.md.

A01–A23 have executable evidence in docs/acceptance.md. M8a e80c473 is pushed and its full CI succeeded (run 35961883622), including a portable artifact. Final readiness/test/doc changes are verified locally and ready for commit; the live README CI badge and GitHub run for the final commit carry its remote outcome. The user-created `ClinicFlow .sln` is preserved, unmodified and uncommitted. Completed remains the user's independent exercise.
