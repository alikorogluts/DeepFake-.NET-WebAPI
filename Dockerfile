# ===============================
# BUILD
# ===============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .

# Restore solution
RUN dotnet restore Deepfake.sln

# Publish API project directly (path önemli!!)
RUN dotnet publish "ASP.NET Core Web API/ASP.NET Core Web API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore


# ===============================
# RUNTIME
# ===============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "ASP.NET Core Web API.dll"]