namespace Upstairs;

static class Program
{
    [STAThread]
    static void Main()
    {
        // 多重起動防止 (F-13)。後から起動した側が終了する。
        using var mutex = new Mutex(initiallyOwned: true, @"Local\Upstairs_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
