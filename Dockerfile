# ===============================
# BUILD
# ===============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Tüm projeleri kopyala
COPY . .

# Restore solution
RUN dotnet restore Deepfake.sln

# Publish API project (DOĞRU PATH)
RUN dotnet publish "Deepfake.API/Deepfake.API.csproj" \
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

# DLL adı project adıyla aynı olmalı
ENTRYPOINT ["dotnet", "Deepfake.API.dll"]