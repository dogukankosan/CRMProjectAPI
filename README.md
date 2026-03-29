# 🏢 CRMProjectAPI

![License](https://img.shields.io/github/license/dogukankosan/CRMProjectAPI)
![Stars](https://img.shields.io/github/stars/dogukankosan/CRMProjectAPI)
![Issues](https://img.shields.io/github/issues/dogukankosan/CRMProjectAPI)
![Last Commit](https://img.shields.io/github/last-commit/dogukankosan/CRMProjectAPI)

> **CRMProjectAPI**, müşteri ilişkileri yönetimi için geliştirilmiş; ticket takibi, bilgi bankası, kullanıcı yönetimi ve SMTP entegrasyonu içeren modern bir ASP.NET Core tabanlı CRM sistemidir.

---

## 🚀 Özellikler

- 🎫 **Ticket Yönetimi** — Açma, atama, önceliklendirme ve durum takibi
- 📚 **Bilgi Bankası** — Kategori & ürün bazlı makale sistemi, dosya ekleri ile
- 👥 **Kullanıcı & Müşteri Yönetimi** — Rol bazlı erişim kontrolü (User / Admin / SuperAdmin)
- 🔔 **Anlık Bildirimler** — Yeni yorum ve dosya yükleme bildirimleri
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
├── CRMProjectAPI/                  # Backend — ASP.NET Core Web API
│   ├── Controllers/                # API endpoint'leri
│   │   ├── AdminTicketController   # Ticket CRUD & atama işlemleri
│   │   ├── AdminUserController     # Kullanıcı yönetimi
│   │   ├── AdminCustomerController # Müşteri yönetimi
│   │   ├── AdminBilgiBankasiController # Bilgi bankası makaleleri
│   │   └── TicketController        # Kullanıcı taraflı ticket işlemleri
│   ├── Data/
│   │   └── DapperContext.cs        # SQL Server bağlantı yönetimi (Dapper)
│   ├── Services/
│   │   └── IJwtService.cs          # JWT üretim & doğrulama servisi
│   └── Validations/
│       └── KnowledgeBaseValidation # İş kuralı doğrulamaları
│
└── CRMProjectUI/                   # Frontend — ASP.NET Core MVC
    ├── Controllers/                # MVC controller'ları
    ├── Views/
    │   ├── Shared/
    │   │   └── _adminLayout.cshtml # Ana layout (bildirimler, SEO, dinamik marka)
    │   ├── Ticket/
    │   │   └── Ticketform.cshtml   # Ticket oluşturma formu (AJAX, sözleşme kontrolü)
    │   └── AdminMail/
    │       └── Index.cshtml        # SMTP yapılandırma ekranı
    ├── APIService/                 # API'ye HTTP isteklerini yöneten servisler
    └── Models/                     # DTO ve ViewModel sınıfları
```

### Mimari Akış

```
Kullanıcı (Tarayıcı)
    │
    ▼
CRMProjectUI  (MVC — Razor Views)
    │  JWT Token ile HTTP isteği
    ▼
CRMProjectAPI (ASP.NET Core Web API)
    │  Dapper ile sorgu
    ▼
SQL Server Veritabanı
```

---

## 🛠️ Teknoloji Yığını

| Teknoloji | Kullanım Amacı |
|---|---|
| ![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-512BD4?logo=dotnet) | Web API & MVC framework |
| ![Dapper](https://img.shields.io/badge/Dapper-ORM-blue) | Hafif SQL erişim katmanı |
| ![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?logo=microsoftsqlserver&logoColor=white) | İlişkisel veritabanı |
| ![JWT](https://img.shields.io/badge/JWT-Auth-000000?logo=jsonwebtokens) | Token tabanlı kimlik doğrulama |
| ![Bootstrap](https://img.shields.io/badge/Bootstrap-4-7952B3?logo=bootstrap&logoColor=white) | UI bileşenleri |
| ![SweetAlert2](https://img.shields.io/badge/SweetAlert2-Notifications-ff6384) | Kullanıcı bildirimleri |
| ![DataTables](https://img.shields.io/badge/DataTables-Grid-003865) | Tablo & listeleme bileşeni |

---

## 🔐 Rol Sistemi

| Rol | Yetki |
|---|---|
| `SuperAdmin` | Tüm kayıtlara tam erişim |
| `Admin` | Kendi kayıtlarını yönetebilir |
| `User` | Yalnızca kendi şirketinin verilerini görebilir |

---

## 🤝 Katkı

Katkı sağlamak için projeyi forklayabilir ve pull request gönderebilirsiniz.

1. `git fork` ile projeyi çatallayın
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
