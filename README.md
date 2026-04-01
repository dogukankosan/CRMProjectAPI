# 🏢 CRMProjectAPI

<img width="1898" height="936" alt="123" src="https://github.com/user-attachments/assets/2ec32bbd-34b7-44f2-bd57-617f17a0ca4a" />


![License](https://img.shields.io/github/license/dogukankosan/CRMProjectAPI)
![Stars](https://img.shields.io/github/stars/dogukankosan/CRMProjectAPI)
![Issues](https://img.shields.io/github/issues/dogukankosan/CRMProjectAPI)
![Last Commit](https://img.shields.io/github/last-commit/dogukankosan/CRMProjectAPI)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![SignalR](https://img.shields.io/badge/SignalR-Real--Time-FF6F61?logo=signalr)

> **CRMProjectAPI**, müşteri ilişkileri yönetimi için geliştirilmiş; ticket takibi, gerçek zamanlı bildirimler (SignalR), duyuru yönetimi, bilgi bankası, kullanıcı yönetimi ve SMTP entegrasyonu içeren modern bir ASP.NET Core tabanlı CRM sistemidir. Backend (Web API) ve Frontend (MVC) iki ayrı proje olarak yapılandırılmıştır.

---

## 🚀 Özellikler

- 🎫 **Ticket Yönetimi** — Açma, atama, önceliklendirme ve durum takibi
- 📡 **Gerçek Zamanlı Bildirimler (SignalR)** — Yeni ticket, yorum ve dosya yükleme olaylarında anlık push bildirimleri
- 📢 **Duyuru Yönetimi** — Admin/SuperAdmin tarafından yayınlanan, dosya ekli, öncelik sıralı duyurular; kullanıcı bazında "tekrar gösterme" desteği
- 📚 **Bilgi Bankası** — Kategori & ürün bazlı makale sistemi, dosya ekleri ile
- 👥 **Kullanıcı & Müşteri Yönetimi** — Rol bazlı erişim kontrolü (User / Admin / SuperAdmin)
- 📨 **SMTP Mail Entegrasyonu** — Test doğrulamalı mail yapılandırması; ticket kapanışında otomatik mail
- 🔐 **JWT Kimlik Doğrulama** — Token tabanlı güvenli oturum yönetimi
- 🏷️ **Sözleşme & Ticket Hakkı Takibi** — Müşteri bazında kota ve süre kontrolü
- 🌐 **SEO Uyumlu Panel** — Open Graph, Twitter Card ve Schema.org desteği
- 🎨 **Dinamik Marka Yönetimi** — Logo, favicon ve site başlığı API üzerinden yönetimi

---

## 🗂 Proje Yapısı / Mimari

```
CRMProject/
│
├── CRMProjectAPI/                        # Backend — ASP.NET Core Web API
│   ├── Controllers/
│   │   ├── AnnouncementController        # Duyuru CRUD, dosya, dismiss & SignalR
│   │   ├── TicketController              # Ticket CRUD, yorumlar, dosyalar, dashboard & SignalR
│   │   ├── AdminUserController           # Kullanıcı yönetimi
│   │   ├── AdminCustomerController       # Müşteri yönetimi
│   │   └── AdminBilgiBankasiController   # Bilgi bankası makaleleri
│   ├── Hubs/
│   │   └── TicketHub.cs                  # SignalR hub (JoinGroup / LeaveGroup)
│   ├── Data/
│   │   └── DapperContext.cs              # SQL Server bağlantı yönetimi (Dapper)
│   ├── Services/
│   │   ├── IJwtService.cs                # JWT üretim & doğrulama servisi
│   │   └── IMailService.cs               # SMTP mail servisi
│   └── Validations/
│       ├── AnnouncementValidation.cs     # Duyuru iş kuralı doğrulamaları
│       └── KnowledgeBaseValidation.cs    # Bilgi bankası doğrulamaları
│
└── CRMProjectUI/                         # Frontend — ASP.NET Core MVC
    ├── Controllers/
    ├── APIService/
    ├── Models/
    └── Views/
        ├── Shared/
        │   └── _adminLayout.cshtml       # Ana layout (SignalR bağlantısı, bildirimler, duyurular)
        ├── Announcement/
        │   └── Index.cshtml              # Duyuru yönetim paneli
        ├── Ticket/
        │   └── Ticketform.cshtml         # Ticket oluşturma formu
        └── AdminMail/
            └── Index.cshtml              # SMTP yapılandırma ekranı
```

### Mimari Akış

```
Kullanıcı (Tarayıcı)
        │
        ├── HTTP (REST)          ──►  CRMProjectUI  (Razor Views + MVC)
        │                                   │
        │                        Bearer Token (JWT) ile HTTP isteği
        │                                   │
        │                                   ▼
        │                            CRMProjectAPI  (ASP.NET Core Web API)
        │                                   │
        │                        Dapper ile parametreli sorgu
        │                                   │
        │                                   ▼
        │                            SQL Server Veritabanı
        │
        └── WebSocket (SignalR)  ──►  TicketHub
                                        ├── admins grubu
                                        ├── customer_{id} grubu
                                        └── Tüm bağlı istemciler (duyurular)
```

---

## 📦 Modüller

### 🎫 Ticket Modülü
Destek taleplerinin uçtan uca yönetildiği ana modüldür.

- Ticket oluştururken müşteri seçilir; sistem anlık olarak o müşterinin **sözleşme bitiş tarihi** ve **kalan ticket hakkını** kontrol eder.
- Sözleşme süresi dolmuşsa veya hak bitmişse form submit edilemez.
- 4 kademeli öncelik sistemi: `Düşük` `Normal` `Yüksek` `Kritik`
- Drag & drop dosya yükleme desteklenir.
- Admin yalnızca kendi atanan ticketlarını görebilir; SuperAdmin tüm ticketlara erişebilir.
- Ticket kapatıldığında ilgili müşteri kullanıcılarına otomatik **SMTP maili** gönderilir.

---

### 📡 SignalR — Gerçek Zamanlı Bildirim Sistemi

`TicketHub` üzerinden WebSocket bağlantısı kurulur. İstemciler grup bazlı abonelik ile ilgili olaylara abone olur.

#### Hub

```csharp
// CRMProjectAPI/Hubs/TicketHub.cs
public class TicketHub : Hub
{
    public async Task JoinGroup(string group)
        => await Groups.AddToGroupAsync(Context.ConnectionId, group);

    public async Task LeaveGroup(string group)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
}
```

#### Gruplar

| Grup Adı | Kapsam |
|---|---|
| `admins` | Tüm Admin ve SuperAdmin kullanıcılar |
| `customer_{id}` | Belirli bir müşteri firmasına ait kullanıcılar |
| *(tüm istemciler)* | `Clients.All` — duyuru bildirimleri için |

#### Tetiklenen Olaylar

| Olay | Tetikleyen Eylem | Alıcı Gruplar |
|---|---|---|
| `TicketCreated` | Yeni ticket oluşturuldu | `admins`, `customer_{id}` |
| `TicketUpdated` | Ticket durumu veya ataması değişti | `admins`, `customer_{id}` |
| `CommentAdded` | Yeni yorum eklendi | `admins`, `customer_{id}` |
| `FileAdded` | Yeni dosya yüklendi | `admins`, `customer_{id}` |
| `AnnouncementCreated` | Yeni duyuru yayınlandı | Tüm bağlı istemciler |

#### İstemci Tarafı Bağlantı Örneği (JavaScript)

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/ticketHub")
    .withAutomaticReconnect()
    .build();

// Gruplara katıl
await connection.start();
await connection.invoke("JoinGroup", "admins");
await connection.invoke("JoinGroup", `customer_${companyId}`);

// Olayları dinle
connection.on("TicketCreated", (data) => {
    showNotification(`Yeni ticket: ${data.ticketNo} — ${data.title}`);
});

connection.on("CommentAdded", (data) => {
    refreshNotifications();
});

connection.on("AnnouncementCreated", (data) => {
    showAnnouncementBanner(data);
});
```

#### Program.cs Kayıt

```csharp
// API tarafı
builder.Services.AddSignalR();
app.MapHub<TicketHub>("/ticketHub");
```

---

### 📢 Duyuru Modülü
Admin ve SuperAdmin tarafından tüm sisteme yayınlanan, öncelik sıralı, dosya destekli duyuru sistemidir.

**Temel Özellikler:**

- Duyurular `Priority` alanına göre sıralanır (yüksek öncelikli duyurular üstte görünür).
- Her duyuruya birden fazla dosya eklenebilir (PDF, Word, Excel, resim vb.).
- Kullanıcılar **"Tekrar gösterme"** seçeneğiyle duyuruyu kendi hesaplarında kapatabilir — `AnnouncementDismissals` tablosu bu durumu takip eder.
- `IsActive` toggle'ı ile duyuru geçici olarak yayından kaldırılabilir.
- Yeni duyuru oluşturulduğunda **SignalR** ile tüm bağlı istemcilere anlık bildirim gönderilir.
- Admin kendi oluşturduğu duyuruları yönetebilir; SuperAdmin tüm duyurulara tam erişime sahiptir.

**Veritabanı Tabloları:**

| Tablo | Açıklama |
|---|---|
| `Announcements` | Duyuru başlık, içerik, öncelik, aktiflik |
| `AnnouncementFiles` | Duyuruya eklenen dosyalar (soft-delete destekli) |
| `AnnouncementDismissals` | Kullanıcı bazında kapatma kayıtları (UNIQUE constraint) |

**Duyuru API Akışı:**

```
Admin duyuru oluşturur
        │
        ▼
POST /api/announcement  →  DB'ye kayıt + dosyalar wwwroot/uploads/announcements/ altına
        │
        ▼
SignalR: Clients.All.SendAsync("AnnouncementCreated", { id, title, priority })
        │
        ▼
Tüm aktif kullanıcıların ekranında anlık duyuru banner'ı
        │
  Kullanıcı "Tekrar gösterme" tıklar
        │
        ▼
POST /api/announcement/{id}/dismiss  →  AnnouncementDismissals tablosuna kayıt
```

---

### 📚 Bilgi Bankası Modülü
Teknik makale ve çözüm rehberlerinin yönetildiği modüldür.

- Makaleler **kategori** (Kurulum, Hata Çözümü, Güncelleme vb.) ve **ürün** bazında filtrelenebilir.
- Her makaleye kod bloğu (dil seçimiyle birlikte) ve dosya eki eklenebilir.
- `IsPublic` ve `IsActive` toggle'ları ile görünürlük yönetimi yapılır.
- Admin yalnızca kendi oluşturduğu makaleyi düzenleyebilir; SuperAdmin kısıtsızdır.
- User rolü yalnızca kendi şirketinin ürünlerine ait makaleleri görebilir.

---

### 👥 Kullanıcı & Müşteri Modülü
Sistem kullanıcıları ve müşteri firmalarının yönetildiği modüldür.

- Kullanıcılar `User`, `Admin` veya `SuperAdmin` rolüyle tanımlanır.
- Müşterilere Logo ürünleri atanır; ticket ve bilgi bankası kayıtları bu ürünlerle ilişkilendirilir.
- Müşteri bazında sözleşme tarihi ve ticket kotası tanımlanabilir.

---

### 📨 Mail Ayarları Modülü
Sistem tarafından gönderilecek maillerin SMTP yapılandırmasını içerir.

- SMTP host, port, SSL ve kimlik bilgileri tek ekrandan yönetilir.
- Gmail, Outlook gibi popüler sağlayıcılar için hızlı doldur seçenekleri mevcuttur.
- **Mevcut bir kayıt güncellenirken önce test maili gönderilmesi zorunludur** — test başarılı olmadan kaydet butonu aktif olmaz.

---

### 🔔 Bildirim Sistemi (Panel İçi)

Panel açıldığında iki ayrı istek ile kişisel bildirimler çekilir; SignalR üzerinden gerçek zamanlı güncellenir.

| Bildirim Tipi | Açıklama |
|---|---|
| Kişisel Ticketlar | Kullanıcının üzerindeki açık ve bekleyen ticketlar |
| Firma Aktiviteleri | Firmanın ticketlarına gelen yorum ve dosya yüklemeleri |

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

Duyuru modülü için aşağıdaki tabloların mevcut olduğundan emin olun:

```sql
CREATE TABLE Announcements (
    ID              INT IDENTITY PRIMARY KEY,
    Title           NVARCHAR(200)  NOT NULL,
    Content         NVARCHAR(MAX)  NOT NULL,
    Priority        TINYINT        NOT NULL DEFAULT 1,  -- 1=Düşük 2=Normal 3=Yüksek
    CreatedByUserID INT            NOT NULL,
    CreatedDate     DATETIME       NOT NULL DEFAULT GETDATE(),
    UpdatedDate     DATETIME       NULL,
    IsActive        BIT            NOT NULL DEFAULT 1,
    IsDeleted       BIT            NOT NULL DEFAULT 0
);

CREATE TABLE AnnouncementFiles (
    ID               INT IDENTITY PRIMARY KEY,
    AnnouncementID   INT            NOT NULL REFERENCES Announcements(ID),
    OriginalFileName NVARCHAR(260)  NOT NULL,
    StoredFileName   NVARCHAR(260)  NOT NULL,
    RelativePath     NVARCHAR(500)  NOT NULL,
    FileExtension    NVARCHAR(20)   NOT NULL,
    MimeType         NVARCHAR(100)  NOT NULL,
    FileSizeBytes    BIGINT         NOT NULL,
    UploadedByUserID INT            NOT NULL,
    UploadedDate     DATETIME       NOT NULL DEFAULT GETDATE(),
    IsDeleted        BIT            NOT NULL DEFAULT 0
);

CREATE TABLE AnnouncementDismissals (
    AnnouncementID INT      NOT NULL,
    UserID         INT      NOT NULL,
    DismissedDate  DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Dismissal UNIQUE (AnnouncementID, UserID)
);
```

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

> ⚠️ `SecretKey` değerini production ortamında mutlaka güçlü ve rastgele bir değerle değiştirin. `appsettings.json` dosyasını `.gitignore` ile versiyon kontrolüne dahil etmemeye dikkat edin.

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
| `GET` | `/api/ticket` | Ticket listesi (açık) | Tümü |
| `GET` | `/api/ticket/search` | Gelişmiş filtreli arama (tüm statuslar) | Tümü |
| `GET` | `/api/ticket/{id}` | Ticket detayı | Tümü |
| `POST` | `/api/ticket` | Yeni ticket oluştur | User, SuperAdmin |
| `PATCH` | `/api/ticket/{id}/status` | Durum güncelle | Admin+ |
| `PATCH` | `/api/ticket/{id}/assign` | Ticket devret | Admin+ |
| `POST` | `/api/ticket/{id}/files` | Dosya yükle | Tümü |
| `GET` | `/api/ticket/files/{fileId}/download` | Dosya indir | Tümü |
| `POST` | `/api/ticket/{id}/comments` | Yorum ekle | Tümü |
| `GET` | `/api/ticket/{id}/comments` | Yorumları listele | Tümü |
| `GET` | `/api/ticket/my-notifications` | Kişisel bildirimler | Tümü |
| `GET` | `/api/ticket/company-notifications` | Firma bildirimleri | Tümü |
| `GET` | `/api/ticket/superadmin-dashboard` | SuperAdmin dashboard | SuperAdmin |
| `GET` | `/api/ticket/admin-dashboard` | Admin dashboard | Admin+ |
| `GET` | `/api/ticket/user-dashboard` | User dashboard | Tümü |
| `GET` | `/api/ticket/superadmin-report` | SuperAdmin detaylı rapor | SuperAdmin |
| `GET` | `/api/ticket/admin-report` | Admin kişisel rapor | Admin+ |

### 📢 Duyuru

| Method | Endpoint | Açıklama | Rol |
|---|---|---|---|
| `GET` | `/api/announcement` | Kullanıcıya görünen duyurular (dismiss edilmemiş) | Tümü |
| `GET` | `/api/announcement/all` | Tüm duyurular | Admin+ |
| `POST` | `/api/announcement` | Duyuru oluştur (dosya ile) + SignalR | Admin+ |
| `PUT` | `/api/announcement/{id}` | Duyuru güncelle | Admin+ |
| `PATCH` | `/api/announcement/{id}/toggle` | Aktif/Pasif toggle | Admin+ |
| `DELETE` | `/api/announcement/{id}` | Duyuru sil (soft) | Admin+ |
| `POST` | `/api/announcement/{id}/dismiss` | "Tekrar gösterme" | Tümü |
| `DELETE` | `/api/announcement/file/{fileId}` | Dosya sil | Admin+ |
| `GET` | `/api/announcement/file/{fileId}/download` | Dosya indir | Tümü |

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
| `SuperAdmin` | Tüm kayıtlara tam erişim, kullanıcı yönetimi, iptal işlemi |
| `Admin` | Kendi kayıtlarını yönetebilir, müşteri işlemleri, duyuru yönetimi |
| `User` | Yalnızca kendi şirketinin verilerini görebilir, ticket açabilir |

---

## 🛠️ Teknoloji Yığını

| Teknoloji | Kullanım Amacı |
|---|---|
| ![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?logo=dotnet) | Web API & MVC framework |
| ![SignalR](https://img.shields.io/badge/SignalR-Real--Time-FF6F61) | Gerçek zamanlı WebSocket bildirimleri |
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
  <img src="https://img.shields.io/badge/SignalR-Real--Time-FF6F61" alt="signalr" />
  <img src="https://img.shields.io/badge/MVC-Razor_Views-68217A?logo=dotnet" alt="mvc" />
  <img src="https://img.shields.io/badge/Dapper-SQL_Server-blue" alt="dapper" />
  <img src="https://img.shields.io/badge/JWT-Authentication-000000?logo=jsonwebtokens" alt="jwt" />
</p>
