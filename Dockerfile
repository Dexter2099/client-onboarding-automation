FROM node:24-alpine AS frontend
WORKDIR /build
COPY package.json package-lock.json ./
RUN npm ci
COPY index.html tsconfig.json vite.config.ts playwright.config.ts ./
COPY src/web/ src/web/
COPY public/ public/
COPY tests/browser/ tests/browser/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend
WORKDIR /build
COPY global.json ./
COPY src/OnboardFlow.Api/ src/OnboardFlow.Api/
COPY --from=frontend /build/src/OnboardFlow.Api/wwwroot/ src/OnboardFlow.Api/wwwroot/
RUN dotnet publish src/OnboardFlow.Api/OnboardFlow.Api.csproj \
    -c Release -o /publish --self-contained false -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false \
    HOME=/data
RUN mkdir -p /data && chown app:app /data
COPY --from=backend /publish/ ./
COPY LICENSE THIRD_PARTY_NOTICES.md ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnboardFlow.Api.dll"]
