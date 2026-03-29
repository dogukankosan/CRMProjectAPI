using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text;
using Image = SixLabors.ImageSharp.Image;

namespace CRMProjectAPI.Helpers
{
    public static class IconHelper
    {
        // Sabit array — her çağrıda yeni allocation olmasın
        private static readonly int[] Sizes = { 16, 32, 48 };
        // Whitelist HashSet — O(1) lookup
        private static readonly HashSet<string> SupportedFormats = new()
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico"
        };
        public static async Task<byte[]> ConvertToIcoAsync(Stream imageStream)
        {
            Image image;
            try
            {
                image = await Image.LoadAsync(imageStream);
            }
            catch (UnknownImageFormatException ex)
            {
                throw new InvalidOperationException("Desteklenmeyen görsel formatı", ex);
            }
            using (image)
            {
                using MemoryStream ms = new MemoryStream();
                // leaveOpen: true — writer dispose ettiğinde stream kapanmasın
                using BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
                // ICO Header
                writer.Write((short)0);
                writer.Write((short)1);
                writer.Write((short)Sizes.Length);
                List<byte[]> imageDataList = new List<byte[]>(Sizes.Length);
                int offset = 6 + (Sizes.Length * 16);
                foreach (int size in Sizes)
                {
                    using var resizedImage = image.Clone(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(size, size),
                        Mode = ResizeMode.Pad  // Stretch yerine Pad — oranı korur
                    }));
                    using MemoryStream pngStream = new MemoryStream();
                    await resizedImage.SaveAsPngAsync(pngStream);
                    byte[] pngData = pngStream.ToArray();
                    imageDataList.Add(pngData);
                    writer.Write((byte)(size >= 256 ? 0 : size));
                    writer.Write((byte)(size >= 256 ? 0 : size));
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((short)1);
                    writer.Write((short)32);
                    writer.Write(pngData.Length);
                    writer.Write(offset);
                    offset += pngData.Length;
                }
                foreach (byte[] data in imageDataList)
                    writer.Write(data);
                // writer flush — leaveOpen olduğu için ms hâlâ açık
                writer.Flush();
                return ms.ToArray();
            }
        }
        public static bool NeedsConversion(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return Path.GetExtension(fileName).ToLowerInvariant() != ".ico";
        }
        public static bool IsSupportedImageFormat(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            return SupportedFormats.Contains(
                Path.GetExtension(fileName).ToLowerInvariant()
            );
        }
    }
}