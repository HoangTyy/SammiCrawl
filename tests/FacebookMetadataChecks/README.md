# Facebook post metadata checks

Runs the production DOM extractor in real headless Chromium with local HTML fixtures, then the production metadata mapper. All browser network requests are blocked. No Facebook account, database, AI, `.env` or user media is read. Requires the locally built `sammicrawl-app:latest` image for its browser and system dependencies.

```powershell
docker build -f tests/FacebookMetadataChecks/Dockerfile -t sammicrawl-facebook-metadata-checks .
docker run --rm sammicrawl-facebook-metadata-checks
```

Tests cover exact/abbreviated/zero/absent counters in Vietnamese and English, reaction vs Like totals, caption/reply/hidden-counter isolation, mixed-post rejection, source timestamps vs comment timestamps, raw relative date labels, UTC mapping and malformed input. Passing fixtures do not establish compatibility with all current Facebook layouts. No GraphQL/private API or interactive reaction/comment control is used.
