FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
COPY backend/ backend/
COPY tests/RewriteChecks/ tests/RewriteChecks/
RUN dotnet build tests/RewriteChecks/RewriteChecks.csproj -c Release
ENTRYPOINT ["dotnet", "run", "--project", "tests/RewriteChecks/RewriteChecks.csproj", "--no-build", "-c", "Release"]
