# QuickKirana: one container serving the API and the built React app on one origin (ADR 0005).
# Build:  docker build -t quickkirana .
# Run:    docker run -p 8080:8080 -e ConnectionStrings__GroceryDb="..." quickkirana

# ---------- 1. Web app (React + Vite) ----------
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
ARG VITE_STORE_NAME=QuickKirana
ENV VITE_STORE_NAME=$VITE_STORE_NAME
RUN npm run build

# ---------- 2. API (.NET) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
# Project files first, so the NuGet restore layer is cached until dependencies change.
# .editorconfig belongs with them: it marks EF migrations as generated code, and without it the
# analyzers (warnings are errors) fail the publish on them.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/GroceryStore.Api/GroceryStore.Api.csproj src/GroceryStore.Api/
RUN dotnet restore src/GroceryStore.Api/GroceryStore.Api.csproj
COPY src/ src/
RUN dotnet publish src/GroceryStore.Api/GroceryStore.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false

# ---------- 3. Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
EXPOSE 8080
# The image's built-in non-root user: a compromised process can't modify the app or the system.
USER $APP_UID
ENTRYPOINT ["dotnet", "GroceryStore.Api.dll"]
