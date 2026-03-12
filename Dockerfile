# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project files and restore
COPY src/CashChangerSimulator.Core/CashChangerSimulator.Core.csproj src/CashChangerSimulator.Core/
COPY src/CashChangerSimulator.UI.Api/CashChangerSimulator.UI.Api.csproj src/CashChangerSimulator.UI.Api/
RUN dotnet restore src/CashChangerSimulator.UI.Api/CashChangerSimulator.UI.Api.csproj

# Copy the rest and publish
COPY src/CashChangerSimulator.Core/ src/CashChangerSimulator.Core/
COPY src/CashChangerSimulator.UI.Api/ src/CashChangerSimulator.UI.Api/
COPY config.toml .

WORKDIR /source/src/CashChangerSimulator.UI.Api
RUN dotnet publish -c Release -o /app --no-restore

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Expose port (Cloud Run defaults to 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CashChangerSimulator.UI.Api.dll"]
