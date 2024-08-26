using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TelegramBots.Utilities;

public class FormatConvert
{
    private readonly ILogger<FormatConvert> _logger;
    // 获取 CPU 核心数，并除以2以避免过度使用系统资源
    private readonly int _processorCount;
    public FormatConvert(ILogger<FormatConvert> logger)
    {
        _logger = logger;
        _processorCount = Environment.ProcessorCount / 2;
    }
    // 处理根目录下所有webm文件夹的方法
    public  async Task ProcessWebmFoldersAsync(string rootPath)
    {
        // 获取所有包含webm子文件夹的目录
        var directories = Directory.GetDirectories(rootPath)
            .Where(dir => Directory.Exists(Path.Combine(dir, "webm")))
            .Select(dir => Path.Combine(dir, "webm"))
            .ToList();
        // 创建信号量以限制并发任务数
        var semaphore = new SemaphoreSlim(_processorCount);
        // 并行处理所有webm文件夹
        await Task.WhenAll(directories.Select(async dir =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessConvertFilesAsync(dir);
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    // 处理单个webm文件夹中所有webm文件的方法
    public  async Task ProcessConvertFilesAsync(string folder)
    {
        // 创建对应的gif文件夹
        string gifFolder = Path.Combine(Path.GetDirectoryName(folder), "gif");
        // 如果 gif 文件夹不存在，则创建
        if (!Directory.Exists(gifFolder))
        {
            Directory.CreateDirectory(gifFolder);
            _logger.LogInformation($"已创建 GIF 文件夹: {gifFolder}");
        }
        // 获取文件夹中的所有文件
        string[] allFiles = Directory.GetFiles(folder);
        _logger.LogInformation($"在 {folder} 中找到文件总数：{allFiles.Length}");
        if (allFiles.Length == 0)
        {
            _logger.LogInformation($"在 {folder} 中没有找到任何文件。");
            return;
        }
        // 按文件扩展名分组并计数
        var fileGroups = allFiles
            .GroupBy(file => Path.GetExtension(file).ToLowerInvariant())
            .OrderByDescending(g => g.Count());

        _logger.LogInformation("文件类型统计：");
        foreach (var group in fileGroups)
        {
            _logger.LogInformation($"{group.Key}: {group.Count()} 个文件");
        }

        // 创建信号量以限制并发任务数
        var semaphore = new SemaphoreSlim(_processorCount);
        // 并行处理所有webm文件
        await Task.WhenAll(allFiles.Select(async file =>
        {
            await semaphore.WaitAsync();
            try
            {
                string outputFile = Path.Combine(gifFolder, Path.GetFileNameWithoutExtension(file) + ".gif");
                if (!File.Exists(outputFile))
                {
                    await ConvertWebmToGifAsync(file, outputFile);
                }
                else
                {
                    _logger.LogInformation($"跳过已存在的文件: {Path.GetFileName(outputFile)}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    // 将单个webm文件转换为gif的方法
     async Task ConvertWebmToGifAsync(string inputFile, string outputFile)
    {
        string ffmpegPath = "/usr/local/bin/ffmpeg";

        // ffmpeg命令参数，限制最大帧率为60
        string arguments =
            $"-y -i \"{inputFile}\" -vf \"fps=fps=60:round=up,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{outputFile}\"";
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using (var process = new Process { StartInfo = startInfo })
        {
            try
            {
                process.Start();
                // 读取错误输出
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _logger.LogInformation($"成功转换: {Path.GetFileName(inputFile)} -> {Path.GetFileName(outputFile)}");
                }
                else
                {
                    _logger.LogInformation($"转换失败 {Path.GetFileName(inputFile)}。错误信息：{error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"处理 {Path.GetFileName(inputFile)} 时发生错误: {ex.Message}");
            }
        }
    }
}