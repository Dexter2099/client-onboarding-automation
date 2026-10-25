FROM node:24-alpine@sha256:44fce696f38ba8c987c4db89954051000bbd05ffd8d8659ba15ed4a8602e937f AS frontend
WORKDIR /build
COPY package.json package-lock.json ./
RUN npm ci
COPY index.html tsconfig.json vite.config.ts playwright.config.ts ./
COPY preview/content-snare/ preview/content-snare/
COPY public/ public/
COPY src/OnboardFlow.Api/ClientRequests/demo.json src/OnboardFlow.Api/ClientRequests/demo.json
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:230c3fbae0ceedc70bca04eed2bfb5694e8b248cadbb81010f41b7a993601f55 AS backend
WORKDIR /build
COPY global.json ./
COPY src/OnboardFlow.Api/ src/OnboardFlow.Api/
COPY --from=frontend /build/src/OnboardFlow.Api/wwwroot/ src/OnboardFlow.Api/wwwroot/
RUN dotnet publish src/OnboardFlow.Api/OnboardFlow.Api.csproj \
    -c Release -o /publish --self-contained false -p:UseAppHost=false -p:RestoreLockedMode=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:819bd3649d8bf53f35527c68d62e17b348ef6f24ae73073f497f00a878348062
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimit=0x10000000 \
    HOME=/data
RUN mkdir -p /data && chown app:app /data
COPY --from=backend /publish/ ./
COPY LICENSE THIRD_PARTY_NOTICES.md ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OnboardFlow.Api.dll"]
