# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer): copy only project files.
COPY Directory.Build.props .
COPY src/KaamLink.Api/KaamLink.Api.csproj src/KaamLink.Api/
COPY src/KaamLink.Application/KaamLink.Application.csproj src/KaamLink.Application/
COPY src/KaamLink.Domain/KaamLink.Domain.csproj src/KaamLink.Domain/
COPY src/KaamLink.Infrastructure/KaamLink.Infrastructure.csproj src/KaamLink.Infrastructure/
RUN dotnet restore src/KaamLink.Api/KaamLink.Api.csproj

# Build + publish.
COPY src/ src/
RUN dotnet publish src/KaamLink.Api/KaamLink.Api.csproj -c Release -o /app/publish --no-restore

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Debian-based image (ICU present — required for correct multilingual culture handling).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run as the built-in non-root user.
USER $APP_UID

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "KaamLink.Api.dll"]
