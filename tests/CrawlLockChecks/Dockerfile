FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
COPY backend/ backend/
COPY tests/CrawlLockChecks/ tests/CrawlLockChecks/
RUN dotnet publish tests/CrawlLockChecks/CrawlLockChecks.csproj -c Release -o /checks
ENTRYPOINT ["dotnet","/checks/CrawlLockChecks.dll"]
