using CRMProjectAPI.Data;
using CRMProjectAPI.Helpers;
using CRMProjectAPI.Models;
using CRMProjectAPI.Validations;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Security.Cryptography;

namespace CRMProjectAPI.Controllers
{
    [ApiController]
    [Route("api/customer")]
    [Authorize] // Tüm controller login zorunlu
    public class CustomerController : ControllerBase
    {
        private readonly DapperContext _context;
        private readonly IWebHostEnvironment _env;

        public CustomerController(DapperContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ── Yardımcı: JWT'den companyId ve role al ──────────────────────────
        private int GetCompanyId() =>
            int.TryParse(User.FindFirst("companyId")?.Value, out int cid) ? cid : 0;

        private bool IsSuperAdmin() => User.IsInRole("SuperAdmin");
        private bool IsAdmin() => User.IsInRole("Admin") || IsSuperAdmin();
        private bool IsUser() => User.IsInRole("User");

        // ────────────────────────────────────────────────────────────────────
        #region Customer CRUD
        /// <summary>
        /// Müşteri detay sayfası için tüm veriler — Admin/SuperAdmin
        /// </summary>
        [HttpGet("{id:int}/detail")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetDetail(int id)
        {
            using var connection = _context.CreateConnection();

            // Müşteri bilgileri
            const string customerSql = @"
        SELECT c.*, cd.Il, cd.Ilce, cd.PostaKodu
        FROM Customers c WITH (NOLOCK)
        LEFT JOIN tCity_District_Street_Town cd WITH (NOLOCK) ON c.CityDistrictID = cd.ID
        WHERE c.ID = @ID
    ";
            var customer = await connection.QueryFirstOrDefaultAsync<CustomerDto>(
                customerSql, new { ID = id });
            if (customer == null)
                return NotFound(ApiResponse.NotFound("Müşteri bulunamadı"));

            // Logo ürünleri
            const string productsSql = @"
        SELECT clp.LogoProductID AS ID, lp.LogoProductName
        FROM CustomersLogoProducts clp WITH (NOLOCK)
        INNER JOIN LogoProducts lp WITH (NOLOCK) ON clp.LogoProductID = lp.ID
        WHERE clp.CustomerID = @CustomerID
    ";
            customer.LogoProducts = (await connection.QueryAsync<LogoProductDto>(
                productsSql, new { CustomerID = id })).ToList();

            // Kullanıcılar
            const string usersSql = @"
        SELECT ID, FullName, Username, EMailAddress, PhoneNumber,
               ISAdmin, Status, Picture
        FROM Users WITH (NOLOCK)
        WHERE CompanyID = @CustomerID
        ORDER BY ISAdmin DESC, FullName ASC
    ";
            var users = (await connection.QueryAsync(usersSql, new { CustomerID = id })).ToList();

            // Ticket istatistikleri
            const string ticketStatsSql = @"
        SELECT
            COUNT(*)                                              AS Total,
            SUM(CASE WHEN Status IN (0,1,4,5) THEN 1 ELSE 0 END) AS OpenCount,
            SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END)          AS Resolved,
            SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END)          AS Failed,
            SUM(CASE WHEN Status = 6 THEN 1 ELSE 0 END)          AS Cancelled,
            ISNULL(AVG(CAST(WorkingMinute AS FLOAT)), 0)          AS AvgWorkingMinute
        FROM Tickets WITH (NOLOCK)
        WHERE CustomerID = @CustomerID AND IsDeleted = 0
    ";
            var ticketStats = await connection.QueryFirstOrDefaultAsync(
                ticketStatsSql, new { CustomerID = id });

            // Son 10 ticket
            const string recentTicketsSql = @"
        SELECT TOP 10
            t.ID, t.TicketNo, t.Title, t.Priority, t.Status,
            t.OpenedDate, t.WorkingMinute,
            lp.LogoProductName,
            ISNULL(cu.FullName, cu.Username) AS CreatedByName,
            ISNULL(au.FullName, au.Username) AS AssignedToName
        FROM Tickets t WITH (NOLOCK)
        LEFT JOIN LogoProducts lp WITH (NOLOCK) ON t.LogoProductID    = lp.ID
        LEFT JOIN Users        cu WITH (NOLOCK) ON t.CreatedByUserID  = cu.ID
        LEFT JOIN Users        au WITH (NOLOCK) ON t.AssignedToUserID = au.ID
        WHERE t.CustomerID = @CustomerID AND t.IsDeleted = 0
        ORDER BY t.OpenedDate DESC
    ";
            var recentTickets = (await connection.QueryAsync<TicketListDto>(
                recentTicketsSql, new { CustomerID = id })).ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                Customer = customer,
                Users = users,
                TicketStats = ticketStats,
                RecentTickets = recentTickets
            }));
        }
        [HttpGet("{id:int}/ticket-eligibility")]
        public async Task<IActionResult> GetTicketEligibility(int id)
        {
            if (IsUser() && GetCompanyId() != id)
                return Forbid();

            const string sql = @"
        SELECT 
            TicketCount,
            ContractEndDate,
            Status,
            CASE WHEN ContractEndDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END AS IsContractExpired,
            CASE WHEN TicketCount <= 0 THEN 1 ELSE 0 END                          AS IsTicketExhausted,
            ISNULL(DATEDIFF(DAY, GETDATE(), ContractEndDate), 999)                AS DaysLeft
        FROM Customers WITH (NOLOCK)
        WHERE ID = @ID
    ";
            using var connection = _context.CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync(sql, new { ID = id });
            if (result == null)
                return NotFound(ApiResponse.NotFound("Firma bulunamadı"));

            return Ok(ApiResponse<object>.Ok(result));
        }
        /// <summary>
        /// Kod kontrolü — Admin ve SuperAdmin
        /// </summary>
        [HttpGet("check-code")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> CheckCode([FromQuery] string code, [FromQuery] int excludeId = 0)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Ok(ApiResponse<bool>.Ok(false));

            const string sql = @"
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Customers WITH (NOLOCK)
                    WHERE CustomerCode = @Code
                    AND DeletedDate IS NULL
                    AND (@ExcludeId = 0 OR ID != @ExcludeId)
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
            ";
            using var connection = _context.CreateConnection();
            bool exists = await connection.QuerySingleAsync<bool>(sql, new { Code = code, ExcludeId = excludeId });
            return Ok(ApiResponse<bool>.Ok(exists));
        }

        /// <summary>
        /// Tüm müşteri listesi — Admin/SuperAdmin.
        /// User kendi firmasına yönlendirilmeli, bu endpoint'e erişemez.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> List()
        {
            const string sql = @"
    SELECT 
        c.ID, c.CustomerCode, c.CustomerName, c.ShortName, c.CustomerType,
        c.VKN, c.TC, c.OfficialName, c.Phone1, c.CompanyEmail,
        c.Importance, c.TicketCount, c.Status, c.ContractEndDate, c.CreatedDate,
        c.LogoWebServiceUserName AS WsUsername,
        c.LogoWebServicePassword AS WsPassword,
c.BulutERPUsername, c.BulutERPPassword,
        cd.Il, cd.Ilce
    FROM Customers c WITH (NOLOCK)
    LEFT JOIN tCity_District_Street_Town cd WITH (NOLOCK) ON c.CityDistrictID = cd.ID
    ORDER BY 
        CASE WHEN c.ContractEndDate <= DATEADD(DAY, 30, GETDATE()) AND c.ContractEndDate >= GETDATE() THEN 0
             WHEN c.ContractEndDate < GETDATE() THEN 1
             ELSE 2 END ASC,
        c.ID DESC
";
            using var connection = _context.CreateConnection();
            var customers = await connection.QueryAsync<CustomerListDto>(sql);
            return Ok(ApiResponse<IEnumerable<CustomerListDto>>.Ok(customers));
        }

        /// <summary>
        /// Aktif müşteri listesi — Admin/SuperAdmin
        /// </summary>
        [HttpGet("active")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> ListActive()
        {
            const string sql = @"
                SELECT 
                    c.ID, c.CustomerCode, c.CustomerName, c.ShortName, c.CustomerType,
                    c.VKN, c.TC, c.OfficialName, c.Phone1, c.CompanyEmail,
                    c.Importance, c.TicketCount, c.Status, c.ContractEndDate, c.CreatedDate,
                    cd.Il, cd.Ilce
                FROM Customers c WITH (NOLOCK)
                LEFT JOIN tCity_District_Street_Town cd WITH (NOLOCK) ON c.CityDistrictID = cd.ID
                WHERE c.Status = 1
                ORDER BY c.CustomerName ASC
            ";
            using var connection = _context.CreateConnection();
            var customers = await connection.QueryAsync<CustomerListDto>(sql);
            return Ok(ApiResponse<IEnumerable<CustomerListDto>>.Ok(customers));
        }

        /// <summary>
        /// Select list (dropdown için) — Admin/SuperAdmin
        /// </summary>
        [HttpGet("select")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> GetSelectList()
        {
            const string sql = @"
                SELECT ID, CustomerName, CustomerCode, VKN
                FROM Customers WITH (NOLOCK)
                WHERE Status = 1
                ORDER BY CustomerName ASC
            ";
            using var connection = _context.CreateConnection();
            var customers = await connection.QueryAsync<CustomerSelectDto>(sql);
            return Ok(ApiResponse<IEnumerable<CustomerSelectDto>>.Ok(customers));
        }

        /// <summary>
        /// Müşteri detay:
        ///   - SuperAdmin/Admin → herkese erişebilir
        ///   - User → sadece kendi firması (CompanyID eşleşmeli)
        ///   - User'a contract ve dosya bilgileri dönmez
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            // User ise sadece kendi firmasına erişebilir
            if (IsUser() && !IsSuperAdmin() && !IsAdmin())
            {
                int companyId = GetCompanyId();
                if (companyId != id)
                    return Forbid();
            }

            const string sql = @"
                SELECT 
                    c.*,
                    cd.Il, cd.Ilce, cd.PostaKodu
                FROM Customers c WITH (NOLOCK)
                LEFT JOIN tCity_District_Street_Town cd WITH (NOLOCK) ON c.CityDistrictID = cd.ID
                WHERE c.ID = @ID
            ";
            using var connection = _context.CreateConnection();
            var customer = await connection.QueryFirstOrDefaultAsync<CustomerDto>(sql, new { ID = id });
            if (customer == null)
                return NotFound(ApiResponse.NotFound("Müşteri bulunamadı"));

            // Logo ürünleri — herkese açık
            const string productsSql = @"
                SELECT clp.LogoProductID AS ID, lp.LogoProductName
                FROM CustomersLogoProducts clp WITH (NOLOCK)
                INNER JOIN LogoProducts lp WITH (NOLOCK) ON clp.LogoProductID = lp.ID
                WHERE clp.CustomerID = @CustomerID
            ";
            customer.LogoProducts = (await connection.QueryAsync<LogoProductDto>(
                productsSql, new { CustomerID = id })).ToList();
            customer.LogoProductIDs = customer.LogoProducts.Select(x => (int)x.ID).ToList();

            // Dosyalar — sadece SuperAdmin
            if (IsSuperAdmin())
            {
                const string filesSql = @"
                    SELECT 
                        cf.*,
                        u.FullName AS UploadedByName
                    FROM CustomerFiles cf WITH (NOLOCK)
                    LEFT JOIN Users u WITH (NOLOCK) ON cf.UploadedBy = u.ID
                    WHERE cf.CustomerID = @CustomerID AND cf.IsDeleted = 0
                    ORDER BY cf.UploadedDate DESC
                ";
                customer.Files = (await connection.QueryAsync<CustomerFileDto>(
                    filesSql, new { CustomerID = id })).ToList();
            }
            else
            {
                // Admin ve User dosyaları göremez
                customer.Files = new List<CustomerFileDto>();
            }

            // Contract alanlarını User ve Admin için gizle
            if (!IsSuperAdmin())
            {
                customer.ContractPath = null;
                customer.ContractStartDate = null;
                customer.ContractEndDate = null;
                // BulutERP: SA + Admin görür, User göremez
            }

            if (IsUser() && !IsAdmin())
            {
                customer.BulutERPUsername = null;
                customer.BulutERPPassword = null;
            }
            return Ok(ApiResponse<CustomerDto>.Ok(customer));
        }

        /// <summary>
        /// Müşteri oluştur — Admin ve SuperAdmin
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Create([FromBody] CustomerDto dto)
        {
            var validationErrors = CustomerValidation.Validate(dto, IsSuperAdmin(), isEdit: false);


            if (validationErrors.Any())
                return BadRequest(ApiResponse.Fail(validationErrors));

            const string sql = @"
                INSERT INTO Customers (
                    CustomerCode, CustomerName, ShortName, CustomerType,
                    VKN, TC, OfficialName, OfficialTitle, OfficialPhone, OfficialEmail,
                    CompanyEmail, Phone1, Phone2, CityDistrictID, Address,
                    Importance, TicketCount, LogoWebServiceUserName, LogoWebServicePassword,
                    SQLPassword, ContractPath, ContractStartDate, ContractEndDate,
                    InternalNotes, Status, CreatedBy, CreatedDate,
BulutERPUsername, BulutERPPassword
                ) VALUES (
                    @CustomerCode, @CustomerName, @ShortName, @CustomerType,
                    @VKN, @TC, @OfficialName, @OfficialTitle, @OfficialPhone, @OfficialEmail,
                    @CompanyEmail, @Phone1, @Phone2, @CityDistrictID, @Address,
                    @Importance, @TicketCount, @LogoWebServiceUserName, @LogoWebServicePassword,
                    @SQLPassword, @ContractPath, @ContractStartDate, @ContractEndDate,
                    @InternalNotes, @Status, @CreatedBy, GETDATE(),@BulutERPUsername, @BulutERPPassword
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);
            ";
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                int newId = await connection.QuerySingleAsync<int>(sql, dto, transaction);
                if (dto.LogoProductIDs != null && dto.LogoProductIDs.Any())
                    await UpdateCustomerLogoProducts(connection, transaction, newId, dto.LogoProductIDs);
                transaction.Commit();
                return Ok(ApiResponse<int>.Ok(newId, "Müşteri başarıyla eklendi"));
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Sözleşme yükle — sadece SuperAdmin
        /// </summary>
        [HttpPost("{id:int}/contract")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UploadContract(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Dosya seçilmedi"));

            string[] allowed = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension))
                return BadRequest(ApiResponse.Fail("Desteklenmeyen dosya formatı"));
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Dosya boyutu 10MB'dan büyük olamaz"));

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string folderPath = Path.Combine(webRoot, "uploads", "contracts");
            Directory.CreateDirectory(folderPath);
            string fileName = $"{Guid.NewGuid():N}{extension}";
            string fullPath = Path.Combine(folderPath, fileName);
            string relativePath = $"/uploads/contracts/{fileName}";

            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);

            const string selectSql = "SELECT ContractPath FROM Customers WHERE ID = @ID";
            using var connection = _context.CreateConnection();
            string? oldPath = await connection.QueryFirstOrDefaultAsync<string>(selectSql, new { ID = id });
            if (!string.IsNullOrEmpty(oldPath))
            {
                string oldFullPath = Path.Combine(webRoot, oldPath.TrimStart('/'));
                if (System.IO.File.Exists(oldFullPath))
                    System.IO.File.Delete(oldFullPath);
            }

            await connection.ExecuteAsync(
                "UPDATE Customers SET ContractPath = @Path, UpdatedDate = GETDATE() WHERE ID = @ID",
                new { Path = relativePath, ID = id });

            return Ok(ApiResponse<string>.Ok(relativePath, "Sözleşme yüklendi"));
        }

        /// <summary>
        /// Müşteri güncelle — Admin ve SuperAdmin
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> Update(int id, [FromBody] CustomerDto dto)
        {
            dto.ID = id; 
            // ── ContractPath 'DELETE' gelirse fiziksel dosyayı sil ──
            if (dto.ContractPath == "DELETE")
            {
                using var connTemp = _context.CreateConnection();
                string? existingPath = await connTemp.QueryFirstOrDefaultAsync<string>(
                    "SELECT ContractPath FROM Customers WHERE ID = @ID", new { ID = id });
                if (!string.IsNullOrEmpty(existingPath))
                {
                    string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string fullPath = Path.Combine(webRoot, existingPath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                        System.IO.File.Delete(fullPath);
                }
            }

            var validationErrors = CustomerValidation.Validate(dto, IsSuperAdmin(), isEdit: true);


            if (validationErrors.Any())
                return BadRequest(ApiResponse.Fail(validationErrors));
            const string sql = @"
    UPDATE Customers SET
        CustomerCode              = @CustomerCode,
        CustomerName              = @CustomerName,
        ShortName                 = @ShortName,
        CustomerType              = @CustomerType,
        VKN                       = @VKN,
        TC                        = @TC,
        OfficialName              = @OfficialName,
        OfficialTitle             = @OfficialTitle,
        OfficialPhone             = @OfficialPhone,
        OfficialEmail             = @OfficialEmail,
        CompanyEmail              = @CompanyEmail,
        Phone1                    = @Phone1,
        Phone2                    = @Phone2,
        CityDistrictID            = @CityDistrictID,
        Address                   = @Address,
        Importance                = @Importance,
        TicketCount               = @TicketCount,
        LogoWebServiceUserName    = @LogoWebServiceUserName,
BulutERPUsername          = @BulutERPUsername,
BulutERPPassword          = CASE 
                                WHEN @BulutERPPassword = 'DELETE' THEN NULL
                                WHEN @BulutERPPassword IS NULL OR @BulutERPPassword = '' THEN BulutERPPassword
                                ELSE @BulutERPPassword END,
LogoWebServicePassword    = CASE 
                                WHEN @LogoWebServicePassword = 'DELETE' THEN NULL
                                WHEN @LogoWebServicePassword IS NULL OR @LogoWebServicePassword = '' THEN LogoWebServicePassword
                                ELSE @LogoWebServicePassword END,
SQLPassword               = CASE 
                                WHEN @SQLPassword = 'DELETE' THEN NULL
                                WHEN @SQLPassword IS NULL OR @SQLPassword = '' THEN SQLPassword
                                ELSE @SQLPassword END,
        ContractPath              = CASE 
                                        WHEN @ContractPath = 'DELETE' THEN NULL
                                        WHEN @ContractPath IS NULL OR @ContractPath = '' THEN ContractPath
                                        ELSE @ContractPath 
                                    END,
        ContractStartDate         = CASE WHEN @ContractStartDate IS NULL
                                        THEN ContractStartDate
                                        ELSE @ContractStartDate END,
        ContractEndDate           = CASE WHEN @ContractEndDate IS NULL
                                        THEN ContractEndDate
                                        ELSE @ContractEndDate END,
        InternalNotes             = @InternalNotes,
        Status                    = @Status,
        UpdatedBy                 = @UpdatedBy,
        UpdatedDate               = GETDATE()
    WHERE ID = @ID
";
            using var connection = _context.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                int affectedRows = await connection.ExecuteAsync(sql, dto, transaction);
                if (affectedRows == 0)
                {
                    transaction.Rollback();
                    return NotFound(ApiResponse.NotFound("Müşteri bulunamadı"));
                }
                if (dto.LogoProductIDs != null)
                    await UpdateCustomerLogoProducts(connection, transaction, id, dto.LogoProductIDs);
                transaction.Commit();
                return Ok(ApiResponse.Ok("Müşteri başarıyla güncellendi"));
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Müşteri sil — sadece SuperAdmin
        /// Kullanıcısı varsa, ticketi varsa veya dosyası varsa silinemez.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            using var connection = _context.CreateConnection();

            // ── Bağlı kayıt kontrolleri ──────────────────────────────────────
            const string checkSql = @"
                SELECT
                    (SELECT COUNT(*) FROM Users
                     WHERE CompanyID = @ID) AS TotalUsers,

                    (SELECT COUNT(*) FROM Tickets
                     WHERE CustomerID = @ID AND IsDeleted = 0) AS TotalTickets,

                    (SELECT COUNT(*) FROM CustomerFiles
                     WHERE CustomerID = @ID AND IsDeleted = 0) AS TotalFiles
            ";
            var counts = await connection.QueryFirstOrDefaultAsync(checkSql, new { ID = id });

            if (counts != null)
            {
                int totalUsers = (int)counts.TotalUsers;
                int totalTickets = (int)counts.TotalTickets;
                int totalFiles = (int)counts.TotalFiles;

                List<string> reasons = new();
                if (totalUsers > 0) reasons.Add($"{totalUsers} kullanıcı");
                if (totalTickets > 0) reasons.Add($"{totalTickets} destek talebi");
                if (totalFiles > 0) reasons.Add($"{totalFiles} dosya");

                if (reasons.Any())
                    return BadRequest(ApiResponse.Fail(
                        $"Müşteri silinemez. Bağlı kayıtlar mevcut: {string.Join(", ", reasons)}. " +
                        "Silmeden önce bağlı tüm kayıtları kaldırın."));
            }

            // ── Fiziksel dosyaları sil (CustomerFiles zaten 0 ama güvenlik için) ──
            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string customerFolder = Path.Combine(webRoot, "uploads", "customers", id.ToString());
            if (Directory.Exists(customerFolder))
                Directory.Delete(customerFolder, recursive: true);

            // ── Sözleşme dosyasını sil ───────────────────────────────────────
            string? contractPath = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT ContractPath FROM Customers WHERE ID = @ID", new { ID = id });
            if (!string.IsNullOrEmpty(contractPath))
            {
                string contractFullPath = Path.Combine(webRoot, contractPath.TrimStart('/'));
                if (System.IO.File.Exists(contractFullPath))
                    System.IO.File.Delete(contractFullPath);
            }

            // ── Logo ürün ilişkilerini sil ───────────────────────────────────
            await connection.ExecuteAsync(
                "DELETE FROM CustomersLogoProducts WHERE CustomerID = @ID", new { ID = id });

            // ── Müşteriyi sil ────────────────────────────────────────────────
            int affected = await connection.ExecuteAsync(
                "DELETE FROM Customers WHERE ID = @ID", new { ID = id });

            if (affected == 0)
                return NotFound(ApiResponse.NotFound("Müşteri bulunamadı"));

            return Ok(ApiResponse.Ok("Müşteri başarıyla silindi"));
        }

        /// <summary>
        /// Statü değiştir — sadece SuperAdmin
        /// </summary>
        [HttpPatch("{id:int}/status")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            const string sql = @"
                UPDATE Customers SET 
                    Status      = CASE WHEN Status = 1 THEN 0 ELSE 1 END,
                    UpdatedDate = GETDATE()
                WHERE ID = @ID;
                SELECT Status FROM Customers WHERE ID = @ID;
            ";
            using var connection = _context.CreateConnection();
            var newStatus = await connection.QueryFirstOrDefaultAsync<byte?>(sql, new { ID = id });
            if (newStatus == null)
                return NotFound(ApiResponse.NotFound("Müşteri bulunamadı"));
            return Ok(ApiResponse<byte>.Ok(newStatus.Value,
                newStatus.Value == 1 ? "Müşteri aktif edildi" : "Müşteri pasif edildi"));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Logo Products

        /// <summary>
        /// Logo ürün listesi — tüm roller görebilir
        /// </summary>
        [HttpGet("logo-products")]
        public async Task<IActionResult> GetLogoProducts()
        {
            const string sql = @"
                SELECT ID, LogoProductName
                FROM LogoProducts WITH (NOLOCK)
                WHERE IsActive = 1
                ORDER BY LogoProductName ASC
            ";
            using var connection = _context.CreateConnection();
            var products = await connection.QueryAsync<LogoProductDto>(sql);
            return Ok(ApiResponse<IEnumerable<LogoProductDto>>.Ok(products));
        }

        /// <summary>
        /// Müşterinin logo ürünleri:
        ///   - Admin/SuperAdmin → herkese
        ///   - User → sadece kendi firması
        /// </summary>
        [HttpGet("{id:int}/logo-products")]
        public async Task<IActionResult> GetCustomerLogoProducts(int id)
        {
            if (IsUser() && !IsAdmin())
            {
                if (GetCompanyId() != id)
                    return Forbid();
            }

            const string sql = @"
                SELECT clp.CustomerID, clp.LogoProductID, lp.LogoProductName,
                       clp.AssignedBy, clp.AssignedDate
                FROM CustomersLogoProducts clp WITH (NOLOCK)
                INNER JOIN LogoProducts lp WITH (NOLOCK) ON clp.LogoProductID = lp.ID
                WHERE clp.CustomerID = @CustomerID
            ";
            using var connection = _context.CreateConnection();
            var products = await connection.QueryAsync<CustomerLogoProductDto>(sql, new { CustomerID = id });
            return Ok(ApiResponse<IEnumerable<CustomerLogoProductDto>>.Ok(products));
        }

        private static async Task UpdateCustomerLogoProducts(
            IDbConnection connection,
            IDbTransaction transaction,
            int customerId,
            List<int> productIds)
        {
            await connection.ExecuteAsync(
                "DELETE FROM CustomersLogoProducts WHERE CustomerID = @CustomerID",
                new { CustomerID = customerId }, transaction);

            if (productIds.Any())
            {
                var rows = productIds.Select(pid => new { CustomerID = customerId, LogoProductID = pid });
                await connection.ExecuteAsync(
                    "INSERT INTO CustomersLogoProducts (CustomerID, LogoProductID) VALUES (@CustomerID, @LogoProductID)",
                    rows, transaction);
            }
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Customer Files — Sadece SuperAdmin

        /// <summary>
        /// Dosya listesi — sadece SuperAdmin
        /// </summary>
        [HttpGet("{id:int}/files")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetCustomerFiles(int id)
        {
            const string sql = @"
                SELECT cf.*, u.FullName AS UploadedByName
                FROM CustomerFiles cf WITH (NOLOCK)
                LEFT JOIN Users u WITH (NOLOCK) ON cf.UploadedBy = u.ID
                WHERE cf.CustomerID = @CustomerID AND cf.IsDeleted = 0
                ORDER BY cf.UploadedDate DESC
            ";
            using var connection = _context.CreateConnection();
            var files = await connection.QueryAsync<CustomerFileDto>(sql, new { CustomerID = id });
            return Ok(ApiResponse<IEnumerable<CustomerFileDto>>.Ok(files));
        }

        /// <summary>
        /// Dosya indir — sadece SuperAdmin
        /// </summary>
        [HttpGet("files/{fileId:int}/download")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DownloadFile(int fileId)
        {
            const string sql = "SELECT * FROM CustomerFiles WHERE ID = @ID AND IsDeleted = 0";
            using var connection = _context.CreateConnection();
            CustomerFileDto? file = await connection.QueryFirstOrDefaultAsync<CustomerFileDto>(sql, new { ID = fileId });
            if (file == null)
                return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string fullPath = Path.Combine(webRoot, file.RelativePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath))
                return NotFound(ApiResponse.NotFound("Dosya fiziksel olarak bulunamadı"));

            byte[] bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            string mimeType = file.MimeType ?? "application/octet-stream";
            string fileName = file.OriginalFileName ?? "dosya";
            Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            return File(bytes, mimeType, fileName);
        }

        [HttpPost("{id:int}/files")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UploadFile(
            int id,
            IFormFile file,
            [FromForm] string? description,
            [FromForm] string category = "Genel",
            [FromForm] string? tags = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Dosya seçilmedi"));

            HashSet<string> allowedExtensions = new()
        { ".pdf", ".xls", ".xlsx", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".txt", ".zip", ".rar" };
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(ApiResponse.Fail("Desteklenmeyen dosya formatı"));
            if (file.Length > 50 * 1024 * 1024)
                return BadRequest(ApiResponse.Fail("Dosya boyutu 50MB'dan büyük olamaz"));

            string fileType = extension switch
            {
                ".pdf" => "PDF",
                ".xls" or ".xlsx" => "Excel",
                ".doc" or ".docx" => "Word",
                ".jpg" or ".jpeg" or ".png"
                    or ".gif" or ".bmp" => "Image",
                ".txt" => "Text",
                ".zip" or ".rar" => "Archive",
                _ => "Other"
            };

            // Hash hesapla — dosyayı okumadan önce
            string fileHash;
            byte[] fileBytes;
            using (MemoryStream ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
                fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
            }

            using var connection = _context.CreateConnection();

            // ── Aynı isimde dosya var mı? ────────────────────────────────────────
            bool nameExists = await connection.ExecuteScalarAsync<bool>(@"
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM CustomerFiles
            WHERE CustomerID      = @CustomerID
              AND OriginalFileName = @OriginalFileName
              AND IsDeleted       = 0
        ) THEN 1 ELSE 0 END",
                new { CustomerID = id, OriginalFileName = file.FileName });

            if (nameExists)
                return BadRequest(ApiResponse.Fail($"'{file.FileName}' adında bir dosya zaten mevcut."));

            // ── Aynı içerikte dosya var mı? (hash kontrolü) ─────────────────────
            bool hashExists = await connection.ExecuteScalarAsync<bool>(@"
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM CustomerFiles
            WHERE CustomerID = @CustomerID
              AND FileHash   = @FileHash
              AND IsDeleted  = 0
        ) THEN 1 ELSE 0 END",
                new { CustomerID = id, FileHash = fileHash });

            if (hashExists)
                return BadRequest(ApiResponse.Fail("Bu dosya zaten yüklenmiş (aynı içerik mevcut)."));

            // ── Fiziksel kaydet ──────────────────────────────────────────────────
            string storedFileName = $"{Guid.NewGuid():N}{extension}";
            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string folderPath = Path.Combine(webRoot, "uploads", "customers", id.ToString());
            Directory.CreateDirectory(folderPath);
            string fullPath = Path.Combine(folderPath, storedFileName);

            try
            {
                await System.IO.File.WriteAllBytesAsync(fullPath, fileBytes);
            }
            catch
            {
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                throw;
            }

            // ── DB'ye kaydet ─────────────────────────────────────────────────────
            const string sql = @"
        INSERT INTO CustomerFiles (
            CustomerID, Category, OriginalFileName, StoredFileName, RelativePath,
            FileExtension, MimeType, FileSizeBytes, FileHash, FileType,
            Description, Tags, Version, IsDeleted, UploadedBy, UploadedDate
        ) VALUES (
            @CustomerID, @Category, @OriginalFileName, @StoredFileName, @RelativePath,
            @FileExtension, @MimeType, @FileSizeBytes, @FileHash, @FileType,
            @Description, @Tags, 1, 0, @UploadedBy, GETDATE()
        );
        SELECT CAST(SCOPE_IDENTITY() AS INT);
    ";

            int? uploadedBy = int.TryParse(User.FindFirst("userId")?.Value, out int uid) ? uid : null;

            CustomerFileDto fileDto = new CustomerFileDto
            {
                CustomerID = id,
                Category = category,
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                RelativePath = $"/uploads/customers/{id}/{storedFileName}",
                FileExtension = extension,
                MimeType = file.ContentType,
                FileSizeBytes = file.Length,
                FileHash = fileHash,
                FileType = fileType,
                Description = description,
                Tags = tags,
                UploadedBy = uploadedBy
            };

            int newId = await connection.QuerySingleAsync<int>(sql, fileDto);
            return Ok(ApiResponse<int>.Ok(newId, "Dosya başarıyla yüklendi"));
        }
        [HttpGet("{id:int}/is-active")]
        public async Task<IActionResult> IsActive(int id)
        {
            const string sql = @"
        SELECT CASE WHEN Status = 1 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
        FROM Customers WITH (NOLOCK) WHERE ID = @ID
    ";
            using var connection = _context.CreateConnection();
            bool isActive = await connection.QueryFirstOrDefaultAsync<bool>(sql, new { ID = id });
            return Ok(ApiResponse<bool>.Ok(isActive));
        }
        /// <summary>
        /// Dosya sil — sadece SuperAdmin
        /// </summary>
        [HttpDelete("files/{fileId:int}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteFile(int fileId)
        {
            const string selectSql = "SELECT RelativePath FROM CustomerFiles WHERE ID = @ID AND IsDeleted = 0";
            using var connection = _context.CreateConnection();
            var relativePath = await connection.QueryFirstOrDefaultAsync<string>(selectSql, new { ID = fileId });
            if (relativePath == null)
                return NotFound(ApiResponse.NotFound("Dosya bulunamadı"));

            int? deletedBy = int.TryParse(User.FindFirst("userId")?.Value, out int uid) ? uid : null;

            const string deleteSql = @"
                UPDATE CustomerFiles SET 
                    IsDeleted   = 1, 
                    DeletedBy   = @DeletedBy,
                    DeletedDate = GETDATE()
                WHERE ID = @ID
            ";
            await connection.ExecuteAsync(deleteSql, new { ID = fileId, DeletedBy = deletedBy });

            string fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);

            return Ok(ApiResponse.Ok("Dosya başarıyla silindi"));
        }

        #endregion

        // ────────────────────────────────────────────────────────────────────
        #region Location — Tüm roller

        [HttpGet("cities")]
        public async Task<IActionResult> GetCities()
        {
            const string sql = @"
                SELECT DISTINCT Il
                FROM tCity_District_Street_Town WITH (NOLOCK)
                ORDER BY Il ASC
            ";
            using var connection = _context.CreateConnection();
            var cities = await connection.QueryAsync<CitySelectDto>(sql);
            return Ok(ApiResponse<IEnumerable<CitySelectDto>>.Ok(cities));
        }

        [HttpGet("districts/{il}")]
        public async Task<IActionResult> GetDistricts(string il)
        {
            const string sql = @"
                SELECT ID, Ilce
                FROM tCity_District_Street_Town WITH (NOLOCK)
                WHERE Il = @Il
                ORDER BY Ilce ASC
            ";
            using var connection = _context.CreateConnection();
            var districts = await connection.QueryAsync<DistrictSelectDto>(sql, new { Il = il });
            return Ok(ApiResponse<IEnumerable<DistrictSelectDto>>.Ok(districts));
        }

        #endregion
    }
}