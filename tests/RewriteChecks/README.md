# Rewrite regression checks

Runs the real ContentService and AiRunner against an in-memory repository and a loopback HTTP gateway fixture. No real database, AI, Facebook, or user media is accessed. Checks caption + image delivery in a single rewrite request, no OCR confirmation prerequisite, media/history preservation, unchanged output rejection, missing image rejection, text-only rewrite, repost, and gate release after errors. Fixture responses do not establish AI writing/vision quality.

```powershell
docker build -f tests/RewriteChecks/Dockerfile -t sammicrawl-rewrite-checks .
docker run --rm sammicrawl-rewrite-checks
```
