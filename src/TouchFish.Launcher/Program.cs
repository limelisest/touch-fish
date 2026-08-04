using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TouchFish.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "源文件");
        var target = Path.Combine(sourceDirectory, "TouchFish.exe");
        if (!File.Exists(target))
        {
            _ = MessageBoxW(
                nint.Zero,
                "找不到“源文件\\TouchFish.exe”，请重新解压完整的 TouchFish 文件夹。",
                "TouchFish 启动失败",
                0x10);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(target)
            {
                UseShellExecute = false,
                WorkingDirectory = sourceDirectory
            };
            foreach (var argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }

            _ = Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            _ = MessageBoxW(
                nint.Zero,
                $"无法启动 TouchFish：{exception.Message}",
                "TouchFish 启动失败",
                0x10);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint windowHandle, string text, string caption, uint type);
}
