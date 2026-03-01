# 🕵️‍♂️ Deepfake Detection API — Core Backend

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](#)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Event_Driven-FF6600?logo=rabbitmq&logoColor=white)](#)
[![Supabase](https://img.shields.io/badge/Supabase-PostgreSQL_%26_Storage-3ECF8E?logo=supabase&logoColor=white)](#)
[![Security](https://img.shields.io/badge/Security-Zero_Trust-DC143C?logo=shield&logoColor=white)](#)
[![Architecture](https://img.shields.io/badge/Architecture-Clean-22C55E?logo=blueprint&logoColor=white)](#)
[![API Version](https://img.shields.io/badge/API-v1-0EA5E9)](#)

> **Dijital ortamlardaki manipüle edilmiş (Deepfake) görselleri tespit etmek için geliştirilmiş, mikroservis tabanlı, olay güdümlü (Event-Driven) ve asenkron çalışan yüksek performanslı analiz platformunun ana omurgası.**

Kullanıcı arayüzleri (**Next.js / Flutter**) ile Python tabanlı ağır Yapay Zeka analiz işçileri (**Workers**) arasında güvenli bir köprü görevi gören bu API; sistemin darboğaza girmeden ve kilitlenmeden binlerce asenkron isteği yönetebilmesini sağlar.

---

## 📋 İçindekiler

- [Teknoloji Stack'i](#-teknoloji-stacki)
- [Sistem Mimarisi](#-sistem-mimarisi)
- [Asenkron İş Akışı](#-asenkron-i̇ş-akışı-polling-mimarisi)
- [API Dokümantasyonu](#-api-dokümantasyonu)
    - [1. Yetkilendirme](#-1-yetkilendirme-auth)
    - [2. Görsel Yükleme](#-2-görsel-yükleme-ve-analiz-başlatma)
    - [3. Sonuç Sorgulama](#-3-analiz-sonucunu-sorgulama-polling)
    - [4. Geçmiş Listeleme](#-4-geçmiş-analizleri-listeleme)
- [Mimari İyileştirmeler — Phase 2](#-mimari-i̇yileştirmeler-phase-2-refactor)
- [Güvenlik & Prodüksiyon Refaktörü — Phase 3](#-güvenlik--prodüksiyon-refaktörü-phase-3)
- [Tamamlanan Özellikler](#-tamamlanan-kurumsal-özellikler)
- [Yol Haritası](#-yol-haritası-upcoming-tasks)
- [Canlı Test Sonuçları](#-canlı-test-sonuçları--doğrulama-raporu)

---

## 🛠️ Teknoloji Stack'i

| Katman | Teknoloji | Açıklama |
|---|---|---|
| 🧠 **Çekirdek** | .NET 9 / ASP.NET Core | Web API altyapısı |
| 🗄️ **Veritabanı** | PostgreSQL (Supabase) | `AsNoTracking` ile optimize EF Core 9 |
| ☁️ **Depolama** | Supabase Storage | Orijinal · Thumbnail · Grad-CAM · ELA · FFT |
| 🐇 **Mesaj Kuyruğu** | RabbitMQ | AI görevlerinin asenkron dağıtımı |
| 🛡️ **Güvenlik** | JWT (Issuer/Audience) + Magic Numbers | Zero-Trust, byte seviyesi doğrulama |
| 🚦 **Rate Limiting** | 3-Zincirli Chained .NET Limiter | Auth + Upload ayrımı, Spam & DDoS koruması |
| 🖼️ **Görüntü İşleme** | SixLabors.ImageSharp | 150×150 Thumbnail üretimi |
| 🔀 **Proxy Desteği** | ASP.NET ForwardedHeaders | Railway/Docker gerçek IP algılama |

---

## 🏗️ Sistem Mimarisi

Sistem, bağımlılıkların **dıştan içe** doğru aktığı **Clean Architecture** standartlarında inşa edilmiştir.

```
┌─────────────────────────────────────────────────────────────┐
│                      CLIENT LAYER                           │
│              Next.js (Web)  │  Flutter (Mobile)             │
└─────────────────────────┬───────────────────────────────────┘
                          │ HTTP / REST  (api/v1/...)
┌─────────────────────────▼───────────────────────────────────┐
│                   GATEWAY / BACKEND API                     │
│                    .NET 9 — Bu Proje                        │
│                                                             │
│  ┌──────────────┐  ┌───────────────┐  ┌────────────────┐   │
│  │  JWT Auth    │  │ 3-Chain Rate  │  │ Magic Numbers  │   │
│  │ Iss+Aud+IP  │  │    Limiter    │  │  Zero Trust    │   │
│  └──────────────┘  └───────────────┘  └────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │       ForwardedHeaders Middleware (Railway-Safe)     │   │
│  └──────────────────────────────────────────────────────┘   │
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
İstemci             Backend API             RabbitMQ         Python Worker
   │                     │                      │                  │
   │── POST /auth ───────►│                      │                  │
   │◄── Cookie/Token ─────│                      │                  │
   │                     │                      │                  │
   │── POST /analyses ───►│                      │                  │
   │                     │── Publish Task ──────►│                  │
   │◄── 200 OK + ID ──────│                      │── Consume ───────►│
   │                     │                      │                  │
   │  ┌─ Her 3sn ─┐      │                      │  (AI Çalışıyor)  │
   │  │GET /{id}  │      │                      │                  │
   │──►            ──────►│                      │                  │
   │◄── 202 Processing ───│                      │                  │
   │  └────────────┘      │                      │                  │
   │                     │◄─ Result ─────────────┤◄── Publish ──────│
   │── GET /{id} ────────►│                      │                  │
   │◄──── 200 OK ─────────│                      │                  │
```

| Adım | Açıklama | UI Davranışı |
|---|---|---|
| **1. Auth** | `POST /api/v1/auth` ile token alınır | Arka planda sessizce çalışır |
| **2. Upload** | Görsel doğrulanır, RabbitMQ'ya iletilir | `analysisId` saklanır |
| **3. Polling** | Her 3sn'de `GET /api/v1/analyses/{id}` çağrılır | Yükleme animasyonu göster |
| **4. Completion** | Worker sonucu ilettiğinde `200 OK` gelir | Animasyon durdurulur, sonuç çizilir |

---

## 📡 API Dokümantasyonu

> **Base URL:** `https://your-api-domain.com`
> **API Version:** `v1`
> **Content-Type:** `application/json`

---

### 🔑 1. Yetkilendirme (Auth)

Auth endpoint'i iki işlevi tek adreste birleştirir. Token almak `POST`, token durumu sorgulamak `GET` ile yapılır.

#### Token Durumu Kontrolü

```http
GET /api/v1/auth
Authorization: Bearer <JWT_TOKEN>
```

**✅ Token Geçerli — `200 OK`**

```json
{ "isAuthenticated": true }
```

> 💡 Bu endpoint Rate Limit'e tabi **değildir.** SPA uygulamaları her açılışta bunu güvenle çağırabilir.

---

#### Token Üretme

```http
POST /api/v1/auth
X-Client-Token: <Uygulamaya-Özel-Gizli-Anahtar>
X-Client-Platform: web | mobile
```

**✅ Başarılı Yanıt (Web) — `200 OK`**

Web platformunda token JSON olarak **dönmez.** Tarayıcının güvenli çerez kasasına yazılır:

```
set-cookie: jwt_token=eyJ...; HttpOnly; Secure; SameSite=None
```

```json
{
  "success": true,
  "expiresAt": "2026-03-08T22:06:10Z"
}
```

**✅ Başarılı Yanıt (Mobile) — `200 OK`**

```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5c...",
  "expiresAt": "2026-03-08T22:06:10Z"
}
```

> 🚦 **Rate Limit:** `POST /api/v1/auth` — Aynı IP'den **5 dakikada 3 istek** ile sınırlıdır.

---

### 📤 2. Görsel Yükleme ve Analiz Başlatma

```http
POST /api/v1/analyses
Authorization: Bearer <JWT_TOKEN>   ← Mobil için. Web'de cookie otomatik gider.
Content-Type: multipart/form-data

Body (form-data):
  image: <dosya — Max 10MB, JPG veya PNG>
```

> 🚦 **Rate Limit:** Aynı IP'den dakikada **maksimum 5**, saatte **maksimum 20** istek.

**✅ Başarılı Yanıt — `200 OK`**

```json
{
  "success": true,
  "message": "Görsel başarıyla yüklendi ve analiz sıraya alındı",
  "analysisId": "87f5035c-8355-40db-b52d-d8002103eefc",
  "timestamp": "2026-02-21T22:06:10Z"
}
```

> 💡 **`analysisId` değerini mutlaka saklayın!** Polling ve geçmiş için gereklidir.

**❌ Olası Hatalar**

| Kod | Sebep | UI Aksiyonu |
|---|---|---|
| `400 Bad Request` | Geçersiz dosya imzası veya boyut aşımı | Hata mesajı göster |
| `429 Too Many Requests` | Hız sınırı aşıldı (dakika veya saat) | Kalan süreyi içeren akıllı mesaj göster |

---

### 🔄 3. Analiz Sonucunu Sorgulama (Polling)

```http
GET /api/v1/analyses/{analysisId:guid}
Authorization: Bearer <JWT_TOKEN>
```

**⏳ İşlem Devam Ediyor — `202 Accepted`** *(UI'da animasyon göster)*

```json
{
  "success": true,
  "status": "Processing",
  "message": "Analiz işlemi devam etmektedir"
}
```

**✅ İşlem Tamamlandı — `200 OK`** *(Animasyonu durdur, sonucu çiz)*

```json
{
  "success": true,
  "analysisId": "87f5035c-8355-40db-b52d-d8002103eefc",
  "status": "Completed",
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
    "gradcamImagePath":  "https://[supabase-url]/gradcam/87f5...gradcam.jpg",
    "elaImagePath":      "https://[supabase-url]/ela/87f5...ela.jpg",
    "fftImagePath":      "https://[supabase-url]/fft/87f5...fft.jpg",
    "processingTimeSeconds": 2.34,
    "createdAt": "2026-02-21T22:06:10Z"
  }
}
```

**❌ Olası Hatalar**

| Kod | Sebep |
|---|---|
| `404 Not Found` | Geçersiz `analysisId` |
| `500 Internal Server Error` | `status: "Failed"` — AI worker hatası veya timeout |

---

### 📜 4. Geçmiş Analizleri Listeleme

Rate Limit'e tabi **değildir.**

```http
GET /api/v1/analyses?page=1&pageSize=10
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

> 💡 `thumbnailPath` — 150×150 piksel optimize görseldir. Orijinal boyutlu görsel yerine bunu kullanın.

**Sayfalama:**

```
?page=1&pageSize=10  →  İlk 10 kayıt
?page=2&pageSize=10  →  Sonraki 10 kayıt
```

---

## ⚙️ Mimari İyileştirmeler (Phase 2 Refactor)

Projenin ikinci fazında `Program.cs` "ayar çöplüğü" olmaktan çıkarılmış, kurumsal **Extension Pattern** benimsenmiştir.

### 📁 Dosya Yapısı

```
Deepfake.API/
├── Controllers/
│   ├── AuthController.cs          ← GET /api/v1/auth + POST /api/v1/auth
│   └── AnalysesController.cs      ← RESTful /api/v1/analyses
├── Extensions/
│   ├── RateLimiterExtensions.cs   ← 3-zincirli Rate Limiter buraya taşındı
│   └── JwtExtensions.cs           ← JWT ayarları buraya taşındı
├── Middlewares/
│   └── IpValidationMiddleware.cs  ← X-Forwarded-For desteği eklendi
└── Program.cs                     ← Sade yol haritası
```

---

### `Program.cs` — Final Görünüm

```csharp
using Deepfake.API.Extensions;
using Microsoft.AspNetCore.HttpOverrides;

// ForwardedHeaders — Railway/Docker proxy desteği için EN ÜSTTE
builder.Services.Configure<ForwardedHeadersOptions>(options => {
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Extension'lar üzerinden temiz servis kaydı
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("JWT_SECRET missing");
builder.Services.AddCustomJwtAuth(jwtSecret);
builder.Services.AddCustomRateLimiter();
builder.Services.AddHealthChecks();

// ...

// Middleware sıralaması — sıra kritik!
app.UseForwardedHeaders();   // 1. Gerçek IP'yi al
app.UseCors("AllowAll");     // 2. CORS
app.UseRateLimiter();        // 3. Hız kontrolü (auth'tan önce, performanslı)
app.UseAuthentication();     // 4. Kim olduğunu doğrula
app.UseAuthorization();      // 5. Ne yapabileceğini doğrula
app.UseMiddleware<IpValidationMiddleware>(); // 6. Token-IP uyumu

app.MapControllers();
app.MapHealthChecks("/health");

var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
app.Run($"http://0.0.0.0:{port}");
```

---

### Route Değişikliği Tablosu

| Eski Route | Yeni Route | Metod | Açıklama |
|---|---|---|---|
| `/api/Token` | `/api/v1/auth` | `POST` | Token üretme |
| — | `/api/v1/auth` | `GET` | Token durum kontrolü |
| `/api/Analysis/upload` | `/api/v1/analyses` | `POST` | Analiz başlatma |
| `/api/Analysis/result/{id}` | `/api/v1/analyses/{id:guid}` | `GET` | Sonuç sorgulama |
| `/api/Analysis/history` | `/api/v1/analyses` | `GET` | Geçmiş listeleme |

---

## 🔐 Güvenlik & Prodüksiyon Refaktörü (Phase 3)

### 1. Gelişmiş JWT Güvenliği (`JwtExtensions.cs`)

JWT doğrulaması güçlendirildi. Token artık yalnızca imza ile değil, **kim tarafından** ve **kimin için** üretildiğiyle de doğrulanır.

```csharp
options.TokenValidationParameters = new TokenValidationParameters {
    ValidateIssuer   = true,        // ✅ Issuer doğrulaması AKTİF
    ValidateAudience = true,        // ✅ Audience doğrulaması AKTİF
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer    = issuer,
    ValidAudience  = audience,
    IssuerSigningKey = new SymmetricSecurityKey(...),
    ClockSkew = TimeSpan.FromSeconds(30)  // ✅ 5 dk → 30 sn (anında kesme)
};
```

| Parametre | Eski Değer | Yeni Değer | Etki |
|---|---|---|---|
| `ValidateIssuer` | `false` | `true` | Cross-site token kullanımı engellendi |
| `ValidateAudience` | `false` | `true` | Yanlış servise giden token reddedilir |
| `ClockSkew` | `5 dakika` | `30 saniye` | Süresi dolan token neredeyse anında reddedilir |

---

### 2. 3-Zincirli Rate Limiter (`RateLimiterExtensions.cs`)

Tüm endpoint'lere tek kural uygulamak yerine her kaynak türüne özel, **akıllı bir kademeli sınırlandırma** kuruldu.

```
┌─────────────────────────────────────────────────────────────┐
│                   RATE LIMITER ZİNCİRİ                      │
├──────────────────┬──────────────────┬───────────────────────┤
│  ZİNCİR 1        │  ZİNCİR 2        │  ZİNCİR 3             │
│  Auth Koruma     │  Upload/Dakika   │  Upload/Saat          │
├──────────────────┼──────────────────┼───────────────────────┤
│ GET /auth → ∞   │ POST /analyses   │ POST /analyses        │
│ POST /auth →    │ IP başına        │ IP başına             │
│ 3 istek / 5 dk  │ 5 istek / 1 dk  │ 20 istek / 1 saat     │
└──────────────────┴──────────────────┴───────────────────────┘
```

**Akıllı Hata Mesajı:** Reddedilen isteklerde `RetryAfter` süresi okunarak kullanıcıya hangi sınıra takıldığı ve ne zaman deneyebileceği anında bildirilir:

```json
// Dakika sınırına takıldıysa:
{ "message": "Dakikalık yükleme sınırınızı (5 resim) aştınız. Lütfen 45 saniye sonra tekrar deneyin." }

// Saat sınırına takıldıysa:
{ "message": "Saatlik yükleme sınırınızı (20 resim) aştınız. Lütfen 38 dakika sonra tekrar deneyin." }
```

---

### 3. Proxy-Aware IP Algılama (`IpValidationMiddleware.cs`)

Railway, Docker ve benzeri ortamlarda `context.Connection.RemoteIpAddress` her zaman proxy'nin IP'sini döndürür. `UseForwardedHeaders` entegrasyonu sayesinde middleware artık `X-Forwarded-For` başlığından **gerçek kullanıcı IP'sini** okur.

```
Önceki durum: Tüm istekler → Railway Proxy IP (10.x.x.x)
Yeni durum:   Tüm istekler → Gerçek kullanıcı IP (85.x.x.x)
```

Bu değişiklik olmadan Railway'de:
- Rate Limiter tüm kullanıcıları aynı IP'den görür → herkes aynı kotayı paylaşır
- IP doğrulama her isteği reddeder → `403 Forbidden` döngüsü

---

### 4. `AuthController.cs` — Tek Adres, İki Görev

`TokenController` kaldırılarak yerine tam RESTful uyumlu `AuthController` yazıldı.

```
GET  /api/v1/auth  →  [Authorize] — Token geçerli mi? { isAuthenticated: true }
POST /api/v1/auth  →  [Anonymous] — Token üret (web: cookie, mobile: JSON)
```

---

## 🏆 Tamamlanan Kurumsal Özellikler

- [x] **N-Tier Clean Architecture** — DTO ve Repository Pattern ile soyutlanmış katmanlı mimari
- [x] **Event-Driven AI Entegrasyonu** — RabbitMQ üzerinden Python AI servisi ile asenkron iletişim
- [x] **3-Zincirli Rate Limiting** — Auth ve Upload için ayrı kurallar, akıllı hata mesajı
- [x] **Magic Numbers Güvenliği** — Byte seviyesi Zero-Trust girdi doğrulama
- [x] **Paginated History & Nested JSON** — Optimize sayfalama ve yapılandırılmış EXIF verisi
- [x] **Extension Pattern Refactor** — `Program.cs` sade yol haritasına dönüştürüldü
- [x] **RESTful v1 Route Standardizasyonu** — `/api/v1/resource` formatı, saf HTTP metodları
- [x] **Gelişmiş JWT (Issuer + Audience + ClockSkew)** — Cross-site saldırı engellemesi
- [x] **Railway-Uyumlu ForwardedHeaders** — Proxy arkasında gerçek IP tespiti
- [x] **AuthController (GET + POST)** — Token kontrolü ve üretimi tek RESTful adreste
- [x] **Otomatik DB Migration** — Uygulama ayağa kalkarken şema otomatik güncellenir

---

## 🚧 Yol Haritası (Upcoming Tasks)

```
[PHASE 4 — Aktif]
  Python AI Worker Mikroservisi
  └─ RabbitMQ'dan görev tüketimi (pika)
  └─ ResNet50 CNN sınıflandırma
  └─ Grad-CAM ısı haritası üretimi
  └─ ELA ve FFT anomali görselleştirme
  └─ Sonuçları Supabase'e yaz, RabbitMQ'ya bildir (FastAPI)

[PHASE 5]
  Kullanıcı Arayüzleri (Frontend)
  └─ Next.js Web Uygulaması
  └─ Flutter Mobil Uygulaması
  └─ Short Polling entegrasyonu

[PHASE 6]  🧹 Storage Garbage Collector
  └─ 7 günden eski orijinal görselleri otomatik sil
  └─ Grad-CAM, ELA, FFT dosyalarını temizle
  └─ Thumbnail ve JSON verisini koru
  └─ .NET Background Service (Cron Job)
```

---

## ✅ Canlı Test Sonuçları & Doğrulama Raporu

---

### 🔑 1. Token Üretimi ve Platform Ayrımı

- **Web modu** → `POST /api/v1/auth` isteğinde `HttpOnly; Secure; SameSite=None` çerezi oluşturuldu. Token JavaScript'e hiç açılmadı. ✅
- **Mobil modu** → Aynı endpoint JSON formatında token döndürdü. ✅
- **Durum kontrolü** → `GET /api/v1/auth` geçerli token ile `isAuthenticated: true` döndürdü. ✅

---

### 🛡️ 2. Magic Numbers Güvenlik Kalkanı

İçi `exe` dolu sahte bir `sahte.jpg` sisteme gönderildi:

| Metrik | Sonuç |
|---|---|
| Tespit süresi | **24ms** |
| HTTP Yanıtı | `400 Bad Request` |
| Kontrol türü | Byte-level Magic Numbers |

Sistem dosya uzantısına güvenmedi; byte imzasını okuyarak tehdidi **saniyenin binde 24'ünde** etkisiz hale getirdi. ✅

---

### 🚦 3. Kademeli Rate Limiter Koruması

| Senaryo | Beklenen | Sonuç |
|---|---|---|
| 6. upload isteği (1 dakika içinde) | `429` + dakika mesajı | ✅ |
| 21. upload isteği (1 saat içinde) | `429` + saat mesajı | ✅ |
| 4. auth isteği (5 dakika içinde) | `429` | ✅ |
| `GET /api/v1/auth` (sınırsız) | `200 OK` her seferinde | ✅ |

---

### 📜 4. Rate Limit'ten Etkilenmeyen History

Upload limiti doluyken `GET /api/v1/analyses?page=1` isteği atıldı:

```
HTTP 200 OK ✅
```

Upload yapamayan kullanıcı geçmişini sorunsuz görüntüleyebildi. ✅

---

### 🎯 Backend Tamamlanma Özeti

| Bileşen | Durum |
|---|---|
| Veritabanı Bağlantısı (PostgreSQL) | ✅ Tamamlandı |
| JWT — Issuer + Audience + ClockSkew | ✅ Tamamlandı |
| Magic Numbers Dosya Doğrulama | ✅ Tamamlandı |
| 3-Zincirli Chained Rate Limiter | ✅ Tamamlandı |
| RabbitMQ Mesaj Kuyruğu | ✅ Tamamlandı |
| Asenkron Polling Mimarisi | ✅ Tamamlandı |
| Extension Pattern Refactor | ✅ Tamamlandı |
| RESTful v1 Route Standardizasyonu | ✅ Tamamlandı |
| AuthController (GET + POST) | ✅ Tamamlandı |
| ForwardedHeaders (Railway Proxy) | ✅ Tamamlandı |
| IP Validation Middleware | ✅ Tamamlandı |
| Otomatik DB Migration | ✅ Tamamlandı |

**.NET Core Backend (Gateway) tüm fazlarıyla tamamlandı. Sıradaki adım → Python AI Worker 🐍**

---

## 👤 Geliştirici Notları

- Tüm endpoint'ler `Authorization: Bearer <token>` veya `HttpOnly Cookie` gerektirir (`POST /api/v1/auth` hariç).
- Upload için `multipart/form-data` kullanılmalıdır.
- Polling için önerilen interval **3 saniye**dir; daha agresif yoklama Rate Limit'e takılabilir.
- `thumbnailPath` — History görünümünde 150×150 px optimize görsel kullanın, orijinali değil.
- Tüm rotalar versiyonlanmıştır: `/api/v1/...` — Eski `/api/Analysis/...` veya `/api/Token` path'leri artık geçerli değildir.
- Railway deploy'unda `PORT`, `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_CLIENT_TOKEN` env değişkenlerinin tanımlı olduğundan emin olun.

---