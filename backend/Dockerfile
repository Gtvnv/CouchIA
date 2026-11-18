# syntax=docker/dockerfile:1
# Contexto de build: a pasta backend/ (veja docker-compose.yml na raiz).

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restaura primeiro só com os .csproj, para aproveitar o cache de camadas.
COPY global.json ./
COPY src/CouchIA.Domain/CouchIA.Domain.csproj src/CouchIA.Domain/
COPY src/CouchIA.Application/CouchIA.Application.csproj src/CouchIA.Application/
COPY src/CouchIA.Infrastructure/CouchIA.Infrastructure.csproj src/CouchIA.Infrastructure/
COPY src/CouchIA.WebUI/CouchIA.WebUI.csproj src/CouchIA.WebUI/
RUN dotnet restore src/CouchIA.WebUI/CouchIA.WebUI.csproj

COPY src/ src/
# O catálogo (src/CouchIA.WebUI/Catalogo/catalogo.json) é copiado para a saída pelo csproj (Content).
RUN dotnet publish src/CouchIA.WebUI/CouchIA.WebUI.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# QuestPDF (exportação em PDF) precisa de fontconfig no Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
# Garante o catálogo no caminho padrão esperado pela API (Catalogo/catalogo.json).
COPY src/CouchIA.WebUI/Catalogo/catalogo.json ./Catalogo/catalogo.json

# Usuário sem privilégios (a imagem aspnet 8 já traz o usuário "app", UID 1654).
USER app
# A imagem aspnet 8 já escuta em http://+:8080 (ASPNETCORE_HTTP_PORTS=8080).
ENV DOTNET_EnableDiagnostics=0
EXPOSE 8080

ENTRYPOINT ["dotnet", "CouchIA.WebUI.dll"]
