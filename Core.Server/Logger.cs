using Spectre.Console;

namespace Sled.Server
{
    public static class Logger
    {
        public static void Info(string message) =>
            AnsiConsole.MarkupLine($"[grey][[Info]][/] {message}");

        public static void Success(string message) =>
            AnsiConsole.MarkupLine($"[green][[Success]][/] {message}");

        public static void Warn(string message) =>
            AnsiConsole.MarkupLine($"[yellow][[Warn]][/] {message}");

        public static void Error(string message) =>
            AnsiConsole.MarkupLine($"[red][[Error]][/] {message}");

        public static void Recv(string message) =>
            AnsiConsole.MarkupLine($"[blue][[Recv]][/] {message}");

        public static void Send(string message) =>
            AnsiConsole.MarkupLine($"[cyan][[Send]][/] {message}");
    }
}
