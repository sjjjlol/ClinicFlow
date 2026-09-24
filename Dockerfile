FROM node:24.13.1-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS backend
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY backend/ backend/
RUN dotnet restore backend --locked-mode
RUN dotnet publish backend -c Release --no-restore -o /out
COPY --from=frontend /src/frontend/dist/ /out/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
WORKDIR /app
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=backend /out/ ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClinicFlow.dll"]
