# ── Build Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Project files first: restore is cached as long as no csproj changes
# Directory.Packages.props is required because the csproj files carry no Version attributes
# (central package management). Drop it here if the repository does not contain that file.
COPY PubQuizMaster.sln Directory.Packages.props ./
COPY PubQuizMaster.Core/PubQuizMaster.Core.csproj PubQuizMaster.Core/
COPY PubQuizMaster.Data/PubQuizMaster.Data.csproj PubQuizMaster.Data/
COPY PubQuizMaster.Services/PubQuizMaster.Services.csproj PubQuizMaster.Services/
COPY PubQuizMaster.Web/PubQuizMaster.Web.csproj PubQuizMaster.Web/
RUN dotnet restore PubQuizMaster.Web/PubQuizMaster.Web.csproj

COPY . .

RUN dotnet publish PubQuizMaster.Web/PubQuizMaster.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --no-self-contained

# ── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Data protection key ring. An empty named volume mounted here inherits these
# permissions from the image, so the non-root user can write the key files.
RUN mkdir -p /data/keys && chown -R $APP_UID:$APP_UID /data/keys

# Non-root user shipped with the aspnet image
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PubQuizMaster.Web.dll"]
