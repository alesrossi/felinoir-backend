# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first so `restore` is cached unless dependencies change.
COPY src/Felinoir.Api/Felinoir.Api.csproj                 src/Felinoir.Api/
COPY src/Felinoir.Application/Felinoir.Application.csproj  src/Felinoir.Application/
COPY src/Felinoir.Domain/Felinoir.Domain.csproj           src/Felinoir.Domain/
COPY src/Felinoir.Infrastructure/Felinoir.Infrastructure.csproj src/Felinoir.Infrastructure/
RUN dotnet restore src/Felinoir.Api/Felinoir.Api.csproj

# Copy the rest of the source and publish a self-contained framework-dependent app.
COPY . .
RUN dotnet publish src/Felinoir.Api/Felinoir.Api.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Static cinema metadata (open-air flag, coordinates) read at runtime by the Felix corpus.
COPY cinemas.yml ./cinemas.yml

# DATABASE_URL and GEMINI_API_KEY are supplied at runtime, not baked in.
ENV PORT=3001
EXPOSE 3001
ENTRYPOINT ["dotnet", "Felinoir.Api.dll"]
