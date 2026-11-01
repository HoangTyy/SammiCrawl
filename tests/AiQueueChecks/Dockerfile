FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
COPY backend/ backend/
COPY tests/AiQueueChecks/ tests/AiQueueChecks/
RUN dotnet build tests/AiQueueChecks/AiQueueChecks.csproj -c Release
ENTRYPOINT ["dotnet", "run", "--project", "tests/AiQueueChecks/AiQueueChecks.csproj", "--no-build", "-c", "Release"]
