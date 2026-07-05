# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY NuGet.config ./
COPY global.json ./
COPY PawConnect.csproj ./
RUN dotnet restore PawConnect.csproj

COPY . .
RUN dotnet publish PawConnect.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
ARG APP_UID=1654

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

RUN mkdir -p /app/wwwroot/uploads && chown -R ${APP_UID}:0 /app
USER ${APP_UID}

ENTRYPOINT ["dotnet", "PawConnect.dll"]
