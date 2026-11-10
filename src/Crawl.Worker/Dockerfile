# Crawl.Worker — multi-stage image for local Compose and (later) ECR → ECS.
# Secrets come from env / task role, never baked into layers.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Crawl.sln ./
COPY src/Crawl.Domain/Crawl.Domain.csproj src/Crawl.Domain/
COPY src/Crawl.Infrastructure/Crawl.Infrastructure.csproj src/Crawl.Infrastructure/
COPY src/Crawl.Worker/Crawl.Worker.csproj src/Crawl.Worker/
RUN dotnet restore src/Crawl.Worker/Crawl.Worker.csproj
COPY src/Crawl.Domain/ src/Crawl.Domain/
COPY src/Crawl.Infrastructure/ src/Crawl.Infrastructure/
COPY src/Crawl.Worker/ src/Crawl.Worker/
RUN dotnet publish src/Crawl.Worker/Crawl.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8081 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8081
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Crawl.Worker.dll"]
