FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
COPY backend/Infrastructure/ backend/Infrastructure/
COPY backend/Controllers/AuthController.cs backend/Controllers/ServiceResultExtensions.cs backend/Controllers/
COPY backend/Contracts/Credentials.cs backend/Contracts/ServiceResult.cs backend/Contracts/
COPY backend/Services/LiveUpdates.cs backend/Services/CrawlCapacity.cs backend/Services/CrawlEvaluationState.cs backend/Services/AiModelUnavailableException.cs backend/Services/AiGatewayException.cs backend/Services/AiGatewayClient.cs backend/Services/
COPY tests/SecurityChecks/ tests/SecurityChecks/
RUN dotnet restore tests/SecurityChecks/SecurityChecks.csproj
RUN dotnet build tests/SecurityChecks/SecurityChecks.csproj --no-restore -c Release
ENTRYPOINT ["dotnet", "run", "--project", "tests/SecurityChecks/SecurityChecks.csproj", "--no-build", "-c", "Release"]
