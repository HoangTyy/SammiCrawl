# Share checks

Build app image first, then:

```powershell
docker build -f tests/ShareChecks/Dockerfile -t sammicrawl-sharechecks .
docker run --rm --network none sammicrawl-sharechecks
```

Uses in-memory repository and routed Chromium DOM fixtures. No real Facebook, AI, database, cookies or publishing. Tests preserve original source/author, enforce explicit confirmation and publish role, deduplicate requests, cancel queued jobs, verify exact group and original preview before one submit, and distinguish moderation/no-permalink from published. Does not prove current Facebook DOM compatibility.
