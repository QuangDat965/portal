# Build with demo-apps/ as the context.
FROM node:24-alpine AS web
WORKDIR /web
RUN corepack enable && corepack prepare pnpm@12.18.0 --activate
COPY exploding-kittens/web/package.json exploding-kittens/web/pnpm-lock.yaml exploding-kittens/web/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile --allow-build=esbuild
COPY exploding-kittens/web/ ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/PortalSso/PortalSso.csproj shared/PortalSso/
COPY exploding-kittens/api/ExplodingKittens.Api.csproj exploding-kittens/api/
RUN dotnet restore exploding-kittens/api/ExplodingKittens.Api.csproj
COPY shared/PortalSso/ shared/PortalSso/
COPY exploding-kittens/api/ exploding-kittens/api/
RUN dotnet publish exploding-kittens/api/ExplodingKittens.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app .
COPY --from=web /web/dist ./wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExplodingKittens.Api.dll"]
