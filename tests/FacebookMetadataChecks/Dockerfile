FROM mcr.microsoft.com/dotnet/sdk:10.0 AS checks
WORKDIR /src
COPY backend/ backend/
COPY tests/FacebookMetadataChecks/ tests/FacebookMetadataChecks/
RUN dotnet publish tests/FacebookMetadataChecks/FacebookMetadataChecks.csproj -c Release -o /checks
FROM sammicrawl-app:latest
WORKDIR /checks
COPY --from=checks /checks ./
ENTRYPOINT ["dotnet", "/checks/FacebookMetadataChecks.dll"]
