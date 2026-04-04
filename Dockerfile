# Multi-stage build for CsharpBbs
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY Bbs.Core/Bbs.Core.csproj Bbs.Core/
COPY Bbs.Terminals/Bbs.Terminals.csproj Bbs.Terminals/
COPY Bbs.Persistence/Bbs.Persistence.csproj Bbs.Persistence/
COPY Bbs.Tenants/Bbs.Tenants.csproj Bbs.Tenants/
COPY Bbs.Petsciiator/Bbs.Petsciiator.csproj Bbs.Petsciiator/
COPY Bbs.Server/Bbs.Server.csproj Bbs.Server/

# Restore
RUN dotnet restore Bbs.Core/Bbs.Core.csproj
RUN dotnet restore Bbs.Terminals/Bbs.Terminals.csproj
RUN dotnet restore Bbs.Persistence/Bbs.Persistence.csproj
RUN dotnet restore Bbs.Tenants/Bbs.Tenants.csproj
RUN dotnet restore Bbs.Petsciiator/Bbs.Petsciiator.csproj
RUN dotnet restore Bbs.Server/Bbs.Server.csproj

# Copy source
COPY . .

# Build dependencies first (Bbs.Server references Debug DLLs)
RUN dotnet build Bbs.Core/Bbs.Core.csproj -c Debug --no-restore
RUN dotnet build Bbs.Terminals/Bbs.Terminals.csproj -c Debug --no-restore
RUN dotnet build Bbs.Persistence/Bbs.Persistence.csproj -c Debug --no-restore
RUN dotnet build Bbs.Petsciiator/Bbs.Petsciiator.csproj -c Debug --no-restore
RUN dotnet build Bbs.Tenants/Bbs.Tenants.csproj -c Debug --no-restore
RUN dotnet build Bbs.Server/Bbs.Server.csproj -c Debug --no-restore -o /app/build
RUN cp -a Bbs.Petsciiator/bin/Debug/net10.0/. /app/build/ \
    && cp -a Bbs.Tenants/bin/Debug/net10.0/. /app/build/ \
    && cp -a Bbs.Terminals/bin/Debug/net10.0/. /app/build/ \
    && cp -a Bbs.Core/bin/Debug/net10.0/. /app/build/ \
    && cp -a Bbs.Persistence/bin/Debug/net10.0/. /app/build/

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends frotz && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/build .

EXPOSE 6510
EXPOSE 9090

ENTRYPOINT ["dotnet", "Bbs.Server.dll"]
CMD ["--bbs", "StdChoice:6510", "-s", "9090", "-t", "3600000"]


