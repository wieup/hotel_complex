namespace HotelComplexApp;

internal static class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotelComplexApp", "error.log");

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Любая необработанная ошибка будет показана и записана в файл
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError("ThreadException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportError("UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportError("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        while (true)
        {
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() != DialogResult.OK) return;
            }

            using var main = new MainForm();
            Application.Run(main);

            if (!main.LogoutRequested) return;
        }
    }

    private static void ReportError(string source, Exception? ex)
    {
        string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\r\n{ex}\r\n\r\n";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, text);
        }
        catch
        {
           
        }

        try
        {
            MessageBox.Show(
                "Произошла непредвиденная ошибка:\n\n" + (ex?.Message ?? "неизвестная ошибка") +
                "\n\nПодробности записаны в файл:\n" + LogPath,
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}