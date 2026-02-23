# ===============================
# BUILD STAGE
# ===============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Solution + tüm projeleri kopyala
COPY . .

# Restore (solution üzerinden yapıyoruz)
RUN dotnet restore Deepfake.sln

# Publish sadece API projesi için
RUN dotnet publish Deepfake.API/Deepfake.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# ===============================
# RUNTIME STAGE
# ===============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "Deepfake.API.dll"]