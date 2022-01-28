using System.IO;
using System.Text;

public static class FileStreamExtensions
{
    public static void AddText(this FileStream fileStream, string text)
    {
        byte[] info = new UTF8Encoding(true).GetBytes(text);
        fileStream.Write(info, 0, info.Length);
    }
}