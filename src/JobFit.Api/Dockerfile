FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/JobFit.ServiceDefaults/JobFit.ServiceDefaults.csproj src/JobFit.ServiceDefaults/
COPY src/JobFit.Api/JobFit.Api.csproj src/JobFit.Api/
RUN dotnet restore src/JobFit.Api/JobFit.Api.csproj

COPY src/JobFit.ServiceDefaults/ src/JobFit.ServiceDefaults/
COPY src/JobFit.Api/ src/JobFit.Api/
RUN dotnet publish src/JobFit.Api/JobFit.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/alive || exit 1

ENTRYPOINT ["dotnet", "JobFit.Api.dll"]
