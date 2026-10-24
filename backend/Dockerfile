# Production API image. Build context: backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
# ponytail: copies everything before restore, so any change re-restores; copy csproj files first if builds get slow
COPY . .
RUN dotnet publish src/App.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
# Non-root user built into the .NET images. Listens on 8080 by default.
USER $APP_UID
ENTRYPOINT ["dotnet", "App.Api.dll"]
