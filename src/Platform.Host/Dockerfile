FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY contracts/ ./contracts/
COPY src/Sdk/ ./src/Sdk/
COPY src/Core/ ./src/Core/
COPY src/MockData/ ./src/MockData/
COPY src/Platform.Host/ ./src/Platform.Host/

RUN dotnet publish src/Platform.Host/Platform.Host.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./
COPY contracts/ /app/contracts/

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Platform.Host.dll"]
