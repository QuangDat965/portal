# Team Notes API + built React SPA (served from wwwroot by the same process).
# Build context must be demo-apps/ so shared/PortalSso is visible:
#   docker build -f notes/api/Dockerfile -t portal-notes .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY shared/PortalSso/PortalSso.csproj shared/PortalSso/
COPY notes/api/Notes.Api.csproj notes/api/
RUN dotnet restore notes/api/Notes.Api.csproj

COPY shared/PortalSso/ shared/PortalSso/
COPY notes/api/ notes/api/
RUN dotnet publish notes/api/Notes.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Notes.Api.dll"]
