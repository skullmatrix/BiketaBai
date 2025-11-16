# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

# Copy project file and restore dependencies
COPY BiketaBai.csproj ./
RUN dotnet restore BiketaBai.csproj

# Copy everything else and build
COPY . ./
RUN dotnet publish BiketaBai.csproj -c Release -o out

# Use the ASP.NET Core runtime image for running
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Copy the published output from build stage
COPY --from=build /app/out .

# Expose port (Railway will override this with PORT env var)
EXPOSE 8080

# Set environment variable
ENV ASPNETCORE_URLS=http://+:8080

# Run the application
ENTRYPOINT ["dotnet", "BiketaBai.dll"]

