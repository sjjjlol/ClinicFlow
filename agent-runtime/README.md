# ClinicFlow Pi runtime

`@earendil-works/pi-agent-core` 0.87.1 owns the Agent transcript and sequential model/tool loop. This is the maintained package formerly named `@mariozechner/pi-agent-core`; both Pi packages are pinned in package-lock.json.

ASP.NET starts `worker.mjs` for one bounded turn. UTF-8 JSON lines over private stdin/stdout carry `init`, model requests, text deltas, tool requests, host responses and final history. There is no network listener. The child receives only PATH; Kimi keys and database credentials stay in ASP.NET. `runtime.mjs` adapts the Pi transcript to the existing model protocol, so the same server-side model adapter handles Kimi SSE and errors. Only `list_catalog` and `search_slots` are executable tools. Booking remains an explicit authenticated confirmation request to the existing scheduling transaction.

Install: `npm ci`. Test: `npm test`. Node 24.13.1 is bundled in the application Docker image. A portable .NET deployment needs Node on PATH and this directory, including installed dependencies, beside the published application. Optional backend settings `Agent__NodePath` and `Agent__RuntimePath` override the executable and worker path.

One process per turn keeps credentials and lifetimes separate. ASP.NET limits active Pi processes to four, tool calls to eight, conflict searches to two and a turn to 90 seconds. Cancellation kills the process tree and invalidates incomplete candidate results. Registered users' patient binding and all query constraints are checked in ASP.NET, independently of Pi or model text. Sessions remain process-local with the existing 30-minute expiry.

Reference: [official Pi Agent Core](https://github.com/earendil-works/pi/tree/main/packages/agent).
