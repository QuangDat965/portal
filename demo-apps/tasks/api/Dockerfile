# Taskboard API + built React SPA (served from wwwroot by the same process).
# Build context must be demo-apps/ so shared/PortalSso is visible:
#   docker build -f tasks/api/Dockerfile -t portal-tasks .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/PortalSso/PortalSso.csproj shared/PortalSso/
COPY tasks/api/Tasks.Api.csproj tasks/api/
RUN dotnet restore tasks/api/Tasks.Api.csproj

COPY shared/PortalSso/ shared/PortalSso/
COPY tasks/api/ tasks/api/
RUN dotnet publish tasks/api/Tasks.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tasks.Api.dll"]
