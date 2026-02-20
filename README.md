# 🕵️‍♂️ Deepfake Detection API 🚀

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](#)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Message_Broker-FF6600?logo=rabbitmq&logoColor=white)](#)
[![Supabase](https://img.shields.io/badge/Supabase-PostgreSQL_%26_Storage-3ECF8E?logo=supabase&logoColor=white)](#)
[![Clean Architecture](https://img.shields.io/badge/Architecture-Clean-success)](#)

Bu proje, yüklenen görsellerin yapay zeka ile manipüle edilip edilmediğini (Deepfake) tespit etmek için geliştirilmiş, **mikroservis mimarisine uygun** ve **asenkron çalışan** bir analiz platformunun ana omurgasıdır (Backend/Gateway).

Kullanıcı arayüzü ile Python tabanlı ağır AI analiz işçileri (Workers) arasında köprü görevi gören bu API, sistemin kilitlenmeden binlerce isteği yönetebilmesini sağlar.

---

## 🏗️ Teknolojiler ve Mimari

Proje, kurumsal standartlara ve SOLID prensiplerine uygun olarak **Clean Architecture (Temiz Mimari)** ile inşa edilmiştir:

* 🧠 **Çekirdek:** .NET 9 (ASP.NET Core Web API)
* 🗄️ **Veritabanı:** PostgreSQL (Supabase) & Entity Framework Core 9
* ☁️ **Depolama (Storage):** Supabase Storage (Service Role ile güvenli yükleme)
* 🐇 **Mesaj Kuyruğu (Message Broker):** RabbitMQ (Asenkron görev yönetimi için)
* 🛡️ **Güvenlik:** JWT (JSON Web Token) tabanlı IP doğrulama kalkanı
* 🚦 **Trafik Kontrolü:** .NET Rate Limiter
* 🖼️ **Görüntü İşleme:** SixLabors.ImageSharp (Anında optimize Thumbnail üretimi)

---

## 📁 Proje Yapısı (N-Tier Clean Architecture)

Sistem, bağımlılıkların dıştan içe doğru aktığı 4 ana katmandan oluşur:

1. 🎯 **`Deepfake.Domain`:** Çekirdek varlıklar (Entities), Enum'lar ve Frontend ile haberleşen DTO'lar. Hiçbir dış bağımlılığı yoktur.
2. ⚙️ **`Deepfake.Application`:** İş kuralları, sabitler (Constants) ve altyapıya verilen arayüz (Interface) sözleşmeleri.
3. 🔌 **`Deepfake.Infrastructure`:** Veritabanı (AppDbContext), Repository implementasyonları, Supabase ve RabbitMQ entegrasyonları.
4. 🚀 **`Deepfake.API`:** Controller'lar, Middleware'ler ve RabbitMQ kuyruğunu arka planda 7/24 dinleyen `BackgroundWorker` servisimiz.

---

## ⚙️ Kurulum ve Çalıştırma

1. Depoyu lokal ortamınıza klonlayın.
2. `Deepfake.API` dizininde `appsettings.json` dosyasını oluşturup **Supabase**, **RabbitMQ** ve **JWT** anahtarlarınızı ekleyin.
3. Terminalden `dotnet restore` komutu ile paketleri yükleyin.
4. Arka planda RabbitMQ sunucunuzun çalıştığından emin olun.
5. `dotnet ef database update` ile veritabanı tablolarını Supabase üzerinde oluşturun.
6. `dotnet run` komutu ile projeyi ayağa kaldırın. (API ve Background Worker aynı anda çalışmaya başlayacaktır).

---

## 🏆 Başarımlarımız (Neleri Tamamladık?)

- [x] Temel mimari (Clean Architecture) ve Supabase entegrasyonunun kurulması.
- [x] JWT, Middleware ve Rate Limiting ile API güvenliğinin sağlanması.
- [x] Orijinal görsel yükleme ve ImageSharp ile anında Thumbnail üretimi.
- [x] RabbitMQ entegrasyonu ile HTTP bağımlılığının kırılması ve asenkron analiz kuyruğuna (`analysis_queue`) geçiş.
- [x] Python AI servisi ile tam uyumlu çalışan .NET Background Listener (`result_queue`) yazılması.
- [x] Controller'ların Repository Pattern ve DTO'lar ile tamamen soyutlanması.

---

## 🚧 Yaklaşan Görevler (Teknik Rapor Uyumluluğu)

Projenin resmi Bitirme Teknik Raporu ile %100 uyumlu çalışması için yapılacak son ince ayarlar:

- [ ] **Geçmiş Analizler Endpoint'i:** Sayfalama (Pagination) destekli `GET /api/analysis/history` uç noktasının yazılması[cite: 211, 213, 230].
- [ ] **Gelişmiş Dosya Doğrulama:** Görsel yüklemede sadece uzantı kontrolü değil, *Magic Numbers (Dosya İmzası)* analizi ile güvenlik doğrulaması yapılması[cite: 309].
- [ ] **Hassas Rate Limiting:** Mevcut "Dakikada 5 İstek" kuralına ek olarak, raporda belirtilen "Saatte maksimum 20 talep" kuralının IP kalkanına entegre edilmesi[cite: 305].
- [ ] **Sonuç (GET) Şeması Revizyonu:** Analiz sonucu uç noktasının HTTP 202 (Processing) ve 500 (Failed) statü kodları [cite: 198, 204] [cite_start]ile *exifAnalysis* nested JSON formatına [cite: 185] uyarlanması.
- [ ] **Thumbnail Boyutu:** Küçük resim üretim boyutunun 256x256 yerine rapor standardı olan 150x150 piksel olarak güncellenmesi[cite: 231].