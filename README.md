# Upstairs

Windows エクスプローラーのファイル一覧の余白をダブルクリックすると、1 つ上の階層へ移動する常駐ツール。

要件の詳細は [REQUIREMENTS.md](REQUIREMENTS.md) を参照。

## 使い方

`Upstairs.exe` を起動すると、タスクトレイに青い上矢印アイコンが常駐します。

- エクスプローラーの**ファイル一覧の余白を左ダブルクリック** → 上の階層へ移動 (`Alt+↑` 相当)
- トレイアイコンの右クリックメニュー:
  - **有効** — 一時的に機能を止める/再開する (アイコンのダブルクリックでも切り替え可)
  - **Windows 起動時に自動開始** — スタートアップ登録 (HKCU の Run キー)
  - **終了**

以下では発動しません:

- ファイル・フォルダー上のダブルクリック (通常どおり開く)
- デスクトップ、ナビゲーションペイン、アドレスバー、列ヘッダー、名前変更中の編集ボックス
- Ctrl / Shift / Alt / Win キーを押しながらのダブルクリック
- 余白からのドラッグ範囲選択 (クリックを抑制しない設計のため影響なし)

## ビルド

.NET SDK 8 が必要。

```powershell
# 開発ビルド
dotnet build -c Debug

# 配布用 (自己完結・単一 exe → bin\Release\net8.0-windows\win-x64\publish\Upstairs.exe)
dotnet publish -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
```

## 仕組み

1. `WH_MOUSE_LL` の低レベルマウスフックで左ダブルクリックを検出 (クリックの抑制はしない)
2. クリック先のトップレベルウィンドウが `CabinetWClass` (エクスプローラー) かを確認
3. UI Automation でカーソル下の要素を親方向に走査し、アイテム/ヘッダー/ツリー等なら不発動、
   ファイル一覧 (`List` / `*ItemsView*` / `SysListView32`) の背景なら余白と判定
4. 対象ウィンドウへ `SendInput` で `Alt+↑` を送出 (重い判定はワーカースレッドで実行)

DLL インジェクションやシェル拡張は使用しないため、エクスプローラー本体には手を加えません。

## プライバシー・セキュリティについて

- グローバルフックは**マウスのみ**で、キーボードフックは使用しません。取得するのはクリック座標だけで、
  判定に使った後は即座に破棄します。**記録・保存・外部送信は一切行いません。**
- ネットワーク通信は行いません。書き込むのはスタートアップ登録時の HKCU の Run キーのみです。
- 管理者権限は不要です。
- グローバルマウスフックを使う常駐ツールの性質上、ウイルス対策ソフトに誤検知される場合があります。
  また、ビルドした exe は署名されていないため、初回実行時に SmartScreen の警告が出ることがあります。
  気になる場合はソースからご自身でビルドしてください。

## ライセンス

[MIT License](LICENSE)

## 構成

| ファイル | 役割 |
|---|---|
| `Program.cs` | エントリポイント、多重起動防止 (Mutex) |
| `TrayApplicationContext.cs` | トレイ常駐、メニュー、スタートアップ登録 |
| `MouseHook.cs` | 低レベルマウスフックとダブルクリック検出 |
| `ExplorerNavigator.cs` | 余白判定 (UI Automation) と Alt+↑ 送出 |
| `NativeMethods.cs` | Win32 P/Invoke 定義 |
