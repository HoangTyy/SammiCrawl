# Crawl global lock checks

From repository root, run an isolated fixture stack (no production .env or published ports):

```powershell
docker compose -p sammicrawl-lock-checks -f tests/CrawlLockChecks/compose.yaml up --build --abort-on-container-exit --exit-code-from checks
docker compose -p sammicrawl-lock-checks -f tests/CrawlLockChecks/compose.yaml down -v
```

Uses production CrawlGlobalLock/Npgsql sessions and two OS processes; tests release and killed-session cancellation. Runs two real FacebookCrawlWorker instances against fixture PostgreSQL with already-captured valid articles, so the completion path never opens Playwright, Facebook or Ollama. Asserts another node is not paused on startup and tasks remain pending behind the shared lock. Also covers orphan policies and persisted identity. No user data is read or modified. SQL Server compatibility branch is not covered by this PostgreSQL suite.

This proves coordination in the fixture, not Facebook extraction quality. All deployed crawler machines must use the new protocol. Legacy workers cannot be forced to honor a lock they do not acquire.
