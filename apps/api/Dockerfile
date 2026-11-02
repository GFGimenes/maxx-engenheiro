# syntax=docker/dockerfile:1
# Contexto: raiz do repositório (global.json e Directory.*.props ficam lá).
# A mesma imagem serve a API e o contêiner de migração (`migrate`).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauração em camada própria: só refaz quando um .csproj ou .props muda.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/Maxx.Domain/Maxx.Domain.csproj apps/api/src/Maxx.Domain/
COPY apps/api/src/Maxx.Application/Maxx.Application.csproj apps/api/src/Maxx.Application/
COPY apps/api/src/Maxx.Infrastructure/Maxx.Infrastructure.csproj apps/api/src/Maxx.Infrastructure/
COPY apps/api/src/Maxx.Api/Maxx.Api.csproj apps/api/src/Maxx.Api/
RUN dotnet restore apps/api/src/Maxx.Api/Maxx.Api.csproj

COPY .editorconfig ./
COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/Maxx.Api/Maxx.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Usuário sem privilégio que a imagem oficial já traz.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Maxx.Api.dll"]
