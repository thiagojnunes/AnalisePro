using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AnalisePro.Models;

namespace AnalisePro.Services
{
    public class DiskUsageScanResult
    {
        public string CurrentPath { get; set; }
        public long TotalSizeBytes { get; set; }
        public string TotalSizeDisplay { get; set; }
        public List<DiskDirectoryItem> Items { get; set; }

        public DiskUsageScanResult()
        {
            Items = new List<DiskDirectoryItem>();
        }
    }

    public static class DiskUsageScanner
    {
        private static readonly SolidColorBrush[] PaletteBrushes = InitializePaletteBrushes();

        private static SolidColorBrush[] InitializePaletteBrushes()
        {
            Color[] colors = new Color[]
            {
                Color.FromRgb(0x00, 0xF0, 0xFF), // Cyan
                Color.FromRgb(0xA8, 0x55, 0xF7), // Purple
                Color.FromRgb(0x00, 0xE6, 0x76), // Emerald
                Color.FromRgb(0xFF, 0xB7, 0x03), // Amber
                Color.FromRgb(0xFF, 0x33, 0x66), // Coral Pink
                Color.FromRgb(0x3B, 0x82, 0xF6), // Blue
                Color.FromRgb(0xEC, 0x48, 0x99), // Pink
                Color.FromRgb(0x10, 0xB9, 0x81), // Teal
                Color.FromRgb(0x64, 0x74, 0x8B)  // Slate (Outros)
            };

            SolidColorBrush[] brushes = new SolidColorBrush[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                brushes[i] = new SolidColorBrush(colors[i]);
                // CRITICAL: Freeze the brush so it can be safely used across threads in WPF
                brushes[i].Freeze();
            }
            return brushes;
        }

        public static Task<DiskUsageScanResult> ScanPathAsync(string path, CancellationToken token)
        {
            return Task.Run(() => ScanPath(path, token));
        }

        private static DiskUsageScanResult ScanPath(string path, CancellationToken token)
        {
            var result = new DiskUsageScanResult { CurrentPath = path };

            if (!Directory.Exists(path))
            {
                return result;
            }

            var rawItems = new List<DiskDirectoryItem>();

            // 1. Scan direct subdirectories
            try
            {
                var dirInfos = new DirectoryInfo(path).GetDirectories();
                foreach (var dir in dirInfos)
                {
                    if (token.IsCancellationRequested) return result;

                    // Skip hidden/system reparse points (like C:\Documents and Settings)
                    if ((dir.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        continue;

                    long dirSize = QuickCalculateDirectorySize(dir.FullName, token, 2);

                    rawItems.Add(new DiskDirectoryItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsDirectory = true,
                        SizeBytes = dirSize,
                        SizeDisplay = FormatBytes(dirSize)
                    });
                }
            }
            catch { }

            // 2. Scan direct files in this directory
            try
            {
                var fileInfos = new DirectoryInfo(path).GetFiles();
                foreach (var file in fileInfos)
                {
                    if (token.IsCancellationRequested) return result;

                    rawItems.Add(new DiskDirectoryItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        SizeBytes = file.Length,
                        SizeDisplay = FormatBytes(file.Length)
                    });
                }
            }
            catch { }

            // 3. Sort by size descending
            var sorted = rawItems.OrderByDescending(i => i.SizeBytes).ToList();
            long totalBytes = sorted.Sum(i => i.SizeBytes);
            result.TotalSizeBytes = totalBytes;
            result.TotalSizeDisplay = FormatBytes(totalBytes);

            // Group into top items + "Demais Arquivos e Pastas"
            int maxTop = 7;
            var finalItems = new List<DiskDirectoryItem>();

            long groupedBytes = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                var item = sorted[i];
                if (i < maxTop && item.SizeBytes > 0)
                {
                    item.Percentage = totalBytes > 0 ? (double)item.SizeBytes / totalBytes * 100.0 : 0;
                    item.ColorBrush = PaletteBrushes[i % PaletteBrushes.Length];
                    finalItems.Add(item);
                }
                else
                {
                    groupedBytes += item.SizeBytes;
                }
            }

            if (groupedBytes > 0)
            {
                finalItems.Add(new DiskDirectoryItem
                {
                    Name = string.Format("Demais itens ({0} arquivos/pastas)", sorted.Count - maxTop),
                    FullPath = path,
                    IsDirectory = false,
                    SizeBytes = groupedBytes,
                    SizeDisplay = FormatBytes(groupedBytes),
                    Percentage = totalBytes > 0 ? (double)groupedBytes / totalBytes * 100.0 : 0,
                    ColorBrush = PaletteBrushes[PaletteBrushes.Length - 1]
                });
            }

            result.Items = finalItems;
            return result;
        }

        private static long QuickCalculateDirectorySize(string dirPath, CancellationToken token, int maxDepth)
        {
            if (maxDepth <= 0 || token.IsCancellationRequested) return 0;
            long size = 0;

            try
            {
                var dir = new DirectoryInfo(dirPath);
                foreach (var f in dir.GetFiles())
                {
                    if (token.IsCancellationRequested) return size;
                    size += f.Length;
                }

                foreach (var sub in dir.GetDirectories())
                {
                    if (token.IsCancellationRequested) return size;
                    if ((sub.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) continue;
                    size += QuickCalculateDirectorySize(sub.FullName, token, maxDepth - 1);
                }
            }
            catch { }

            return size;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return string.Format("{0} B", bytes);
            double kb = bytes / 1024.0;
            if (kb < 1024) return string.Format("{0:F1} KB", kb);
            double mb = kb / 1024.0;
            if (mb < 1024) return string.Format("{0:F1} MB", mb);
            double gb = mb / 1024.0;
            if (gb < 1024) return string.Format("{0:F2} GB", gb);
            double tb = gb / 1024.0;
            return string.Format("{0:F2} TB", tb);
        }
    }
}
