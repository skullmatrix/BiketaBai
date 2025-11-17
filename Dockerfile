# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY BiketaBai.csproj .
RUN dotnet restore BiketaBai.csproj

# Copy everything else and build
COPY . .
RUN dotnet build BiketaBai.csproj -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish BiketaBai.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create necessary directories
RUN mkdir -p logs wwwroot/uploads/bikes wwwroot/uploads/id-documents wwwroot/uploads/profiles

# Copy published application
COPY --from=publish /app/publish .

# Set environment to Production
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port (Railway will set PORT env var)
EXPOSE 8080

# Start the application
ENTRYPOINT ["dotnet", "BiketaBai.dll"]

