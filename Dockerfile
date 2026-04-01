# ===============================
# BUILD
# ===============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Tüm projeleri (sln, csprojlar ve kodlar) kopyala
COPY . .

# Bağımlılıkları geri yükle
RUN dotnet restore Deepfake.sln

# API projesini publish et
RUN dotnet publish "Deepfake.API/Deepfake.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore


# ===============================
# RUNTIME
# ===============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Derlenmiş dosyaları kopyala
COPY --from=build /app/publish .

# Railway için Port Ayarı (Genelde 80 veya Railway'in atadığı PORT kullanılır)
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

# DLL adı doğruluğunu kontrol edin
ENTRYPOINT ["dotnet", "Deepfake.API.dll"]