# AI gateway queue checks

Run from repository root:

```powershell
docker build -f tests/AiQueueChecks/Dockerfile -t sammicrawl-ai-queue-checks .
docker run --rm sammicrawl-ai-queue-checks
```

Uses production queue/controllers/auth pipeline and the real AiRunner semaphore, with fake model operations and fake remote HTTP. Does not connect to PostgreSQL, Ollama or Facebook. Tests serialized mixed jobs from multiple clients, result polling, compatibility routes, admission limits, cancellation of queued work, retained-result expiry, shutdown, idempotent submissions, and no duplicate POST on lost response. Model loading/unloading itself is not run here; production AiRunner retains that responsibility.

Queue is process-local and volatile. See `REMOTE-AI-QUEUE.md` for deployment limits and client rollout order (gateway first, then clients).
