FROM node:24-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src/backend
COPY backend/*.csproj ./
RUN dotnet restore
COPY backend/ ./
RUN dotnet publish -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app/.playwright ./.playwright
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN printf 'Acquire::http::Timeout "30"; Acquire::https::Timeout "30"; Acquire::Retries "2";\n' > /etc/apt/apt.conf.d/99sammicrawl-timeouts \
    && sed -i 's|http://archive.ubuntu.com/ubuntu|https://archive.ubuntu.com/ubuntu|g; s|http://security.ubuntu.com/ubuntu|https://security.ubuntu.com/ubuntu|g' /etc/apt/sources.list.d/ubuntu.sources \
    && .playwright/node/linux-x64/node .playwright/package/cli.js install --with-deps --only-shell chromium
COPY --from=backend /app ./
COPY --from=frontend /src/frontend/dist ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "SammiCrawl.Backend.dll"]
