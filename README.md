# 📝 Markdown to PDF Converter

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

マークダウンファイルをリアルタイムでプレビューし、PDFに出力できるWPFアプリケーションです。日本語の文字コード（UTF-8、Shift-JIS、EUC-JP、ISO-2022-JP）に対応しています。

## ✨ 主な機能

- 📂 **マークダウンファイルの読み込み** - .md, .markdownファイルに対応
- 🔤 **多様な文字コード対応** - UTF-8、Shift-JIS、EUC-JP、ISO-2022-JP（JIS）
- 👁️ **リアルタイムプレビュー** - 編集中の内容を即座にHTMLプレビュー
- 📄 **PDF出力** - Microsoft Print to PDFを使用して簡単にPDF化
- 🎨 **モダンなUI** - 使いやすく洗練されたインターフェース
- ✏️ **完全なMarkdown対応** - Markdigライブラリによる豊富な記法サポート
- 📦 **単一実行ファイル** - .exe一つで動作、インストール不要

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
- Microsoft Print to PDF（Windows標準機能）

## 🔧 インストール

### 方法1: リリースからダウンロード（推奨）

1. [Releases](https://github.com/kouta0606/md-pdf-exporter/releases)から最新版の`MK_to_PDF.exe`をダウンロード
2. 任意の場所に配置（デスクトップやドキュメントフォルダなど）
3. `MK_to_PDF.exe`をダブルクリックで起動

**それだけです！** インストール不要、設定ファイル不要、追加ダウンロード不要です。

### 方法2: ソースからビルド

```bash
# リポジトリをクローン
git clone https://github.com/kouta0606/md-pdf-exporter.git
cd md-pdf-exporter/MK_to_PDF

# NuGetパッケージを復元
dotnet restore

# 実行
dotnet run
```

### 方法3: 単一実行ファイルとして発行

```powershell
# プロジェクトディレクトリに移動
cd md-pdf-exporter/MK_to_PDF

# 単一実行ファイルを生成
dotnet publish -c Release -r win-x64 --self-contained

# 実行ファイルは以下に作成されます:
# bin\Release\net10.0-windows\win-x64\publish\MK_to_PDF.exe
```

生成された`MK_to_PDF.exe`（約65MB）だけで動作します。

## 📖 使い方

### 基本操作

1. **ファイルを開く**: 「📂 ファイルを開く」ボタンをクリックしてマークダウンファイルを選択
2. **文字コード選択**: 文字化けする場合は、ドロップダウンから適切な文字コードを選択
3. **再読込**: 文字コードを変更したら「🔄 再読込」で再読み込み
4. **プレビュー**: 右側のパネルでリアルタイムプレビューを確認
5. **PDF出力**: 「📄 PDF出力」ボタンをクリックして印刷ダイアログからPDF保存

### 文字コード選択ガイド

| 文字コード | 用途 |
|------------|------|
| **UTF-8** | 最も一般的な文字コード。デフォルトで選択 |
| **Shift-JIS** | 古い日本語ファイル、Windowsの標準文字コード |
| **EUC-JP** | Unix/Linuxシステムで使用される日本語文字コード |
| **ISO-2022-JP (JIS)** | メールなどで使用される日本語文字コード |

## 🛠️ 技術スタック

- **フレームワーク**: .NET 10 (プレビュー)
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
├── README.md
└── LICENSE
```

## 🐛 既知の問題

- PDF出力はWindowsの印刷ダイアログを使用するため、自動保存には対応していません
- WebView2を使用していないため、一部の高度なHTML機能は制限されます

## 📄 ライセンス

このプロジェクトはMITライセンスの下で公開されています。詳細は[LICENSE](LICENSE)ファイルをご覧ください。

## 👤 作者

**kouta0606**

- GitHub: [@kouta0606](https://github.com/kouta0606)

## 🙏 謝辞

- [Markdig](https://github.com/xoofx/markdig) - 素晴らしいMarkdownパーサー
- Material Design - UIデザインのインスピレーション

---
