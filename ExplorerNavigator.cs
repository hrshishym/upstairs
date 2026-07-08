using System.Windows.Automation;
using static Upstairs.NativeMethods;

namespace Upstairs;

/// <summary>
/// ダブルクリック地点がエクスプローラーのファイル一覧の余白かを判定し、
/// 余白なら Alt+↑ を送出して上の階層へ移動する。
/// </summary>
internal static class ExplorerNavigator
{
    /// <summary>この ControlType の上では発動しない (F-2, F-6, F-7, F-9)。</summary>
    private static readonly ControlType[] BlockingTypes =
    {
        ControlType.ListItem,
        ControlType.TreeItem,
        ControlType.DataItem,
        ControlType.HeaderItem,
        ControlType.Header,
        ControlType.Edit,
        ControlType.Document,
        ControlType.Button,
        ControlType.SplitButton,
        ControlType.CheckBox,
        ControlType.RadioButton,
        ControlType.Hyperlink,
        ControlType.MenuItem,
        ControlType.Menu,
        ControlType.MenuBar,
        ControlType.TabItem,
        ControlType.Tab,
        ControlType.ComboBox,
        ControlType.Group,      // グループ見出し
        ControlType.Tree,       // ナビゲーションペイン
        ControlType.ToolBar,
        ControlType.StatusBar,
        ControlType.ScrollBar,
        ControlType.Image,
        ControlType.Text,
        ControlType.ProgressBar,
        ControlType.Slider,
        ControlType.Spinner,
    };

    /// <summary>フックスレッド外 (ワーカースレッド) から呼ぶこと。</summary>
    public static void HandleDoubleClick(int x, int y)
    {
        var pt = new POINT { X = x, Y = y };
        IntPtr hwnd = WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // 対象は標準エクスプローラーのみ (F-5, F-8: デスクトップ/ダイアログは対象外)
        IntPtr root = GetAncestor(hwnd, GA_ROOT);
        if (GetWindowClassName(root) != "CabinetWClass")
        {
            return;
        }

        if (!IsEmptyAreaOfItemsView(x, y))
        {
            return;
        }

        SendAltUp(root);
    }

    /// <summary>
    /// UI Automation でカーソル下の要素を調べ、ファイル一覧の背景 (余白) かを判定する。
    /// 要素から親方向に辿り、アイテム類に当たったら余白ではない。
    /// List (項目ビュー) に当たったら余白と判定する。
    /// </summary>
    private static bool IsEmptyAreaOfItemsView(int x, int y)
    {
        AutomationElement? element;
        try
        {
            element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
        }
        catch
        {
            return false;
        }

        var walker = TreeWalker.ControlViewWalker;

        // Windows Update で UIA ツリー構造が変わっても壊れにくいよう、深さ制限付きで親方向に走査する
        for (int depth = 0; element != null && depth < 32; depth++)
        {
            ControlType controlType;
            string className;
            try
            {
                controlType = element.Current.ControlType;
                className = element.Current.ClassName ?? string.Empty;
            }
            catch
            {
                return false;
            }

            if (Array.IndexOf(BlockingTypes, controlType) >= 0)
            {
                return false;
            }

            // ファイル一覧本体。Win10: UIItemsView / 旧: SysListView32 / Win11: ItemsView 系
            if (controlType == ControlType.List
                || className.Contains("ItemsView", StringComparison.OrdinalIgnoreCase)
                || className == "SysListView32")
            {
                return true;
            }

            if (controlType == ControlType.Window)
            {
                return false;
            }

            try
            {
                element = walker.GetParent(element);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>対象ウィンドウに Alt+↑ を送る。ルートより上がれない場所ではエクスプローラー側で無視される (F-4)。</summary>
    private static void SendAltUp(IntPtr window)
    {
        if (GetForegroundWindow() != window)
        {
            SetForegroundWindow(window);
            Thread.Sleep(50);
        }

        var inputs = new INPUT[4];
        inputs[0] = MakeKeyInput(VK_MENU, keyUp: false);
        inputs[1] = MakeKeyInput(VK_UP, keyUp: false);
        inputs[2] = MakeKeyInput(VK_UP, keyUp: true);
        inputs[3] = MakeKeyInput(VK_MENU, keyUp: true);
        SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKeyInput(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
            },
        },
    };
}
