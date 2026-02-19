# Deepfake Detection API 🕵️‍♂️🔍

Bu proje, yüklenen görsellerin manipüle edilip edilmediğini (Deepfake) tespit etmek için geliştirilmiş, mikroservis mimarisine uygun ve asenkron çalışan bir yapay zeka analiz platformunun **.NET 9 Backend** (Gateway) katmanıdır.

## 🚀 Teknolojiler ve Mimari

Proje, kurumsal standartlara uygun olarak **Clean Architecture** (Temiz Mimari) prensipleriyle inşa edilmiştir:

* **Framework:** .NET 9 (ASP.NET Core Web API)
* **Veritabanı:** PostgreSQL (Supabase)
* **ORM:** Entity Framework Core 9
* **Depolama (Storage):** Supabase Storage (Service Role ile güvenli yükleme)
* **Güvenlik:** JWT (JSON Web Token) tabanlı IP doğrulama kalkanı
* **Trafik Kontrolü:** .NET Rate Limiter (Dakikada 5 istek sınırı)

## 📁 Proje Yapısı (Clean Architecture)

* `Deepfake.Domain`: Çekirdek varlıklar (Entities), Enum'lar ve DTO'lar.
* `Deepfake.Application`: İş kuralları ve dış dünyaya açılan arayüzler (Interfaces).
* `Deepfake.Infrastructure`: Veritabanı (AppDbContext) ve dış servis (Supabase, HttpClient) entegrasyonları.
* `Deepfake.API`: Controller'lar, Middleware'ler ve sistemin giriş kapısı.

## ⚙️ Kurulum ve Çalıştırma

1.  Depoyu klonlayın.
2.  `Deepfake.API` dizinindeki `appsettings.json` dosyasını oluşturup Supabase ve JWT anahtarlarınızı ekleyin.
3.  Terminalden `dotnet restore` komutu ile paketleri yükleyin.
4.  `dotnet ef database update` ile veritabanı tablolarını Supabase üzerinde oluşturun.
5.  `dotnet run` komutu ile projeyi ayağa kaldırın.

## 🔄 Gelecek Adımlar (To-Do)

- [x] Temel mimari ve Supabase entegrasyonu.
- [x] JWT ve API güvenliği.
- [x] Görsel yükleme ve format/boyut denetimi.
- [ ] Python (FastAPI) AI analiz servisi ile haberleşme.
- [ ] RabbitMQ / Message Broker entegrasyonu ile asenkron analiz kuyruğu.