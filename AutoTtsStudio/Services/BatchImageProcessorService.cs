using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using ImageRectangle = SixLabors.ImageSharp.Rectangle;

namespace AutoTtsStudio.Services;

/// <summary>
/// Dịch vụ xử lý ảnh hàng loạt: Xóa watermark/logo góc dưới phải,
/// chuyển đổi định dạng (.webp -> .png/.jpg), upscale lên 1080p bằng thuật toán Lanczos3.
/// </summary>
public static class BatchImageProcessorService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webp", ".png", ".jpg", ".jpeg"
    };

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    /// <summary>
    /// Xử lý tất cả ảnh trong thư mục inputFolder và lưu kết quả vào thư mục con Cleaned_Images.
    /// </summary>
    public static async Task<int> ProcessFolderAsync(
        string inputFolder,
        double cropPercent,
        string outputFormat,
        bool upscaleTo1080p,
        IProgress<(int current, int total, string fileName)>? progress,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inputFolder) || !Directory.Exists(inputFolder))
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục: {inputFolder}");

        // Quét tất cả file ảnh hợp lệ
        var files = Directory.EnumerateFiles(inputFolder)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        if (files.Count == 0) return 0;

        // Sắp xếp tự nhiên theo số thứ tự tên file (001_, 002_, 10_...)
        files.Sort((a, b) => StrCmpLogicalW(a, b));

        // Thư mục lưu kết quả
        string outputDir = Path.Combine(inputFolder, "Cleaned_Images");
        Directory.CreateDirectory(outputDir);

        bool isJpg = outputFormat.StartsWith("JPG", StringComparison.OrdinalIgnoreCase);
        string targetExt = isJpg ? ".jpg" : ".png";
        var jpegEncoder = new JpegEncoder { Quality = 95 };

        int total = files.Count;
        int processedCount = 0;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();

            string srcFile = files[i];
            string fileName = Path.GetFileName(srcFile);
            progress?.Report((i + 1, total, fileName));

            string baseName = Path.GetFileNameWithoutExtension(srcFile);
            string destPath = Path.Combine(outputDir, baseName + targetExt);

            try
            {
                using var image = await Image.LoadAsync(srcFile, ct).ConfigureAwait(false);

                // 1. Cắt bỏ logo góc dưới bên phải giữ nguyên tỷ lệ khung hình
                if (cropPercent > 0.0)
                {
                    double factor = 1.0 - (cropPercent / 100.0);
                    int newWidth = (int)Math.Round(image.Width * factor);
                    int newHeight = (int)Math.Round(image.Height * factor);

                    if (newWidth > 10 && newHeight > 10 && (newWidth < image.Width || newHeight < image.Height))
                    {
                        image.Mutate(ctx => ctx.Crop(new ImageRectangle(0, 0, newWidth, newHeight)));
                    }
                }

                // 2. Nâng lên 1920x1080 bằng Lanczos3 nếu ảnh nhỏ hơn 1920px
                if (upscaleTo1080p && image.Width < 1920)
                {
                    image.Mutate(ctx => ctx.Resize(1920, 1080, KnownResamplers.Lanczos3));
                }

                // 3. Lưu vào Cleaned_Images
                if (isJpg)
                {
                    await image.SaveAsJpegAsync(destPath, jpegEncoder, ct).ConfigureAwait(false);
                }
                else
                {
                    await image.SaveAsPngAsync(destPath, ct).ConfigureAwait(false);
                }

                processedCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BatchImage] Lỗi xử lý {fileName}: {ex.Message}");
            }
        }

        return processedCount;
    }
}
