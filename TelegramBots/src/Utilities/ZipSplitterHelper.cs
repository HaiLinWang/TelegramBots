using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace TelegramBots.Utilities;

// 定义 ZipSplitterHelper 的接口
public interface IZipSplitterHelper
{
    Task<List<string>> SplitAndZipFolderAsync(string sourceFolder, string outputFolder, string baseZipName);
}

public class ZipSplitterHelper(ILogger<ZipSplitterHelper> logger) : IZipSplitterHelper
{
    private readonly ILogger<ZipSplitterHelper> _logger = logger;
    private const long MaxZipSize = 50 * 1024 * 1024; // 20MB in bytes

    public async Task<List<string>> SplitAndZipFolderAsync(string sourceFolder, string outputFolder, string baseZipName)
    {
        // 存储生成的ZIP文件路径
        List<string> zipFiles = new List<string>();

        // 确保输出文件夹存在
        Directory.CreateDirectory(outputFolder);

        // 获取源文件夹中的所有文件
        string[] files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);

        int zipIndex = 1;
        long currentZipSize = 0;
        ZipArchive? currentZip = null;
        try
        {
            foreach (string file in files)
            {
                FileInfo fileInfo = new FileInfo(file);

                // 如果当前ZIP文件不存在或已达到最大大小，创建新的ZIP文件
                if (currentZip == null || currentZipSize + fileInfo.Length > MaxZipSize)
                {
                    // 关闭当前ZIP文件（如果存在）
                    if (currentZip != null)
                    {
                        currentZip.Dispose();
                    }

                    // 创建新的ZIP文件
                    string zipPath = Path.Combine(outputFolder, GetNextZipFileName(baseZipName, zipIndex));
                    currentZip = new ZipArchive(File.Create(zipPath), ZipArchiveMode.Create);
                    zipFiles.Add(zipPath);
                    currentZipSize = 0;
                    zipIndex++;
                }

                // 将文件异步添加到当前ZIP文件
                string entryName = Path.GetRelativePath(sourceFolder, file);
                await AddFileToZipAsync(currentZip, file, entryName);
                currentZipSize += fileInfo.Length;
            }
        }
        finally
        {
            // 确保最后一个ZIP文件被正确关闭
            if (currentZip != null)
            {
                currentZip.Dispose();
            }
        }

        return zipFiles;
    }

    // 异步将文件添加到ZIP文件
    private static async Task AddFileToZipAsync(ZipArchive archive, string filePath, string entryName)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using Stream entryStream = entry.Open();
        await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous);
        await fileStream.CopyToAsync(entryStream);
    }

    // 生成下一个ZIP文件的名称
    private static string GetNextZipFileName(string baseZipName, int index)
    {
        return $"{baseZipName}_{index:D3}.zip";
    }
}