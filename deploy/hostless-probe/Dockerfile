FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:230c3fbae0ceedc70bca04eed2bfb5694e8b248cadbb81010f41b7a993601f55 AS build
WORKDIR /build
COPY global.json ./
COPY deploy/hostless-probe/ ./probe/
RUN dotnet publish probe/HostlessProbe.csproj -c Release -o /publish --self-contained false -p:UseAppHost=false -p:RestoreLockedMode=true
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:819bd3649d8bf53f35527c68d62e17b348ef6f24ae73073f497f00a878348062
WORKDIR /app
COPY --from=build /publish/ ./
COPY public/release.json ./release.json
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "HostlessProbe.dll"]
