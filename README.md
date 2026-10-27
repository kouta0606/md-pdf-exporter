# 📝 Markdown to PDF Converter

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)

マークダウンファイルをリアルタイムでプレビューし、PDFに出力できるWPFアプリケーションです。日本語の文字コード（UTF-8、Shift-JIS、EUC-JP、ISO-2022-JP）に対応しています。

## ⬇️ ダウンロード

1. [Releases ページ](https://github.com/kouta0606/md-pdf-exporter/releases/latest)を開く
2. 最新バージョンの `MK_to_PDF-vX.X.X-win-x64.zip` をダウンロード
3. zip を展開し、`MK_to_PDF.exe` をダブルクリックで起動

インストール不要です。.NET Runtime も不要です（WebView2 Runtime のみ必要。Windows 11 は標準搭載）。

## 主な機能

- **マークダウンファイルの読み込み** - .md, .markdownファイルに対応
- **多様な文字コード対応** - UTF-8、Shift-JIS、EUC-JP、ISO-2022-JP（JIS）
- **リアルタイムプレビュー** - 編集中の内容を即座にHTMLプレビュー
- **PDF出力** - WebView2の機能で直接PDFを保存（ヘッダー/フッター/ページ番号付き）
- **Markdown保存** - 編集内容を文字コードを保ったまま保存
- **タスクリスト** - `- [ ]` / `- [x]` をチェックボックスとして表示
- **入力支援** - エディタで文字を選択して左クリック（または右クリック）すると、太字・取り消し線・コード・見出し・引用・リスト・チェックリスト・リンク・改行（`<br>`）・表の挿入メニューを表示
- **ページ区切り** - `<!-- PAGE_BREAK -->` で任意の位置で改ページ（ページ境界表示はデフォルトOFF）
- **スクロール同期** - エディタとプレビューを双方向に同期。編集しても位置を維持
- **選択ハイライト** - プレビューで選択した箇所に対応するエディタ側をハイライト
- **リンク** - クリックするとプレビューを離れず、既定のブラウザで開く
- **設定** - 保存先フォルダ、PDFのヘッダー/フッター（左・中央・右）、ページ番号
- **単一実行ファイル** - .exe一つで動作、インストール不要

## 🚀 対応しているMarkdown記法

- 見出し（`#`, `##`, `###`, etc.）
- 太字・イタリック（`**bold**`, `*italic*`）
- リスト（箇条書き・番号付き）
- コードブロック（`` `inline` `` と ` ```code block``` `）
- リンク（`[text](url)`）
- 画像（`![alt](url)`）
- 引用（`> quote`）
- テーブル
- タスクリスト（`- [ ]` / `- [x]`）
- 水平線（`---`）
- その他の拡張機能

## 📋 必要要件

- Windows 10/11 (64-bit)
- **.NET Runtimeは不要** - 単一実行ファイルに全て含まれています
- **WebView2 Runtime** - Windows 11では標準搭載、それ以外は[こちら](https://go.microsoft.com/fwlink/p/?LinkId=2124703)から自動インストール（約30秒）

## 🔧 ビルド（開発者向け）

### 開発モードで実行

```powershell
git clone https://github.com/kouta0606/md-pdf-exporter.git
cd md-pdf-exporter
dotnet run --project MK_to_PDF
```

### 単一exeを作成

```powershell
dotnet publish MK_to_PDF\MK_to_PDF.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
# 出力: publish\MK_to_PDF.exe
```

## 🚢 リリース手順（メンテナー向け）

```powershell
# 1. exeを作成して zip 化
dotnet publish MK_to_PDF\MK_to_PDF.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
Compress-Archive publish\MK_to_PDF.exe MK_to_PDF-v1.0.0-win-x64.zip

# 2. タグを作成して push
git tag v1.0.0
git push origin v1.0.0

# 3. GitHub Release を作成（gh auth login 済みであること）
gh release create v1.0.0 MK_to_PDF-v1.0.0-win-x64.zip --title "v1.0.0" --generate-notes
```

ブラウザから行う場合は、GitHub の「Releases」→「Draft a new release」でタグを選び、zip を添付して公開します。

## 📖 使い方

### 基本操作

1. **ファイルを開く**: 「📂 ファイルを開く」ボタンをクリックしてマークダウンファイルを選択
2. **文字コード選択**: 文字化けする場合は、ドロップダウンから適切な文字コードを選択
3. **再読込**: 文字コードを変更したら「🔄 再読込」で再読み込み
4. **プレビュー**: 右側のパネルでリアルタイムプレビューを確認
5. **PDF出力**: 「📄 PDF出力」ボタンをクリックして保存先を選択
6. **Markdown保存**: 編集した内容を保存ボタンから保存
7. **入力支援**: エディタで文字を選択して左クリック（または右クリック）し、メニューから書式や表・リンク・改行を挿入
8. **改ページ**: 「📄 改ページ」ボタンまたは `<!-- PAGE_BREAK -->` を記述

### ⚙ 設定

「⚙ 設定」ボタンから以下を設定できます（デフォルトはすべて空欄/OFF）。

| 項目 | 内容 |
|------|------|
| 保存先フォルダ | 保存ダイアログの初期フォルダ |
| ヘッダー / フッター | 文字列と配置（左端・中央・右端）。日付・時刻は出力されません |
| ページ番号 | ON でフッターにページ番号を出力 |

設定は `%LocalAppData%\MK_to_PDF_設定\settings.json` に保存されます（キー: `SaveDirectory`, `header`, `header-alignment`, `footer`, `footer-alignment`, `page-number`）。  
端近くに表示されます。

### 文字コード

| 文字コード | 用途 |
|------------|------|
| **UTF-8** | 最も一般的な文字コード。デフォルトで選択 |
| **Shift-JIS** | 古い日本語ファイル、Windowsの標準文字コード |
| **EUC-JP** | Unix/Linuxシステムで使用される日本語文字コード |
| **ISO-2022-JP (JIS)** | メールなどで使用される日本語文字コード |

## 🛠️ 技術スタック

- **フレームワーク**: .NET 10
- **UI**: WPF (Windows Presentation Foundation)
- **Markdownパーサー**: [Markdig](https://github.com/xoofx/markdig) v0.40.0
- **配布形式**: 単一実行ファイル（Self-Contained Deployment）

## 📂 プロジェクト構成

```
md-pdf-exporter/
├── MK_to_PDF/
│   ├── MainWindow.xaml        # UIデザイン
│   ├── MainWindow.xaml.cs     # メインロジック
│   ├── App.xaml               # アプリケーション設定
│   ├── App.xaml.cs            # アプリケーションロジック
│   ├── app.ico                # アプリケーションアイコン
│   └── MK_to_PDF.csproj       # プロジェクトファイル
└── README.md
```

## 👤 作者

**kouta0606**

- GitHub: [@kouta0606](https://github.com/kouta0606)

## 🙏 謝辞

- [Markdig](https://github.com/xoofx/markdig) - 素晴らしいMarkdownパーサー
- Material Design - UIデザインのインスピレーション

---
