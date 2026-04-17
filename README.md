# 📝 Markdown to PDF Converter

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

マークダウンファイルをリアルタイムでプレビューし、PDFに出力できるWPFアプリケーションです。日本語の文字コード（UTF-8、Shift-JIS、EUC-JP、ISO-2022-JP）に対応しています。

![Screenshot](docs/screenshot.png)

## ✨ 主な機能

- 📂 **マークダウンファイルの読み込み** - .md, .markdownファイルに対応
- 🔤 **多様な文字コード対応** - UTF-8、Shift-JIS、EUC-JP、ISO-2022-JP（JIS）
- 👁️ **リアルタイムプレビュー** - 編集中の内容を即座にHTMLプレビュー
- 📄 **PDF出力** - Microsoft Print to PDFを使用して簡単にPDF化
- 🎨 **モダンなUI** - 使いやすく洗練されたインターフェース
- ✏️ **完全なMarkdown対応** - Markdigライブラリによる豊富な記法サポート

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

- Windows 10/11
- .NET 10 Runtime
- Microsoft Print to PDF（Windows標準機能）

## 🔧 インストール

### バイナリからインストール

1. [Releases](https://github.com/kouta0606/md-pdf-exporter/releases)から最新版をダウンロード
2. ZIPファイルを解凍
3. `MK_to_PDF.exe`を実行

### ソースからビルド

```bash
# リポジトリをクローン
git clone https://github.com/kouta0606/md-pdf-exporter.git
cd md-pdf-exporter

# ビルド
dotnet build

# 実行
dotnet run
```

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

### キーボードショートカット

現在のバージョンではマウス操作のみサポートしています。

## 🛠️ 技術スタック

- **フレームワーク**: .NET 10
- **UI**: WPF (Windows Presentation Foundation)
- **Markdownパーサー**: [Markdig](https://github.com/xoofx/markdig) v0.40.0
- **文字コード**: System.Text.Encoding.CodePages v9.0.0

## 📂 プロジェクト構成

```
md-pdf-exporter/
├── MK_to_PDF/
│   ├── MainWindow.xaml        # UIデザイン
│   ├── MainWindow.xaml.cs     # メインロジック
│   └── MK_to_PDF.csproj       # プロジェクトファイル
├── README.md
└── LICENSE
```

## 🐛 既知の問題

- PDF出力はWindowsの印刷ダイアログを使用するため、自動保存には対応していません

## 🤝 コントリビューション

プルリクエストを歓迎します！大きな変更を行う場合は、まずissueを開いて変更内容を議論してください。

1. このリポジトリをフォーク
2. フィーチャーブランチを作成 (`git checkout -b feature/amazing-feature`)
3. 変更をコミット (`git commit -m 'Add some amazing feature'`)
4. ブランチにプッシュ (`git push origin feature/amazing-feature`)
5. プルリクエストを作成

## 👤 作者

**kouta0606**

- GitHub: [@kouta0606](https://github.com/kouta0606)

## 🙏 謝辞

- [Markdig](https://github.com/xoofx/markdig) - 素晴らしいMarkdownパーサー
- Material Design - UIデザインのインスピレーション

---
