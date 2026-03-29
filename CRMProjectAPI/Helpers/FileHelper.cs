using Microsoft.AspNetCore.Http;
using System.IO;

namespace CRMProjectAPI.Helpers
{
    public static class FileHelper  // static class olmalı
    {
        private const long MaxFaviconSize = 1 * 1024 * 1024; // 1MB
        public static async Task<string> SaveFaviconAsync(
            IFormFile file,
            IWebHostEnvironment env,         // wwwroot için inject
            string subFolder = "favicon")
        {
            // Validasyonlar
            if (file == null || file.Length == 0)
                throw new ArgumentNullException(nameof(file), "Dosya boş olamaz");
            if (file.Length > MaxFaviconSize)
                throw new ArgumentException($"Dosya boyutu {MaxFaviconSize / (1024 * 1024)}MB'dan büyük olamaz");
            if (!IconHelper.IsSupportedImageFormat(file.FileName))
                throw new ArgumentException("Desteklenmeyen format. PNG, JPG, BMP, GIF veya ICO yükleyiniz.");
            // Path traversal koruması
            if (subFolder.Contains("..") || Path.IsPathRooted(subFolder))
                throw new ArgumentException("Geçersiz klasör adı");
            // IWebHostEnvironment ile güvenli path
            string uploadsFolder = Path.Combine(env.WebRootPath, "uploads", subFolder);
            Directory.CreateDirectory(uploadsFolder);
            string fileName = $"favicon_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid():N}.ico";
            byte[] fileData;
            if (IconHelper.NeedsConversion(file.FileName))
            {
                await using Stream stream = file.OpenReadStream();
                fileData = await IconHelper.ConvertToIcoAsync(stream);
            }
            else
            {
                await using MemoryStream ms = new MemoryStream();
                await file.CopyToAsync(ms);
                fileData = ms.ToArray();
            }
            string filePath = Path.Combine(uploadsFolder, fileName);
            // Hata durumunda yarım kalan dosyayı temizle
            try
            {
                await File.WriteAllBytesAsync(filePath, fileData);
            }
            catch
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                throw;
            }
           return $"/uploads/{subFolder}/{fileName}";
        }
    }
}