FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# expose the port on which you are running web server in your application
EXPOSE 8080


WORKDIR /source


# Copy fsproj and restore all dependencies
COPY ./*.fsproj ./
RUN dotnet restore

# Set create mode to log.txt
RUN mkdir -p /Service/ApplicationService
RUN chmod -R 755 /Service/ApplicationService


# Copy source code and build / publish app and libraries
COPY . .
RUN dotnet publish -c release -o /app


# Copy the historicalData.txt file into the container
COPY historicalData.txt /app/historicalData.txt

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "ArbitrageGainer.dll"]
