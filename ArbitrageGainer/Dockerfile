# Build stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Expose the port
EXPOSE 8080

# Set working directory
WORKDIR /source

# Copy and restore dependencies
COPY ./*.fsproj ./
RUN dotnet restore

# Copy source code and build the application
COPY . .
RUN dotnet publish -c release -o /app

# Create log directory in the build stage
RUN mkdir -p /source/Service/ApplicationService

# Copy the historicalData.txt file into the container
COPY historicalData.txt /app/historicalData.txt

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app

# Copy app files and ensure log directory is created in runtime
COPY --from=build /app .
RUN mkdir -p /source/Service/ApplicationService

# Start the application
ENTRYPOINT ["dotnet", "ArbitrageGainer.dll"]
