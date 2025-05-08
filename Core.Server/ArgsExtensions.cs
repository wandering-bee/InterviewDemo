namespace Sled.Server;

// 可放在任意 `.cs` 文件，命名空间随意，只要能被 Main 看到
internal static class ArgsExtensions
{
    public static string GetOption(this string[] args, string name, string fallback = "")
    {
        var idx = Array.IndexOf(args, name);
        return (idx >= 0 && idx < args.Length - 1) ? args[idx + 1] : fallback;
    }

    public static int GetOption(this string[] args, string name, int fallback)
        => int.TryParse(args.GetOption(name, fallback.ToString()), out var v) ? v : fallback;
}
