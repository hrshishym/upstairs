using Microsoft.Win32;
using static Upstairs.NativeMethods;

namespace Upstairs;

/// <summary>タスクトレイ常駐のアプリケーションコンテキスト (F-12)。</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Upstairs";

    private readonly NotifyIcon _notifyIcon;
    private readonly MouseHook _mouseHook;
    private readonly ToolStripMenuItem _enabledMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;
    private readonly Icon _trayIcon;

    private volatile bool _enabled = true;

    public TrayApplicationContext()
    {
        _enabledMenuItem = new ToolStripMenuItem("有効", null, OnToggleEnabled) { Checked = true };
        _startupMenuItem = new ToolStripMenuItem("Windows 起動時に自動開始", null, OnToggleStartup)
        {
            Checked = IsStartupRegistered(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("終了", null, OnExit));

        _trayIcon = CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Upstairs — 余白ダブルクリックで上の階層へ",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += OnToggleEnabled;

        _mouseHook = new MouseHook();
        _mouseHook.DoubleClick += OnGlobalDoubleClick;
    }

    /// <summary>フックスレッド (UI スレッド) 上で呼ばれる。重い判定はワーカースレッドへ逃がす (N-1)。</summary>
    private void OnGlobalDoubleClick(int x, int y)
    {
        if (!_enabled)
        {
            return;
        }

        // 修飾キー併用時は発動しない (F-11)
        if (Control.ModifierKeys != Keys.None || IsWinKeyDown())
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                ExplorerNavigator.HandleDoubleClick(x, y);
            }
            catch
            {
                // 判定失敗時は何もしない (通常操作を妨げない)
            }
        });
    }

    private static bool IsWinKeyDown()
        => (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _enabled = !_enabled;
        _enabledMenuItem.Checked = _enabled;
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        bool register = !_startupMenuItem.Checked;
        using var key = Registry.CurrentUser.CreateSubKey(RunRegistryKey);
        if (register)
        {
            key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        _startupMenuItem.Checked = register;
    }

    private static bool IsStartupRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey);
        return key?.GetValue(RunValueName) != null;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _mouseHook.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
        ExitThread();
    }

    /// <summary>上向き矢印のトレイアイコンを実行時に生成する (外部リソース不要)。</summary>
    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(0, 120, 215));
            g.FillEllipse(brush, 1, 1, 30, 30);

            // 上向き矢印
            using var pen = new Pen(Color.White, 4)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };
            g.DrawLine(pen, 16, 24, 16, 10);
            g.DrawLine(pen, 16, 9, 9, 16);
            g.DrawLine(pen, 16, 9, 23, 16);
        }

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            // FromHandle はハンドルを所有しないため、Clone した複製を返して元ハンドルは破棄する
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
