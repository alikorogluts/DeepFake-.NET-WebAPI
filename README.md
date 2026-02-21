# 🕵️‍♂️ Deepfake Detection API — Core Backend

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](#)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Event_Driven-FF6600?logo=rabbitmq&logoColor=white)](#)
[![Supabase](https://img.shields.io/badge/Supabase-PostgreSQL_%26_Storage-3ECF8E?logo=supabase&logoColor=white)](#)
[![Security](https://img.shields.io/badge/Security-Zero_Trust-DC143C?logo=shield&logoColor=white)](#)
[![Architecture](https://img.shields.io/badge/Architecture-Clean-22C55E?logo=blueprint&logoColor=white)](#)

> **Dijital ortamlardaki manipüle edilmiş (Deepfake) görselleri tespit etmek için geliştirilmiş, mikroservis tabanlı, olay güdümlü (Event-Driven) ve asenkron çalışan yüksek performanslı analiz platformunun ana omurgası.**

Kullanıcı arayüzleri (**Next.js / Flutter**) ile Python tabanlı ağır Yapay Zeka analiz işçileri (**Workers**) arasında güvenli bir köprü görevi gören bu API; sistemin darboğaza girmeden ve kilitlenmeden binlerce asenkron isteği yönetebilmesini sağlar.

---

## 📋 İçindekiler

- [Teknoloji Stack'i](#-teknoloji-stacki)
- [Sistem Mimarisi](#-sistem-mimarisi)
- [Asenkron İş Akışı](#-asenkron-i̇ş-akışı-polling-mimarisi)
- [API Dokümantasyonu](#-api-dokümantasyonu)
    - [1. Yetkilendirme](#-1-yetkilendirme-token-alma)
    - [2. Görsel Yükleme](#-2-görsel-yükleme-ve-analiz-başlatma)
    - [3. Sonuç Sorgulama](#-3-analiz-sonucunu-sorgulama-polling)
    - [4. Geçmiş Listeleme](#-4-geçmiş-analizleri-listeleme)
- [Tamamlanan Özellikler](#-tamamlanan-kurumsal-özellikler)
- [Yol Haritası](#-yol-haritası-upcoming-tasks)

---

## 🛠️ Teknoloji Stack'i

| Katman | Teknoloji | Açıklama |
|---|---|---|
| 🧠 **Çekirdek** | .NET 9 / ASP.NET Core | Web API altyapısı |
| 🗄️ **Veritabanı** | PostgreSQL (Supabase) | `AsNoTracking` ile optimize EF Core 9 |
| ☁️ **Depolama** | Supabase Storage | Orijinal · Thumbnail · Grad-CAM · ELA · FFT |
| 🐇 **Mesaj Kuyruğu** | RabbitMQ | AI görevlerinin asenkron dağıtımı |
| 🛡️ **Güvenlik** | JWT + Magic Numbers | Zero-Trust byte seviyesi doğrulama |
| 🚦 **Rate Limiting** | Chained .NET Limiter | Spam & DDoS koruması |
| 🖼️ **Görüntü İşleme** | SixLabors.ImageSharp | 150×150 Thumbnail üretimi |

---

## 🏗️ Sistem Mimarisi

Sistem, bağımlılıkların **dıştan içe** doğru aktığı **Clean Architecture** standartlarında inşa edilmiştir.

```
┌─────────────────────────────────────────────────────────────┐
│                      CLIENT LAYER                           │
│              Next.js (Web)  │  Flutter (Mobile)             │
└─────────────────────────┬───────────────────────────────────┘
                          │ HTTP / REST
┌─────────────────────────▼───────────────────────────────────┐
│                   GATEWAY / BACKEND API                     │
│                    .NET 9 — Bu Proje                        │
│                                                             │
│   ┌─────────────┐   ┌──────────────┐   ┌────────────────┐   │
│   │  JWT Auth   │   │ Rate Limiter │   │ Magic Numbers  │   │
│   │   Shield    │   │  (5/min·20h) │   │  Zero Trust    │   │
│   └─────────────┘   └──────────────┘   └────────────────┘   │
└──────────────┬──────────────────────────────┬───────────────┘
               │ Publish / Subscribe          │ Read / Write
┌──────────────▼──────────┐     ┌─────────────▼───────────────┐
│        RabbitMQ         │     │       Supabase              │
│    (Message Broker)     │     │  PostgreSQL + Storage       │
└──────────────┬──────────┘     └─────────────────────────────┘
               │ Consume
┌──────────────▼──────────────────────────────────────────────┐
│                  PYTHON AI WORKER (WIP)                     │
│         ResNet50 │ Grad-CAM │ ELA │ FFT Analizi             │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔄 Asenkron İş Akışı (Polling Mimarisi)

Bu sistem ağır yapay zeka işlemleri yürüttüğü için **"Ateşle ve Unut" (Fire and Forget)** mantığıyla çalışır.

> ⚠️ **Frontend geliştiricilerinin bu akışa dikkat etmesi zorunludur.**

```
İstemci          Backend API            RabbitMQ          Python Worker
   │                  │                     │                   │
   │─ POST /upload ──►│                     │                   │
   │                  │── Publish Task ────►│                   │
   │◄── 200 OK + ID ──│                     │── Consume ───────►│
   │                  │                     │                   │
   │  ┌─ Her 3sn  ─┐  │                     │   (AI Çalışıyor)  │
   │  │GET /result │  │                     │                   │
   │──►            ──►│                     │                   │
   │◄── 202 Processing│                     │                   │
   │  └────────────┘  │                     │                   │
   │                  │◄── Result ──────────┤◄── Publish ───────│
   │── GET /result ──►│                     │                   │
   │◄──── 200 OK ─────│                     │                   │
``` 

| Adım | Açıklama | UI Davranışı |
|---|---|---|
| **1. Upload** | Görsel doğrulanır, RabbitMQ'ya iletilir | `analysisId` saklanır |
| **2. Polling** | Her 3sn'de `GET /result/{id}` çağrılır | Yükleme animasyonu göster |
| **3. Completion** | Worker sonucu ilettiğinde `200 OK` gelir | Animasyon durdurulur, sonuç çizilir |

---

## 📡 API Dokümantasyonu

> **Base URL:** `https://your-api-domain.com`  
> **Content-Type:** `application/json`

---

### 🔑 1. Yetkilendirme (Token Alma)

Sisteme yapılacak tüm istekler için önce bir **JWT Token** alınmalıdır.

```http
GET /api/Token
X-Client-Token: <Uygulamaya-Özel-Gizli-Anahtar>
```

**✅ Başarılı Yanıt — `200 OK`**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5c...",
  "expiration": "2026-02-21T23:30:00Z"
}
```

---

### 📤 2. Görsel Yükleme ve Analiz Başlatma

Kullanıcının seçtiği görseli sisteme yükler ve analizi başlatır.

> 🚦 **Rate Limit:** Aynı IP'den dakikada **maksimum 5**, saatte **maksimum 20** istek.

```http
POST /api/Analysis/upload
Authorization: Bearer <JWT_TOKEN>
Content-Type: multipart/form-data

Body (form-data):
  image: <dosya — Max 10MB, JPG veya PNG>
```

**✅ Başarılı Yanıt — `200 OK`**

```json
{
  "success": true,
  "message": "Görsel başarıyla yüklendi ve analiz sıraya alındı",
  "analysisId": "87f5035c-8355-40db-b52d-d8002103eefc",
  "timestamp": "2026-02-21T22:06:10Z"
}
```

> 💡 **`analysisId` değerini mutlaka saklayın!** Sonraki adımlar için gereklidir.

**❌ Olası Hatalar**

| Kod | Sebep | UI Aksiyonu |
|---|---|---|
| `400 Bad Request` | Geçersiz dosya imzası veya boyut aşımı | Hata mesajı göster |
| `429 Too Many Requests` | Hız sınırı aşıldı | *"Lütfen biraz bekleyin"* uyarısı göster |

---

### 🔄 3. Analiz Sonucunu Sorgulama (Polling)

Frontend, upload adımından aldığı `analysisId` ile **her 3 saniyede bir** bu ucu yoklamalıdır.

```http
GET /api/Analysis/result/{analysisId}
Authorization: Bearer <JWT_TOKEN>
```

**⏳ İşlem Devam Ediyor — `202 Accepted`** *(UI'da yükleme animasyonu göster)*

```json
{
  "success": true,
  "status": "processing",
  "message": "Analiz işlemi devam etmektedir"
}
```

**✅ İşlem Tamamlandı — `200 OK`** *(Tüm verileri ekrana çiz, animasyonu durdur)*

```json
{
  "success": true,
  "analysisId": "87f5035c-8355-40db-b52d-d8002103eefc",
  "status": "completed",
  "result": {
    "isDeepfake": true,
    "cnnConfidence": 0.9452,
    "elaScore": 0.78,
    "fftAnomalyScore": 0.82,
    "exifAnalysis": {
      "hasMetadata": true,
      "cameraInfo": "Apple iPhone 13",
      "suspiciousIndicators": [
        "Software signature mismatch",
        "Missing GPS data"
      ]
    },
    "originalImagePath": "https://[supabase-url]/originals/87f5...jpg",
    "gradcamImagePath": "https://[supabase-url]/gradcam/87f5...gradcam.jpg",
    "elaImagePath":     "https://[supabase-url]/ela/87f5...ela.jpg",
    "fftImagePath":     "https://[supabase-url]/fft/87f5...fft.jpg",
    "processingTimeSeconds": 2.34,
    "createdAt": "2026-02-21T22:06:10Z"
  }
}
```

**❌ Olası Hatalar**

| Kod | Sebep |
|---|---|
| `500 Internal Server Error` | `status: "failed"` — Sistem veya AI çökmesi |

---

### 📜 4. Geçmiş Analizleri Listeleme

Kullanıcının geçmiş analizlerini sayfalama (pagination) desteğiyle çeker. Rate Limit'e tabi **değildir**.

```http
GET /api/Analysis/history?page=1&pageSize=10
Authorization: Bearer <JWT_TOKEN>
```

**✅ Başarılı Yanıt — `200 OK`**

```json
{
  "success": true,
  "totalCount": 45,
  "page": 1,
  "pageSize": 10,
  "data": [
    {
      "analysisId": "87f5035c-8355-40db-b52d-d8002103eefc",
      "isDeepfake": true,
      "cnnConfidence": 0.9452,
      "thumbnailPath": "https://[supabase-url]/thumbnails/87f5...jpg",
      "createdAt": "2026-02-21T22:06:10Z"
    }
  ]
}
```

---

## 🏆 Tamamlanan Kurumsal Özellikler

- [x] **N-Tier Clean Architecture** — DTO ve Repository Pattern ile soyutlanmış kusursuz katmanlı mimari
- [x] **Event-Driven AI Entegrasyonu** — RabbitMQ üzerinden Python AI servisi ile tam asenkron iletişim
- [x] **Chained Rate Limiting** — Sadece ağır işlemlere (Upload) özel dakikada 5, saatte 20 istek sınırı
- [x] **Magic Numbers Güvenliği** — Uzantılara güvenmeyen, byte seviyesinde Zero-Trust girdi doğrulama
- [x] **Paginated History & Nested JSON** — Optimize edilmiş sayfalama ve yapılandırılmış EXIF veri mimarisi

---

## 🚧 Yol Haritası (Upcoming Tasks)

```
[PHASE 2]  Python AI Worker Mikroservisi
           └─ ResNet50 sınıflandırma
           └─ Grad-CAM, ELA, FFT analiz çıktıları
           └─ RabbitMQ üzerinden sonuç iletimi (FastAPI)

[PHASE 3]  Kullanıcı Arayüzleri (Frontend)
           └─ Next.js Web Uygulaması
           └─ Flutter Mobil Uygulaması
           └─ Short Polling entegrasyonu

[PHASE 4]  🧹 Storage Optimizasyonu (Garbage Collector)
           └─ 7 günden eski orijinal görselleri otomatik sil
           └─ Grad-CAM, ELA, FFT dosyalarını temizle
           └─ Geçmiş JSON verisi ve 150×150 Thumbnail'leri koru
           └─ .NET Background Service (Cron Job) ile zamanlanmış görev
```

---

## 👤 Geliştirici Notları

- Tüm endpoint'ler `Authorization: Bearer <token>` header'ı gerektirir (Token endpoint hariç).
- Upload endpoint'i için `multipart/form-data` encoding kullanılmalıdır.
- Polling mekanizması için önerilen interval **3 saniye**dir; daha agresif yoklama Rate Limit'e takılabilir.
- `thumbnailPath` alanı History endpoint'inde liste kartları için optimize edilmiş 150×150 görseldir.

---
