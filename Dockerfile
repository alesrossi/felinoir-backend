# syntax=docker/dockerfile:1

# Two images from one build:
#   docker build .                   -> the API (runtime, the last stage, the default)
#   docker build --target migrator . -> the one-shot schema migrator (ADR 0001)

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first so `restore` is cached unless dependencies change.
COPY src/Felinoir.Api/Felinoir.Api.csproj                 src/Felinoir.Api/
COPY src/Felinoir.Application/Felinoir.Application.csproj  src/Felinoir.Application/
COPY src/Felinoir.Domain/Felinoir.Domain.csproj           src/Felinoir.Domain/
COPY src/Felinoir.Infrastructure/Felinoir.Infrastructure.csproj src/Felinoir.Infrastructure/
COPY src/Felinoir.Migrator/Felinoir.Migrator.csproj       src/Felinoir.Migrator/
RUN dotnet restore src/Felinoir.Api/Felinoir.Api.csproj \
 && dotnet restore src/Felinoir.Migrator/Felinoir.Migrator.csproj

# Copy the rest of the source and publish both framework-dependent apps.
COPY . .
RUN dotnet publish src/Felinoir.Api/Felinoir.Api.csproj -c Release -o /app --no-restore \
 && dotnet publish src/Felinoir.Migrator/Felinoir.Migrator.csproj -c Release -o /migrator --no-restore

# ---- migrator ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS migrator
WORKDIR /app
COPY --from=build /migrator ./

# DATABASE_URL is supplied at runtime. Exits 0 once the schema is current, 1 on failure.
ENTRYPOINT ["dotnet", "Felinoir.Migrator.dll"]

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# DATABASE_URL and GEMINI_API_KEY are supplied at runtime, not baked in.
ENV PORT=3001
EXPOSE 3001
ENTRYPOINT ["dotnet", "Felinoir.Api.dll"]
