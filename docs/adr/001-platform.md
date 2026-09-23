# ADR 001 — Runtime and persistence

2026-09-23. Use .NET 10 LTS with Pomelo 9.0.0 / EF Core 9; Pomelo's published compatibility table supports .NET 8+ and EF 9, and explicitly tests MySQL 8.4. Do not pair this provider with EF 10. Patch packages and SDK are pinned in project/global files and dependency locks. MySQL 8.4.8 ARM64 is checked by pulling and running the official image. EF 9 is a transitional dependency: reassess the provider before its support ends; do not silently upgrade its major version.

Sources checked: https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql#compatibility and https://dotnet.microsoft.com/en-us/download/dotnet/10.0 .

Local SDK is installed under ~/.local/share/clinicflow-dotnet when no global SDK exists. No machine-wide PATH changes. Real MySQL is mandatory for transaction tests. EF handles ordinary tracking and migrations; locking statements use parameterized SQL within the same transaction. Java comparison: DbContext resembles a scoped JPA persistence context, but async operations on a single context must not overlap.
