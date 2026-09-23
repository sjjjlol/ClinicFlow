# ADR 003 — Slot claims, locking and request idempotency

Each 15-minute interval in [start,end) occupies one SlotClaim. The composite primary key (ResourceId, SlotStartUtc) is the final uniqueness guard. Pending and Confirmed occupy slots; Cancelled releases them. Max duration is 4 hours; timestamps are normalized to UTC and rendered in Asia/Shanghai.

Create transaction at READ COMMITTED: insert/lock idempotency row → lock resource parents in ascending ID order → check slots → save appointment/claims/tasks/audit/Outbox/idempotency response → commit. A key is SHA256(subject, operation, caller key); fingerprint is SHA256(typed, UTC-normalized JSON). Concurrent identical keys serialize through the unique row. Successful replay returns the saved response before inspecting current business version. Rollbacks remove provisional records. Records are retained indefinitely in this learning version.

No HTTP runs inside a database transaction. MySQL deadlock, lock timeout and unique conflicts map to 409; there is no hidden infinite retry. The parent lock deliberately sacrifices per-resource throughput for an easily explained protocol. EF's command timeout bounds waits. No operation takes a resource lock after taking an appointment lock. Future mutations must preserve this order and revalidate any pre-read resource/version.

Tests use a separate *_tests database, independent DbContexts/connections and explicit TaskCompletionSource barriers. Production has only a no-op transaction probe; fault injection requires a test-supplied object. Tests assert persistent row counts, not just response status.

Java reading: BeginTransactionAsync is explicit, not annotation-driven; await using ensures rollback/disposal on exceptions. One DbContext per request is similar to a persistence context, not a shared thread-safe repository. EF tracking does not replace the database locking protocol.
