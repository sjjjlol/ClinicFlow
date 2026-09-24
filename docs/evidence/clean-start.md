# Clean-checkout startup evidence

Date: 2026-09-24. Source snapshot: e80c473 plus the readiness correction described below. Independent local clone with no .env, node_modules, bin/obj, or database data. Docker base/build layers were cached, as is normal for a repeatable container build; no developer build output was copied.

Command: `COMPOSE_PROJECT_NAME=clinicflow-clean APP_PORT=5088 DB_PORT=3318 MOCK_PORT=5098 ./scripts/start.sh`. The script generated new random credentials and new named MySQL/SQLite/DataProtection volumes. API migrations and seeds completed automatically; UI was served by the container on port 5088.

The first fresh-volume run caught a defect: socket-only mysqladmin ping became healthy against MySQL's temporary bootstrap server (port 0). ASP.NET tried TCP before the real server started and exited. Corrected Compose readiness to execute SELECT 1 over TCP using the application account and clinicflow database. Deleted only the disposable clinicflow-clean volumes, then ran the full startup again successfully.

On the corrected fresh deployment, all HTTP suites passed (three-role login, CSRF, booking/replay/conflict, reschedule/cancel, task/confirmation permissions, UTC, pagination/filter boundaries, FHIR errors and generated OpenAPI). Six browser flows passed against the bundled UI: login/logout, create/slot conflict, reschedule/cancel, full collaborative lifecycle, admin attempts, mobile/Escape focus behavior. A separate seventh controlled UI fixture verified sync failure/error/retry feedback and retained request identity across a network failure. Real lease/retry persistence and actual receiver downtime were verified by the MySQL and Docker tests, not inferred from that UI fixture.

A separate HTTP proxy test intentionally destroyed the client connection after the API committed a booking. Retrying with the same identity returned the exact original response; the database-backed detail still contained one audit and one Outbox effect.

Regression protection: CI now has a container-smoke job that uses the exact documented start command on fresh volumes, independently of the normal build/test job. The earlier e80c473 expanded CI run passed all backend/frontend/HTTP/receiver/lab/upgrade/browser steps and uploaded the portable artifact (run 35961883622).
