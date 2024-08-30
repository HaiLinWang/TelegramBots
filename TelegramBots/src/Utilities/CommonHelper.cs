namespace TelegramBots.Utilities;

public class CommonHelper
{
    public static string GetLastPartOfUrl(string url)
    {
        // 查找最后一个斜杠的位置
        var lastSlashIndex = url.LastIndexOf('/');
    
        // 如果找到了斜杠，返回斜杠后面的所有字符
        if (lastSlashIndex != -1 && lastSlashIndex < url.Length - 1)
        {
            return url.Substring(lastSlashIndex + 1);
        }
    
        // 如果没有找到斜杠或斜杠在最后，返回整个字符串
        return url;
    }

}