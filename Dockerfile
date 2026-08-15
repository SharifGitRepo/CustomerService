# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files first (Docker cache optimization)
COPY src/CustomerService.API/CustomerService.API.csproj src/CustomerService.API/
COPY src/CustomerService.Application/*.csproj src/CustomerService.Application/
COPY src/CustomerService.Domain/*.csproj src/CustomerService.Domain/
COPY src/CustomerService.Infrastructure/*.csproj src/CustomerService.Infrastructure/

# Restore dependencies
RUN dotnet restore src/CustomerService.API/CustomerService.API.csproj

# Copy everything else
COPY . .

# Build
WORKDIR /src
RUN dotnet build src/CustomerService.API/CustomerService.API.csproj -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish src/CustomerService.API/CustomerService.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CustomerService.API.dll"]
