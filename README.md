# 🏢 CRMProjectAPI

![License](https://img.shields.io/github/license/dogukankosan/CRMProjectAPI)
![Stars](https://img.shields.io/github/stars/dogukankosan/CRMProjectAPI)
![Issues](https://img.shields.io/github/issues/dogukankosan/CRMProjectAPI)
![Last Commit](https://img.shields.io/github/last-commit/dogukankosan/CRMProjectAPI)

> **CRMProjectAPI**, müşteri ilişkileri yönetimi için geliştirilmiş; ticket takibi, bilgi bankası, kullanıcı yönetimi ve SMTP entegrasyonu içeren modern bir ASP.NET Core tabanlı CRM sistemidir. Backend (Web API) ve Frontend (MVC) iki ayrı proje olarak yapılandırılmıştır.

---

## 🚀 Özellikler

- 🎫 **Ticket Yönetimi** — Açma, atama, önceliklendirme ve durum takibi
- 📚 **Bilgi Bankası** — Kategori & ürün bazlı makale sistemi, dosya ekleri ile
- 👥 **Kullanıcı & Müşteri Yönetimi** — Rol bazlı erişim kontrolü (User / Admin / SuperAdmin)
- 🔔 **Anlık Bildirimler** — Yeni yorum ve dosya yükleme bildirimleri (AJAX polling)
- 📨 **SMTP Mail Entegrasyonu** — Test doğrulamalı mail yapılandırması
- 🔐 **JWT Kimlik Doğrulama** — Token tabanlı güvenli oturum yönetimi
- 🏷️ **Sözleşme & Ticket Hakkı Takibi** — Müşteri bazında kota ve süre kontrolü
- 🌐 **SEO Uyumlu Panel** — Open Graph, Twitter Card ve Schema.org desteği
- 🎨 **Dinamik Marka Yönetimi** — Logo, favicon ve site başlığı API üzerinden yönetimi

---

## 🗂 Proje Yapısı / Mimari

```
CRMProject/
│
├── CRMProjectAPI/                      # Backend — ASP.NET Core Web API
│   ├── Controllers/                    # API endpoint'leri
│   │   ├── AdminTicketController       # Ticket CRUD & atama işlemleri
│   │   ├── AdminUserController         # Kullanıcı yönetimi
│   │   ├── AdminCustomerController     # Müşteri yönetimi
│   │   ├── AdminBilgiBankasiController # Bilgi bankası makaleleri
│   │   └── TicketController            # Kullanıcı taraflı ticket işlemleri
│   ├── Data/
│   │   └── DapperContext.cs            # SQL Server bağlantı yönetimi (Dapper)
│   ├── Services/
│   │   └── IJwtService.cs              # JWT üretim & doğrulama servisi
│   └── Validations/
│       └── KnowledgeBaseValidation.cs  # İş kuralı doğrulamaları
│
└── CRMProjectUI/                       # Frontend — ASP.NET Core MVC
    ├── Controllers/                    # MVC controller'ları
    ├── APIService/                     # API'ye HTTP isteklerini yöneten servisler
    ├── Models/                         # DTO ve ViewModel sınıfları
    └── Views/
        ├── Shared/
        │   └── _adminLayout.cshtml     # Ana layout (bildirimler, SEO, dinamik marka)
        ├── Ticket/
        │   └── Ticketform.cshtml       # Ticket oluşturma formu
        └── AdminMail/
            └── Index.cshtml            # SMTP yapılandırma ekranı
```

### Mimari Akış

```
Kullanıcı (Tarayıcı)
        │
        ▼
CRMProjectUI  ── Razor Views + MVC Controller
        │
        │  Bearer Token (JWT) ile HTTP isteği
        ▼
CRMProjectAPI ── ASP.NET Core Web API
        │
        │  Dapper ile parametreli sorgu
        ▼
  SQL Server Veritabanı
```

---

## 📦 Modüller

### 🎫 Ticket Modülü
Destek taleplerinin uçtan uca yönetildiği ana modüldür.

- Ticket oluştururken müşteri seçilir; sistem anlık olarak o müşterinin **sözleşme bitiş tarihi** ve **kalan ticket hakkını** kontrol eder.
- Sözleşme süresi dolmuşsa veya hak bitmişse form submit edilemez.
- 4 kademeli öncelik sistemi vardır: `Düşük` `Normal` `Yüksek` `Kritik`
- Drag & drop dosya yükleme desteklenir.
- Admin yalnızca kendi atanan ticketlarını görebilir; SuperAdmin tüm ticketlara erişebilir.

### 📚 Bilgi Bankası Modülü
Teknik makale ve çözüm rehberlerinin yönetildiği modüldür.

- Makaleler **kategori** (Kurulum, Hata Çözümü, Güncelleme vb.) ve **ürün** bazında filtrelenebilir.
- Her makaleye kod bloğu (dil seçimiyle birlikte) ve dosya eki eklenebilir.
- `IsPublic` ve `IsActive` toggle'ları ile görünürlük yönetimi yapılır.
- Admin yalnızca kendi oluşturduğu makaleyi düzenleyebilir; SuperAdmin kısıtsızdır.
- User rolü yalnızca kendi şirketinin ürünlerine ait makaleleri görebilir.

### 👥 Kullanıcı & Müşteri Modülü
Sistem kullanıcıları ve müşteri firmalarının yönetildiği modüldür.

- Kullanıcılar `User`, `Admin` veya `SuperAdmin` rolüyle tanımlanır.
- Müşterilere Logo ürünleri atanır; ticket ve bilgi bankası kayıtları bu ürünlerle ilişkilendirilir.
- Müşteri bazında sözleşme tarihi ve ticket kotası tanımlanabilir.

### 📨 Mail Ayarları Modülü
Sistem tarafından gönderilecek maillerin SMTP yapılandırmasını içerir.

- SMTP host, port, SSL ve kimlik bilgileri tek ekrandan yönetilir.
- Gmail, Outlook gibi popüler sağlayıcılar için hızlı doldur seçenekleri mevcuttur.
- **Mevcut bir kayıt güncellenirken önce test maili gönderilmesi zorunludur** — test başarılı olmadan kaydet butonu aktif olmaz.

### 🔔 Bildirim Sistemi
Panel açıldığında iki ayrı AJAX isteği ile bildirimler çekilir.

| Bildirim Tipi | Açıklama |
|---|---|
| Kişisel Ticketlar | Kullanıcının üzerindeki açık ve bekleyen ticketlar |
| Firma Aktiviteleri | Firmanın ticket'larına gelen yorum ve dosya yüklemeleri |

---

## 🛠️ Kurulum & Çalıştırma

### Gereksinimler

- .NET 8 SDK
- SQL Server (LocalDB veya full instance)
- Visual Studio 2022 / VS Code

### 1. Projeyi Klonla

```bash
git clone https://github.com/dogukankosan/CRMProjectAPI.git
cd CRMProjectAPI
```

### 2. Veritabanını Hazırla

SQL Server üzerinde bir veritabanı oluşturun ve gerekli migration / script dosyalarını çalıştırın.

### 3. API Yapılandırması — `CRMProjectAPI/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=CRMDb;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "JwtSettings": {
    "SecretKey": "buraya-en-az-32-karakterlik-gizli-anahtar",
    "Issuer": "CRMProjectAPI",
    "Audience": "CRMProjectUI",
    "ExpiryHours": 8
  },
  "ApiSettings": {
    "ApiKey": "dahili-api-anahtariniz"
  }
}
```

> ⚠️ `SecretKey` değerini production ortamında mutlaka güçlü ve rastgele bir değerle değiştirin. `.gitignore` ile `appsettings.json` dosyasını versiyon kontrolüne dahil etmemeye dikkat edin.

### 4. UI Yapılandırması — `CRMProjectUI/appsettings.json`

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7001",
    "ApiKey": "dahili-api-anahtariniz"
  }
}
```

> `BaseUrl` değerinin API projesinin çalıştığı adres ve port ile eşleştiğinden emin olun.

### 5. Projeleri Çalıştır

Önce API'yi, ardından UI'ı başlatın:

```bash
# Terminal 1 — API
cd CRMProjectAPI
dotnet run

# Terminal 2 — UI
cd CRMProjectUI
dotnet run
```

> Visual Studio kullanıyorsanız solution'ı açıp her iki projeyi **Multiple Startup Projects** olarak ayarlayabilirsiniz.

---

## 📡 API Endpoint Özeti

### 🎫 Ticket

| Method | Endpoint | Açıklama | Rol |
|---|---|---|---|
| `GET` | `/api/ticket` | Ticket listesi | Admin+ |
| `GET` | `/api/ticket/{id}` | Ticket detayı | Admin+ |
| `POST` | `/api/ticket` | Yeni ticket oluştur | Tümü |
| `PUT` | `/api/ticket/{id}` | Ticket güncelle | Admin+ |
| `DELETE` | `/api/ticket/{id}` | Ticket sil | SuperAdmin |
| `GET` | `/api/ticket/check-eligibility` | Sözleşme & kota kontrolü | Admin+ |

### 📚 Bilgi Bankası

| Method | Endpoint | Açıklama | Rol |
|---|---|---|---|
| `GET` | `/api/knowledgebase` | Makale listesi (filtreli) | Tümü |
| `GET` | `/api/knowledgebase/{id}` | Makale detayı | Tümü |
| `POST` | `/api/knowledgebase` | Makale oluştur | Admin+ |
| `PUT` | `/api/knowledgebase/{id}` | Makale güncelle | Admin+ |
| `DELETE` | `/api/knowledgebase/{id}` | Makale sil | Admin+ |
| `POST` | `/api/knowledgebase/{id}/files` | Dosya yükle | Admin+ |
| `GET` | `/api/knowledgebase/files/{fileId}/download` | Dosya indir | Tümü |
| `DELETE` | `/api/knowledgebase/files/{fileId}` | Dosya sil | Admin+ |
| `PATCH` | `/api/knowledgebase/{id}/toggle-active` | Aktif/Pasif toggle | Admin+ |
| `PATCH` | `/api/knowledgebase/{id}/toggle-public` | Public/Private toggle | Admin+ |

### 👥 Kullanıcı & Müşteri

| Method | Endpoint | Açıklama | Rol |
|---|---|---|---|
| `GET` | `/api/user` | Kullanıcı listesi | SuperAdmin |
| `POST` | `/api/user` | Kullanıcı oluştur | SuperAdmin |
| `PUT` | `/api/user/{id}` | Kullanıcı güncelle | SuperAdmin |
| `GET` | `/api/customer` | Müşteri listesi | Admin+ |
| `POST` | `/api/customer` | Müşteri oluştur | Admin+ |

### 🔐 Auth

| Method | Endpoint | Açıklama |
|---|---|---|
| `POST` | `/api/auth/login` | Giriş yap, JWT döner |
| `POST` | `/api/auth/refresh` | Token yenile |

---

## 🔐 Rol Sistemi

| Rol | Yetki |
|---|---|
| `SuperAdmin` | Tüm kayıtlara tam erişim, kullanıcı yönetimi |
| `Admin` | Kendi kayıtlarını yönetebilir, müşteri işlemleri |
| `User` | Yalnızca kendi şirketinin verilerini görebilir |

---

## 🛠️ Teknoloji Yığını

| Teknoloji | Kullanım Amacı |
|---|---|
| ![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?logo=dotnet) | Web API & MVC framework |
| ![Dapper](https://img.shields.io/badge/Dapper-ORM-blue) | Hafif & hızlı SQL erişim katmanı |
| ![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?logo=microsoftsqlserver&logoColor=white) | İlişkisel veritabanı |
| ![JWT](https://img.shields.io/badge/JWT-Auth-000000?logo=jsonwebtokens) | Token tabanlı kimlik doğrulama |
| ![Bootstrap](https://img.shields.io/badge/Bootstrap-4-7952B3?logo=bootstrap&logoColor=white) | UI bileşenleri |
| ![SweetAlert2](https://img.shields.io/badge/SweetAlert2-Notifications-ff6384) | Kullanıcı bildirimleri |
| ![DataTables](https://img.shields.io/badge/DataTables-Grid-003865) | Tablo & listeleme bileşeni |

---

## 🤝 Katkı

Katkı sağlamak için projeyi forklayabilir ve pull request gönderebilirsiniz.

1. Projeyi forklayın
2. Yeni bir branch oluşturun: `git checkout -b feature/yenilik`
3. Değişikliklerinizi commit edin: `git commit -m 'feat: yeni özellik'`
4. Branch'i push edin: `git push origin feature/yenilik`
5. Pull Request açın

---

## 📄 Lisans

MIT License — Detaylar için [LICENSE](./LICENSE) dosyasına bakınız.

---

## 📬 İletişim

- 👨‍💻 Geliştirici: [@dogukankosan](https://github.com/dogukankosan)
- 🐞 Hata & Öneri: [Issues Sekmesi](https://github.com/dogukankosan/CRMProjectAPI/issues)

---

<p align="center">
  <img src="https://img.shields.io/badge/ASP.NET_Core-Web_API-512BD4?logo=dotnet" alt="aspnet" />
  <img src="https://img.shields.io/badge/MVC-Razor_Views-68217A?logo=dotnet" alt="mvc" />
  <img src="https://img.shields.io/badge/Dapper-SQL_Server-blue" alt="dapper" />
  <img src="https://img.shields.io/badge/JWT-Authentication-000000?logo=jsonwebtokens" alt="jwt" />
</p>
