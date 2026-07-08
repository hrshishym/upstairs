using System.ComponentModel;
using static Upstairs.NativeMethods;

namespace Upstairs;

/// <summary>
/// WH_MOUSE_LL で左ダブルクリックを検出する。
/// クリックは一切抑制 (suppress) しないため、通常のクリック・ドラッグ操作を妨げない (F-10)。
/// コールバック内では重い処理を行わないこと (N-1)。
/// </summary>
internal sealed class MouseHook : IDisposable
{
    /// <summary>左ダブルクリック検出時に発火する。引数は物理スクリーン座標。フックスレッド上で呼ばれるため即時リターンすること。</summary>
    public event Action<int, int>? DoubleClick;

    private readonly LowLevelMouseProc _proc; // GC に回収されないよう保持する
    private readonly IntPtr _hook;

    private uint _lastClickTime;
    private int _lastX;
    private int _lastY;

    public MouseHook()
    {
        _proc = Callback;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            throw new Win32Exception();
        }
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == WM_LBUTTONDOWN)
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            // SendInput 等で注入された合成クリックには反応しない
            if ((data.flags & LLMHF_INJECTED) == 0)
            {
                bool isDoubleClick =
                    data.time - _lastClickTime <= GetDoubleClickTime()
                    && Math.Abs(data.pt.X - _lastX) <= GetSystemMetrics(SM_CXDOUBLECLK)
                    && Math.Abs(data.pt.Y - _lastY) <= GetSystemMetrics(SM_CYDOUBLECLK);

                if (isDoubleClick)
                {
                    // トリプルクリックが再度ダブルクリック扱いにならないようリセット
                    _lastClickTime = 0;
                    DoubleClick?.Invoke(data.pt.X, data.pt.Y);
                }
                else
                {
                    _lastClickTime = data.time;
                    _lastX = data.pt.X;
                    _lastY = data.pt.Y;
                }
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
        }
    }
}
