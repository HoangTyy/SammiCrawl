FROM mcr.microsoft.com/dotnet/sdk:10.0 AS checks
WORKDIR /src
COPY backend/*.csproj backend/
COPY tests/ShareChecks/*.csproj tests/ShareChecks/
RUN dotnet restore tests/ShareChecks/ShareChecks.csproj
COPY backend/ backend/
COPY tests/ShareChecks/ tests/ShareChecks/
RUN dotnet publish tests/ShareChecks/ShareChecks.csproj -c Release --no-restore -o /checks
FROM sammicrawl-app:latest
WORKDIR /checks
COPY --from=checks /checks ./
ENTRYPOINT ["dotnet", "/checks/ShareChecks.dll"]
