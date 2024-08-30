

using FFmpeg.NET;
using FFmpeg.NET.Enums;

namespace TelegramBots.Utilities;


public class VideoConverter
{
    private readonly string _ffmpegPath;

    public VideoConverter(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath;
    }

    public async Task ConvertWebmToGif(string inputPath, string outputPath, int width = -1, int fps = 10)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input WebM file not found.", inputPath);
        }

        var inputFile = new InputFile(inputPath);
        var outputFile = new OutputFile(outputPath);

        var ffmpeg = new Engine(_ffmpegPath);

        var conversionOptions = new ConversionOptions
        {
            CustomWidth = width,
            VideoFps = fps,
            VideoCodec =VideoCodec.gif,
        };

        var filterComplex = $"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse";
        try
        {
            await ffmpeg.ConvertAsync(inputFile, outputFile, conversionOptions, CancellationToken.None);
            Console.WriteLine($"Conversion completed. GIF saved to: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during conversion: {ex.Message}");
            throw;
        }
    }
}