FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Crawl.sln ./
COPY src/Crawl.Domain/Crawl.Domain.csproj src/Crawl.Domain/
COPY src/Crawl.Infrastructure/Crawl.Infrastructure.csproj src/Crawl.Infrastructure/
COPY src/Crawl.Api/Crawl.Api.csproj src/Crawl.Api/
RUN dotnet restore src/Crawl.Api/Crawl.Api.csproj
COPY src/Crawl.Domain/ src/Crawl.Domain/
COPY src/Crawl.Infrastructure/ src/Crawl.Infrastructure/
COPY src/Crawl.Api/ src/Crawl.Api/
RUN dotnet publish src/Crawl.Api/Crawl.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Crawl.Api.dll"]

