# Use the official .NET SDK image for build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all files (including solution and projects)
COPY . .

# Restore all dependencies
RUN dotnet restore MenloProject.sln

# Publish the API project
RUN dotnet publish FileService.Api/FileService.Api.csproj -c Release -o /app/publish --no-restore

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port (matches launchSettings.json, default 5037)
EXPOSE 5037

# Set environment (optional, can be overridden)
ENV ASPNETCORE_URLS=http://+:5037
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "FileService.Api.dll"]

