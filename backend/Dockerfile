FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Portal.slnx ./
COPY src/Portal.Domain/Portal.Domain.csproj src/Portal.Domain/
COPY src/Portal.Application/Portal.Application.csproj src/Portal.Application/
COPY src/Portal.Infrastructure/Portal.Infrastructure.csproj src/Portal.Infrastructure/
COPY src/Portal.Api/Portal.Api.csproj src/Portal.Api/
COPY tests/Portal.Tests/Portal.Tests.csproj tests/Portal.Tests/
RUN dotnet restore Portal.slnx

COPY . .
RUN dotnet publish src/Portal.Api/Portal.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

RUN mkdir -p /data/keys && chown -R app:app /app /data
USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "Portal.Api.dll"]
