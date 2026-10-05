# syntax=docker/dockerfile:1

# ---------- Étape 1 : compilation (image SDK, ~900 Mo, jamais livrée) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1a. Uniquement ce qui influence la restauration NuGet → couche mise en cache
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY dotnet-tools.json ./
COPY src/SongVault.Domain/*.csproj         src/SongVault.Domain/
COPY src/SongVault.Application/*.csproj    src/SongVault.Application/
COPY src/SongVault.Infrastructure/*.csproj src/SongVault.Infrastructure/
COPY src/SongVault.Api/*.csproj            src/SongVault.Api/
RUN dotnet restore src/SongVault.Api/SongVault.Api.csproj

# 1b. Le code source (change souvent → couches suivantes seulement)
COPY src/ src/
RUN dotnet publish src/SongVault.Api/SongVault.Api.csproj -c Release -o /app/publish --no-restore

# ---------- Étape : bundle de migrations (réutilise la compilation de "build") ----------
FROM build AS bundle
RUN dotnet tool restore && \
    dotnet ef migrations bundle \
      --project src/SongVault.Infrastructure \
      --startup-project src/SongVault.Api \
      --configuration Release \
      -o /app/efbundle

# ---------- Étape : conteneur ponctuel qui applique les migrations ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
USER $APP_UID
# La chaîne vient de l'environnement (Compose), jamais de l'image
ENTRYPOINT ["sh", "-c", "./efbundle --connection \"$ConnectionStrings__SongVault\""]

# ---------- Étape finale : exécution (image runtime ASP.NET, ~230 Mo) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Dossier des fichiers uploadés, créé en root puis confié à l'utilisateur applicatif :
# un volume nommé VIDE monté ici héritera de ce propriétaire.
RUN mkdir -p /var/songvault/files && chown $APP_UID /var/songvault/files

COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SongVault.Api.dll"]