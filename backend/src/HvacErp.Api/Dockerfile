# Build context is expected to be `backend/` (all 4 projects, for the .csproj project references).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HvacErp.sln ./
COPY src/HvacErp.Domain/HvacErp.Domain.csproj src/HvacErp.Domain/
COPY src/HvacErp.Application/HvacErp.Application.csproj src/HvacErp.Application/
COPY src/HvacErp.Infrastructure/HvacErp.Infrastructure.csproj src/HvacErp.Infrastructure/
COPY src/HvacErp.Api/HvacErp.Api.csproj src/HvacErp.Api/
RUN dotnet restore src/HvacErp.Api/HvacErp.Api.csproj

COPY . .
RUN dotnet publish src/HvacErp.Api/HvacErp.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render (and most PaaS Docker hosts) inject the port to listen on via $PORT at container
# start, not build time — bind to it with a fallback so `docker run` still works locally.
ENV ASPNETCORE_ENVIRONMENT=Production \
	DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "dotnet HvacErp.Api.dll --urls http://+:${PORT:-8080}"]
