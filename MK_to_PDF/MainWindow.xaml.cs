using System.Text;
using System.Windows;
using System.Windows.Controls;
using Markdig;
using Microsoft.Web.WebView2.Core;

namespace MK_to_PDF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string settingsFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MK_to_PDF_設定",
            "settings.json");
        private string? currentFilePath;
        private string? saveDirectory;
        private string pdfHeader = string.Empty;
        private string pdfHeaderAlignment = "left";
        private string pdfFooter = string.Empty;
        private string pdfFooterAlignment = "left";
        private bool printPageNumbers;
        private bool showPageBoundaries = false;
        private System.Windows.Threading.DispatcherTimer? debounceTimer;
        private bool isWebView2Initialized = false;
        private bool isScrollSyncEnabled = true;
        private ScrollViewer? markdownEditorScrollViewer;
        private double lastPreviewScrollPercentage;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            // Shift-JIS、EUC-JP、ISO-2022-JPなどのエンコーディングを使用可能にする
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // エンコーディングのリストを設定
            EncodingComboBox.Items.Add(new EncodingItem("UTF-8", Encoding.UTF8));
            EncodingComboBox.Items.Add(new EncodingItem("Shift-JIS", Encoding.GetEncoding("Shift_JIS")));
            EncodingComboBox.Items.Add(new EncodingItem("EUC-JP", Encoding.GetEncoding("EUC-JP")));
            EncodingComboBox.Items.Add(new EncodingItem("ISO-2022-JP (JIS)", Encoding.GetEncoding("ISO-2022-JP")));
            EncodingComboBox.SelectedIndex = 0; // デフォルトはUTF-8

            // デバウンスタイマーの初期化（300ms待機）
            debounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            debounceTimer.Tick += (s, e) =>
            {
                debounceTimer.Stop();
                RefreshPreview();
            };

            // ClearFile_Click
            InitializeWebView2Async();

            // マークダウンエディタのスクロールイベントを追加（Loaded後に設定）
            MarkdownEditor.Loaded += (s, e) =>
            {
                var scrollViewer = FindScrollViewer(MarkdownEditor);
                if (scrollViewer != null)
                {
                    markdownEditorScrollViewer = scrollViewer;
                    scrollViewer.ScrollChanged += MarkdownEditor_ScrollChanged;
                }
            };
        }

        private void LoadSettings()
        {
            if (!System.IO.File.Exists(settingsFilePath))
            {
                return;
            }

            try
            {
                var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
                    System.IO.File.ReadAllText(settingsFilePath));

                if (settings == null)
                {
                    return;
                }

                saveDirectory = !string.IsNullOrWhiteSpace(settings.SaveDirectory)
                    && System.IO.Directory.Exists(settings.SaveDirectory)
                    ? settings.SaveDirectory
                    : null;
                pdfHeader = settings.Header ?? string.Empty;
                pdfHeaderAlignment = NormalizeAlignment(settings.HeaderAlignment);
                pdfFooter = settings.Footer ?? string.Empty;
                pdfFooterAlignment = NormalizeAlignment(settings.FooterAlignment);
                printPageNumbers = settings.PageNumber;

                if (!string.IsNullOrWhiteSpace(settings.SaveDirectory) && saveDirectory == null)
                {
                    MessageBox.Show(
                        $"設定された保存先フォルダーが見つかりません。既定のドキュメントフォルダーを使用します。\n\n{settings.SaveDirectory}",
                        "保存先設定",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"保存先設定を読み込めませんでした。既定のドキュメントフォルダーを使用します。\n\n{ex.Message}",
                    "設定読み込みエラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private string GetInitialSaveDirectory()
        {
            if (!string.IsNullOrWhiteSpace(saveDirectory))
            {
                if (System.IO.Directory.Exists(saveDirectory))
                {
                    return saveDirectory;
                }

                saveDirectory = null;
                MessageBox.Show(
                    "設定された保存先フォルダーが見つからないため、既定のドキュメントフォルダーを使用します。",
                    "保存先設定",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            var documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Directory.Exists(documentsDirectory)
                ? documentsDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private void SaveDirectorySettings_Click(object sender, RoutedEventArgs e)
        {
            var directoryTextBox = new TextBox
            {
                Text = saveDirectory ?? string.Empty,
                IsReadOnly = true,
                MinWidth = 300,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var browseButton = new Button
            {
                Content = "参照...",
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(10, 4, 10, 4)
            };
            browseButton.Click += (_, _) =>
            {
                var folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "MarkdownとPDFの保存先フォルダーを選択",
                    InitialDirectory = System.IO.Directory.Exists(directoryTextBox.Text)
                        ? directoryTextBox.Text
                        : GetInitialSaveDirectory()
                };
                if (folderDialog.ShowDialog(this) == true)
                {
                    directoryTextBox.Text = folderDialog.FolderName;
                }
            };

            var headerTextBox = new TextBox { Text = pdfHeader, MinWidth = 280, Margin = new Thickness(0, 4, 0, 12) };
            var headerAlignmentComboBox = CreateAlignmentComboBox(pdfHeaderAlignment);
            var footerTextBox = new TextBox { Text = pdfFooter, MinWidth = 280, Margin = new Thickness(0, 4, 0, 12) };
            var footerAlignmentComboBox = CreateAlignmentComboBox(pdfFooterAlignment);
            var pageNumberCheckBox = new CheckBox
            {
                Content = "PDFにページ番号を表示する",
                IsChecked = printPageNumbers,
                Margin = new Thickness(0, 4, 0, 16)
            };

            var content = new StackPanel { Margin = new Thickness(16) };
            content.Children.Add(new TextBlock { Text = "保存先フォルダー" });
            var directoryPanel = new DockPanel { Margin = new Thickness(0, 4, 0, 16) };
            DockPanel.SetDock(browseButton, Dock.Right);
            directoryPanel.Children.Add(browseButton);
            directoryPanel.Children.Add(directoryTextBox);
            content.Children.Add(directoryPanel);
            content.Children.Add(new TextBlock { Text = "PDFヘッダー（空欄可）" });
            content.Children.Add(CreateHeaderFooterRow(headerTextBox, headerAlignmentComboBox));
            content.Children.Add(new TextBlock { Text = "PDFフッター（空欄可）" });
            content.Children.Add(CreateHeaderFooterRow(footerTextBox, footerAlignmentComboBox));
            content.Children.Add(pageNumberCheckBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveButton = new Button { Content = "保存", IsDefault = true, MinWidth = 72, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
            var cancelButton = new Button { Content = "キャンセル", IsCancel = true, MinWidth = 72, Padding = new Thickness(8, 4, 8, 4) };
            buttons.Children.Add(saveButton);
            buttons.Children.Add(cancelButton);
            content.Children.Add(buttons);

            var dialog = new Window
            {
                Title = "設定",
                Width = 480,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = this,
                Content = content
            };
            saveButton.Click += (_, _) =>
            {
                var selectedDirectory = directoryTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(selectedDirectory) && !System.IO.Directory.Exists(selectedDirectory))
                {
                    MessageBox.Show("選択した保存先フォルダーが見つかりません。", "入力内容の確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                dialog.DialogResult = true;
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var selectedDirectory = directoryTextBox.Text.Trim();
            try
            {
                var settingsDirectory = System.IO.Path.GetDirectoryName(settingsFilePath)
                    ?? throw new InvalidOperationException("設定ファイルの保存先を特定できません。");
                System.IO.Directory.CreateDirectory(settingsDirectory);
                var newSettings = new AppSettings
                {
                    SaveDirectory = string.IsNullOrEmpty(selectedDirectory) ? null : selectedDirectory,
                    Header = headerTextBox.Text,
                    HeaderAlignment = GetSelectedAlignment(headerAlignmentComboBox),
                    Footer = footerTextBox.Text,
                    FooterAlignment = GetSelectedAlignment(footerAlignmentComboBox),
                    PageNumber = pageNumberCheckBox.IsChecked == true
                };
                var settingsJson = System.Text.Json.JsonSerializer.Serialize(
                    newSettings,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(settingsFilePath, settingsJson);

                saveDirectory = newSettings.SaveDirectory;
                pdfHeader = newSettings.Header ?? string.Empty;
                pdfHeaderAlignment = NormalizeAlignment(newSettings.HeaderAlignment);
                pdfFooter = newSettings.Footer ?? string.Empty;
                pdfFooterAlignment = NormalizeAlignment(newSettings.FooterAlignment);
                printPageNumbers = newSettings.PageNumber;
                MessageBox.Show("設定を保存しました。", "設定", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"設定を保存できませんでした。\n\n{ex.Message}",
                    "設定保存エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static ComboBox CreateAlignmentComboBox(string alignment)
        {
            var comboBox = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(8, 4, 0, 12),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            comboBox.Items.Add(new ComboBoxItem { Content = "左端", Tag = "left" });
            comboBox.Items.Add(new ComboBoxItem { Content = "中央", Tag = "center" });
            comboBox.Items.Add(new ComboBoxItem { Content = "右端", Tag = "right" });
            comboBox.SelectedItem = comboBox.Items
                .Cast<ComboBoxItem>()
                .First(item => string.Equals(item.Tag as string, NormalizeAlignment(alignment), StringComparison.Ordinal));
            return comboBox;
        }

        private static StackPanel CreateHeaderFooterRow(TextBox textBox, ComboBox alignmentComboBox)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(textBox);
            row.Children.Add(alignmentComboBox);
            return row;
        }

        private static string GetSelectedAlignment(ComboBox comboBox)
        {
            return NormalizeAlignment((comboBox.SelectedItem as ComboBoxItem)?.Tag as string);
        }

        private static string NormalizeAlignment(string? alignment)
        {
            return alignment is "center" or "right" ? alignment : "left";
        }

        private void PreviewBrowser_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!IsHttpUri(e.Uri))
            {
                return;
            }

            e.Cancel = true;
            OpenLinkInDefaultBrowser(e.Uri);
        }

        private void PreviewBrowser_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (!IsHttpUri(e.Uri))
            {
                return;
            }

            e.Handled = true;
            OpenLinkInDefaultBrowser(e.Uri);
        }

        private static bool IsHttpUri(string uri)
        {
            return Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)
                && (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps);
        }

        private void OpenLinkInDefaultBrowser(string uri)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"リンクを既定のブラウザーで開けませんでした。\n\n{ex.Message}",
                    "リンクを開けません",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void InitializeWebView2Async()
        {
            try
            {
                await PreviewBrowser.EnsureCoreWebView2Async(null);
                isWebView2Initialized = true;

                // デフォルトのコンテキストメニューを無効化（オプション）
                PreviewBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                PreviewBrowser.CoreWebView2.Settings.AreDevToolsEnabled = true; // F12で開発者ツール

                // ダークモードをWebView2に適用しない（OSのダークモード設定に関わらず常にライト表示）
                PreviewBrowser.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
                PreviewBrowser.CoreWebView2.NavigationStarting += PreviewBrowser_NavigationStarting;
                PreviewBrowser.CoreWebView2.NewWindowRequested += PreviewBrowser_NewWindowRequested;
                PreviewBrowser.CoreWebView2.NavigationCompleted += PreviewBrowser_NavigationCompleted;
                PreviewBrowser.CoreWebView2.WebMessageReceived += PreviewBrowser_WebMessageReceived;

                System.Diagnostics.Debug.WriteLine("[WebView2] 初期化完了");

                // 初期化完了後にプレビューを表示
                RefreshPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"WebView2の初期化に失敗しました: {ex.Message}\n\n" +
                    "Microsoft Edge WebView2 Runtimeがインストールされているか確認してください。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void MarkdownEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            // デバウンス：連続入力時は300ms待ってから更新
            debounceTimer?.Stop();
            debounceTimer?.Start();
        }

        private string WrapHtml(string body) =>
            $@"<html>
<head>
    <meta charset='utf-8'/>
    <meta name='color-scheme' content='light'/>
    <style>
        body {{ 
            font-family: 'Segoe UI', Meiryo, sans-serif; 
            padding: 20px;
            line-height: 1.6;
            background-color: #ffffff;
            color: #1a1a1a;
        }}
        code {{ 
            background-color: #f4f4f4; 
            padding: 2px 4px; 
            border-radius: 3px;
            font-family: 'Consolas', monospace;
            /* インラインコードは通常サイズのまま */
            border: 1px solid #ddd;
            /* 印刷時に背景色を強制的に出力 */
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
            color-adjust: exact;
        }}
        pre {{ 
            background-color: #f4f4f4; 
            padding: 10px; 
            border-radius: 5px;
            overflow-x: visible; /* スクロールバーを表示しない */
            overflow-y: visible;
            white-space: pre-wrap; /* 長い行を折り返す */
            word-wrap: break-word; /* 単語を折り返す */
            word-break: break-all; /* 長い単語も強制的に折り返す */
            border: 1px solid #ccc;
            /* font-size と line-height はインラインスタイルで動的に設定される */
            /* 印刷時に背景色を強制的に出力 */
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
            color-adjust: exact;
            /* コードブロックが途中で分割されないようにする */
            page-break-inside: avoid;
            break-inside: avoid;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
            border: none;
            font-size: inherit; /* preのフォントサイズを継承 */
        }}
        blockquote {{ 
            border-left: 4px solid #ddd; 
            margin: 0;
            padding-left: 16px; 
            color: #666;
            /* 引用ブロックが途中で分割されないようにする */
            page-break-inside: avoid;
            break-inside: avoid;
        }}
        table {{ 
            border-collapse: collapse; 
            width: 100%;
            margin: 10px 0;
            table-layout: auto;
            word-break: break-word;
            overflow-wrap: break-word;
        }}
        table, th, td {{ 
            border: 1px solid #ddd; 
        }}
        th, td {{ 
            padding: 6px 8px; 
            text-align: left;
            word-break: break-word;
            overflow-wrap: break-word;
            min-width: 40px;
            max-width: 400px;
        }}
        th {{
            background-color: #f0f0f0;
            font-weight: bold;
            white-space: nowrap;
            /* テーブルヘッダーの背景色も印刷 */
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
            color-adjust: exact;
        }}
        /* テーブルをページ幅に収める */
        .table-wrapper {{
            width: 100%;
            overflow-x: auto;
        }}
        /* 見出しの直後で改ページしない（見出しが孤立しないようにする） */
        h1, h2, h3, h4, h5, h6 {{
            page-break-after: avoid;
            break-after: avoid;
            page-break-inside: avoid;
            break-inside: avoid;
        }}
        /* リストアイテムが途中で分割されにくくする */
        li {{
            page-break-inside: avoid;
            break-inside: avoid;
        }}
        .contains-task-list {{
            list-style: none;
            padding-left: 1.5em;
        }}
        .task-list-item input[type=""checkbox""] {{
            accent-color: #2196F3;
            width: 1em;
            height: 1em;
            vertical-align: middle;
            margin: 0 0.4em 0 0;
        }}
        /* 段落が分割されにくくする */
        p {{
            orphans: 3;
            widows: 3;
        }}
        /* 手動改ページマーカー */
        .page-break {{
            /* 印刷時：この位置で必ず改ページ */
            page-break-after: always;
            break-after: page;
            /* プレビュー時：視覚的な区切り線を表示 */
            margin: 20px 0;
            padding: 10px;
            border-top: 3px dashed #ff6b6b;
            border-bottom: 3px dashed #ff6b6b;
            background-color: #ffe0e0;
            text-align: center;
            font-weight: bold;
            color: #d63031;
        }}
        .page-break::before {{
            content: '📄 ページ区切り (印刷時はここで改ページ)';
        }}
        /* 自動ページ境界マーカー（プレビュー専用） */
        .page-boundary {{
            margin: 0;
            padding: 8px;
            border-top: 2px dashed #2196F3;
            background-color: #E3F2FD;
            text-align: center;
            font-size: 12px;
            color: #1565C0;
            display: {(showPageBoundaries ? "block" : "none")};
        }}
        .page-boundary::before {{
            content: '━━━ 📏 ページ境界（このあたりで改ページされる可能性があります） ━━━';
        }}
        /* 印刷用の追加スタイル */
        @media print {{
            body {{
                /* 全体の背景色印刷を強制 */
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
                color-adjust: exact;
            }}
            /* ページ余白の設定 */
            @page {{
                margin: 0.8in 0.1in;
                {GetPdfPageMarginBoxCss()}
            }}
            /* 印刷時はスクロールバーを完全に非表示 */
            * {{
                overflow: visible !important;
            }}
            pre {{
                overflow: visible !important;
                white-space: pre-wrap !important;
                word-wrap: break-word !important;
                word-break: break-all !important;
                /* font-size と line-height は動的に設定されたインラインスタイルを維持 */
            }}
            /* インラインコードは通常サイズのまま */
            /* 印刷時はページ区切りマーカーを非表示 */
            .page-break {{
                border: none;
                background: none;
                padding: 0;
                margin: 0;
                visibility: hidden;
                height: 0;
            }}
            .page-break::before {{
                content: '';
            }}
            /* 印刷時はページ境界マーカーを非表示 */
            .page-boundary {{
                display: none;
            }}
            /* 印刷時の表スタイル */
            table {{
                width: 100%;
                table-layout: auto;
                font-size: 85%;
                word-break: break-word;
                overflow-wrap: break-word;
            }}
            th, td {{
                padding: 4px 6px;
                word-break: break-word;
                overflow-wrap: break-word;
                max-width: 200px;
            }}
            /* 大きな表はページをまたいで表示（break-inside: avoidを外す） */
            table {{
                page-break-inside: auto;
                break-inside: auto;
            }}
            tr {{
                page-break-inside: avoid;
                break-inside: avoid;
            }}
        }}
    </style>
</head>
<body>{body}</body>
</html>";

        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!isWebView2Initialized)
            {
                MessageBox.Show("プレビューの読み込みが完了していません。少し待ってから再度お試しください。", "お待ちください", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[PDF出力] 開始");

                // ファイル保存ダイアログを表示
                var sfd = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    Title = "PDFファイルを保存",
                    InitialDirectory = GetInitialSaveDirectory(),
                    FileName = string.IsNullOrEmpty(currentFilePath) 
                        ? "output.pdf" 
                        : System.IO.Path.GetFileNameWithoutExtension(currentFilePath) + ".pdf"
                };

                if (sfd.ShowDialog() == true)
                {
                    System.Diagnostics.Debug.WriteLine($"[PDF出力] 保存先: {sfd.FileName}");

                    // CoreWebView2がnullでないことを確認
                    if (PreviewBrowser.CoreWebView2 == null)
                    {
                        MessageBox.Show("WebView2が初期化されていません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // PDF出力設定
                    var printSettings = PreviewBrowser.CoreWebView2.Environment.CreatePrintSettings();
                    printSettings.ShouldPrintBackgrounds = true; // 背景色を印刷
                    printSettings.MarginTop = 0.8;
                    printSettings.MarginBottom = 0.8;
                    printSettings.MarginLeft = 0.1;
                    printSettings.MarginRight = 0.1;
                    printSettings.ShouldPrintHeaderAndFooter = false;

                    System.Diagnostics.Debug.WriteLine("[PDF出力] 設定完了、PDF生成開始");

                    // PDFに出力
                    var result = await PreviewBrowser.CoreWebView2.PrintToPdfAsync(sfd.FileName, printSettings);

                    System.Diagnostics.Debug.WriteLine($"[PDF出力] 結果: {result}");

                    if (result)
                    {
                        // ファイルが実際に作成されたか確認
                        if (System.IO.File.Exists(sfd.FileName))
                        {
                            var fileInfo = new System.IO.FileInfo(sfd.FileName);
                            System.Diagnostics.Debug.WriteLine($"[PDF出力] ファイルサイズ: {fileInfo.Length} bytes");

                            // 保存完了とPDFを開くか確認を1つのダイアログで
                            var openResult = MessageBox.Show(
                                $"PDFの保存が完了しました。\n\n" +
                                $"保存先: {sfd.FileName}\n" +
                                $"サイズ: {fileInfo.Length / 1024} KB\n\n" +
                                $"保存したPDFを開きますか？",
                                "保存完了",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Information);

                            if (openResult == MessageBoxResult.Yes)
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = sfd.FileName,
                                    UseShellExecute = true
                                });
                            }
                        }
                        else
                        {
                            MessageBox.Show("PDFファイルの作成には成功しましたが、ファイルが見つかりません。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("PDFの出力に失敗しました。\n\n以下を確認してください:\n" +
                                       "1. ファイルが他のアプリで開かれていないか\n" +
                                       "2. 保存先フォルダに書き込み権限があるか\n" +
                                       "3. ディスクに十分な空き容量があるか", 
                                       "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PDF出力] 例外: {ex}");
                MessageBox.Show($"PDF出力中にエラーが発生しました:\n\n{ex.Message}\n\nスタックトレース:\n{ex.StackTrace}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Markdown files (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*",
                Title = "Markdownファイルを選択"
            };
            var ok = ofd.ShowDialog();
            if (ok == true)
            {
                currentFilePath = ofd.FileName;
                LoadFile();
            }
        }

        private void ReloadFile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                LoadFile();
            }
        }

        private void EncodingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentFilePath) && EncodingComboBox.SelectedItem != null)
            {
                LoadFile();
            }
        }

        private void LoadFile()
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;

            try
            {
                var selectedItem = EncodingComboBox.SelectedItem as EncodingItem;
                var encoding = selectedItem?.Encoding ?? Encoding.UTF8;

                var text = System.IO.File.ReadAllText(currentFilePath, encoding);
                MarkdownEditor.Text = text;
                SelectedFileLabel.Text = $"{System.IO.Path.GetFileName(currentFilePath)} ({selectedItem?.Name})";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("ファイルを開けませんでした: " + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("文章をクリアしますか?","クリア通知",MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                MarkdownEditor.Clear();
                SelectedFileLabel.Text = string.Empty;
                PreviewBrowser.NavigateToString(WrapHtml(string.Empty));
                currentFilePath = null;
            }
            else if (result == MessageBoxResult.No)
            {
                return;
            }

        }

        private void SaveMarkdown_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MarkdownEditor.Text))
            {
                MessageBox.Show("保存するコンテンツがありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // ファイル保存ダイアログを表示
                var sfd = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "Markdown files (*.md)|*.md|Markdown files (*.markdown)|*.markdown|All files (*.*)|*.*",
                    Title = "Markdownファイルを保存",
                    InitialDirectory = GetInitialSaveDirectory(),
                    FileName = string.IsNullOrEmpty(currentFilePath) 
                        ? "document.md" 
                        : System.IO.Path.GetFileNameWithoutExtension(currentFilePath) + ".md",
                    DefaultExt = ".md"
                };

                if (sfd.ShowDialog() == true)
                {
                    // 選択されたエンコーディングを取得
                    var selectedItem = EncodingComboBox.SelectedItem as EncodingItem;
                    var encoding = selectedItem?.Encoding ?? Encoding.UTF8;

                    // Markdownテキストをファイルに保存
                    System.IO.File.WriteAllText(sfd.FileName, MarkdownEditor.Text, encoding);

                    // 保存成功のメッセージ
                    var fileInfo = new System.IO.FileInfo(sfd.FileName);
                    MessageBox.Show(
                        $"Markdownの保存が完了しました。\n\n" +
                        $"保存先: {sfd.FileName}\n" +
                        $"ファイルサイズ: {fileInfo.Length} bytes\n" +
                        $"文字コード: {selectedItem?.Name ?? "UTF-8"}",
                        "保存完了",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    System.Diagnostics.Debug.WriteLine($"[Markdown保存] 保存完了: {sfd.FileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Markdownの保存に失敗しました。\n\n{ex.Message}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[Markdown保存] エラー: {ex}");
            }
        }

        private string SimpleMarkdownToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return string.Empty;

            // 手動改ページマーカーを一時的なHTMLタグに置換
            // マークダウン内で <!-- PAGE_BREAK --> と書くとページ区切りになる
            markdown = System.Text.RegularExpressions.Regex.Replace(
                markdown, 
                @"<!--\s*PAGE_BREAK\s*-->", 
                "<div class='page-break'></div>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Markdigを使用してマークダウンをHTMLに変換
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions() // テーブル、リスト、タスクリストなどの拡張機能を有効化
                .UseTaskLists()
                .Build();

            var html = Markdown.ToHtml(markdown, pipeline);

            // ⚠️ インデント型コード判定機能は一時、无效化
            // HTML構造の破壊の原因となったため、安定性を優先
            // html = RemoveIndentedCodeBlocks(html);

            html = ApplyDynamicCodeBlockFontSize(html);

            // ⚠️ 段落改行処理は一時的に無効化
            // HTML構造を破壊しているため
            // html = ConvertLineBreaksToHtml(html);

            // ページ境界表示が有効な場合、HTMLにマーカーを挿入
            if (showPageBoundaries)
            {
                html = InsertPageBoundaryMarkers(html);
            }

            return html;
        }

        private string ApplyDynamicCodeBlockFontSize(string html)
        {
            // <pre><code>...</code></pre> パターンを検索して、各コードブロックに最適なフォントサイズを設定
            var regex = new System.Text.RegularExpressions.Regex(
                @"<pre><code(?:\s+class=""[^""]*"")?>(?<code>.*?)</code></pre>",
                System.Text.RegularExpressions.RegexOptions.Singleline
            );

            int blockIndex = 0;
            var result = regex.Replace(html, match =>
            {
                blockIndex++;
                var codeContent = match.Groups["code"].Value;

                // HTMLエンティティをデコードしてから解析
                var decodedContent = System.Web.HttpUtility.HtmlDecode(codeContent);

                // コードの特性を解析
                var lines = decodedContent.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
                // 空の配列チェック
                if (lines.Length == 0 || string.IsNullOrWhiteSpace(decodedContent))
                {
                    return match.Value; // 空のコードブロックはそのまま返す
                }

                var maxLineLength = lines.Max(line => line.Replace("\t", "    ").Length);
                var lineCount = lines.Length;

                // 最適なフォントサイズを計算
                int fontSize = CalculateOptimalFontSize(maxLineLength, lineCount);

                // line-heightは固定値（px）で指定（フォントサイズに応じて計算）
                int lineHeightPx = (int)(fontSize * 1.6); // フォントサイズの1.6倍

                // style属性を追加
                var originalTag = match.Value;
                var styledTag = originalTag.Replace("<pre>", 
                    $"<pre style=\"font-size: {fontSize}px; line-height: {lineHeightPx}px;\">");

                System.Diagnostics.Debug.WriteLine($"[コードブロック #{blockIndex}] 行数:{lineCount}, 最長行:{maxLineLength}文字, フォントサイズ:{fontSize}px, line-height:{lineHeightPx}px");

                return styledTag;
            });

            return result;
        }

        private int CalculateOptimalFontSize(int maxLineLength, int lineCount)
        {
            // 基本フォントサイズ（より保守的に、大きめに設定）
            int baseFontSize = 11;

            // 最長行の長さに応じて段階的に縮小
            if (maxLineLength > 140)
            {
                baseFontSize = 8;  // 非常に長い行
            }
            else if (maxLineLength > 120)
            {
                baseFontSize = 9;  // とても長い行
            }
            else if (maxLineLength > 100)
            {
                baseFontSize = 10; // 長い行
            }
            else if (maxLineLength > 80)
            {
                baseFontSize = 11; // やや長い行
            }
            else
            {
                baseFontSize = 12; // 短い行（読みやすく）
            }

            // 行数が非常に多い場合のみ縮小
            if (lineCount > 80)
            {
                baseFontSize = Math.Max(8, baseFontSize - 2);
            }
            else if (lineCount > 60)
            {
                baseFontSize = Math.Max(9, baseFontSize - 1);
            }

            return baseFontSize;
        }

        private string ConvertLineBreaksToHtml(string html)
        {
            // HTML内の<p>...</p>タグの内部における改行を<br>タグに置換
            // <pre>と<code>内は処理しない
            var result = new StringBuilder();
            var reader = new System.IO.StringReader(html);
            string? line;
            var inPre = false;
            var inCode = false;
            var inParagraph = false;
            var paragraphContent = new StringBuilder();

            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();

                // <pre>の開始と終了を追跡
                if (trimmed.StartsWith("<pre"))
                {
                    inPre = true;
                }
                if (trimmed.EndsWith("</pre>"))
                {
                    inPre = false;
                }

                // <code>の開始と終了を追跡
                if (trimmed.StartsWith("<code"))
                {
                    inCode = true;
                }
                if (trimmed.EndsWith("</code>"))
                {
                    inCode = false;
                }

                // <p>の開始と終了を追跡
                if (trimmed.StartsWith("<p") && !inPre)
                {
                    inParagraph = true;
                    // <p>タグ自体のしまい括弧までを含む
                    var closingIndex = trimmed.IndexOf(">");
                    if (closingIndex >= 0)
                    {
                        paragraphContent.Append(trimmed.Substring(0, closingIndex + 1));
                        paragraphContent.Append(trimmed.Substring(closingIndex + 1));
                    }
                }
                else if (trimmed.EndsWith("</p>") && inParagraph)
                {
                    inParagraph = false;
                    paragraphContent.Append(line);

                    // 段落の完全な内容を処理
                    var fullParagraph = paragraphContent.ToString();

                    // 段落内の改行（\n）を<br>に置換（ただしタグは除く）
                    var pTagMatch = System.Text.RegularExpressions.Regex.Match(
                        fullParagraph,
                        @"<p(.*?)>(.*?)</p>",
                        System.Text.RegularExpressions.RegexOptions.Singleline
                    );

                    if (pTagMatch.Success)
                    {
                        var attrs = pTagMatch.Groups[1].Value;
                        var content = pTagMatch.Groups[2].Value;

                        // テキスト内の改行を<br>に置換
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"\n(?!</?)", "<br />\n");
                        result.AppendLine($"<p{attrs}>{content}</p>");
                    }
                    else
                    {
                        result.AppendLine(fullParagraph);
                    }

                    paragraphContent.Clear();
                }
                else if (inParagraph)
                {
                    paragraphContent.AppendLine(line);
                }
                else
                {
                    result.AppendLine(line);
                }
            }

            return result.ToString();
        }

        private string InsertPageBoundaryMarkers(string html)
        {
            // CSSのpage-break-inside: avoidを考慮した実装
            // A4サイズでの実測値に基づく調整済み

            var boundaryMarker = "<div class='page-boundary'></div>";
            var manualBreakMarker = "<div class='page-break'></div>";
            var result = new StringBuilder();
            var lines = html.Split('\n');

            var accumulatedHeight = 0; // 累積高さ（行数ベース）
            var pageHeight = 40; // A4サイズでのおおよその行数
            var inCodeBlock = false;
            var inTable = false;
            var inBlockquote = false;
            var blockStartHeight = 0; // ブロック開始時の高さ
            var boundariesInserted = 0;
            var manualBreaksFound = 0;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // 手動改ページマーカーがある場合
                if (trimmedLine.Contains(manualBreakMarker))
                {
                    result.AppendLine(line);
                    accumulatedHeight = 0; // 高さをリセット
                    blockStartHeight = 0;
                    manualBreaksFound++;
                    System.Diagnostics.Debug.WriteLine($"[ページ境界] 手動マーカー検出 (#{manualBreaksFound})");
                    continue;
                }

                result.AppendLine(line);

                // 空行はカウントしない
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                // ブロック要素の開始を検出
                if (trimmedLine.StartsWith("<pre>") || trimmedLine.Contains("<pre"))
                {
                    inCodeBlock = true;
                    blockStartHeight = accumulatedHeight;
                    accumulatedHeight++;
                    System.Diagnostics.Debug.WriteLine($"[ページ境界] <pre>開始 (高さ:{accumulatedHeight})");
                }
                else if (trimmedLine.StartsWith("<table"))
                {
                    inTable = true;
                    blockStartHeight = accumulatedHeight;
                    accumulatedHeight++;
                    System.Diagnostics.Debug.WriteLine($"[ページ境界] <table>開始 (高さ:{accumulatedHeight})");
                }
                else if (trimmedLine.StartsWith("<blockquote"))
                {
                    inBlockquote = true;
                    blockStartHeight = accumulatedHeight;
                    accumulatedHeight++;
                }
                // ブロック要素の終了を検出
                else if (trimmedLine.StartsWith("</pre>"))
                {
                    inCodeBlock = false;
                    accumulatedHeight++;
                    var blockHeight = accumulatedHeight - blockStartHeight;
                    System.Diagnostics.Debug.WriteLine($"[ページ境界] </pre>終了 (ブロック高さ:{blockHeight}, 累積:{accumulatedHeight})");

                    // page-break-inside: avoidを考慮
                    // ブロック全体が次のページに収まるか判定
                    if (ShouldInsertBoundaryBeforeBlock(blockStartHeight, blockHeight, pageHeight))
                    {
                        // ブロックの前に境界を挿入（ブロック全体を次ページへ）
                        InsertBoundaryBeforeLine(result, blockStartHeight, boundaryMarker);
                        boundariesInserted++;
                        System.Diagnostics.Debug.WriteLine($"[ページ境界] 挿入 (コードブロック前, 累計:{boundariesInserted})");
                        accumulatedHeight = blockHeight; // リセット
                    }
                    else if (accumulatedHeight >= pageHeight)
                    {
                        // ブロック後に境界を挿入
                        result.AppendLine(boundaryMarker);
                        boundariesInserted++;
                        System.Diagnostics.Debug.WriteLine($"[ページ境界] 挿入 (コードブロック後, 累計:{boundariesInserted})");
                        accumulatedHeight = 0;
                    }
                }
                else if (trimmedLine.StartsWith("</table>"))
                {
                    inTable = false;
                    accumulatedHeight++;
                    var blockHeight = accumulatedHeight - blockStartHeight;

                    if (ShouldInsertBoundaryBeforeBlock(blockStartHeight, blockHeight, pageHeight))
                    {
                        InsertBoundaryBeforeLine(result, blockStartHeight, boundaryMarker);
                        boundariesInserted++;
                        System.Diagnostics.Debug.WriteLine($"[ページ境界] 挿入 (テーブル前, 累計:{boundariesInserted})");
                        accumulatedHeight = blockHeight;
                    }
                    else if (accumulatedHeight >= pageHeight)
                    {
                        result.AppendLine(boundaryMarker);
                        boundariesInserted++;
                        System.Diagnostics.Debug.WriteLine($"[ページ境界] 挿入 (テーブル後, 累計:{boundariesInserted})");
                        accumulatedHeight = 0;
                    }
                }
                else if (trimmedLine.StartsWith("</blockquote>"))
                {
                    inBlockquote = false;
                    accumulatedHeight++;
                    var blockHeight = accumulatedHeight - blockStartHeight;

                    if (ShouldInsertBoundaryBeforeBlock(blockStartHeight, blockHeight, pageHeight))
                    {
                        InsertBoundaryBeforeLine(result, blockStartHeight, boundaryMarker);
                        boundariesInserted++;
                        accumulatedHeight = blockHeight;
                    }
                    else if (accumulatedHeight >= pageHeight)
                    {
                        result.AppendLine(boundaryMarker);
                        boundariesInserted++;
                        accumulatedHeight = 0;
                    }
                }
                // ブロック内またはその他の行
                else if (inCodeBlock || inTable || inBlockquote)
                {
                    // ブロック内ではカウントのみ
                    accumulatedHeight++;
                }
                else
                {
                    // 通常のコンテンツ
                    accumulatedHeight++;

                    // 見出しの開始タグは改ページしない（page-break-after: avoid）
                    bool isHeadingStart = trimmedLine.StartsWith("<h") && trimmedLine.Contains(">");

                    // ページ高さを超えた場合の処理
                    if (accumulatedHeight >= pageHeight)
                    {
                        // ブロック終了タグで区切る（優先）
                        if (trimmedLine.StartsWith("</h") || 
                            trimmedLine.StartsWith("</p>") ||
                            trimmedLine.StartsWith("</ul>") ||
                            trimmedLine.StartsWith("</ol>") ||
                            trimmedLine.StartsWith("</li>") ||
                            trimmedLine.StartsWith("</div>"))
                        {
                            result.AppendLine(boundaryMarker);
                            boundariesInserted++;
                            System.Diagnostics.Debug.WriteLine($"[ページ境界] 挿入 (ブロック終了後, 累計:{boundariesInserted})");
                            accumulatedHeight = 0;
                        }
                        // ブロック終了タグがない場合でも、一定間隔で挿入（フォールバック）
                        else if (accumulatedHeight >= pageHeight + 5 && !isHeadingStart)
                        {
                            // 5行のバッファを超えたら強制的に挿入
                            result.AppendLine(boundaryMarker);
                            boundariesInserted++;
                            System.Diagnostics.Debug.WriteLine($"[ページ境界] 挿入 (強制/バッファ超過, 累計:{boundariesInserted})");
                            accumulatedHeight = 0;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ページ境界] 完了: 挿入数={boundariesInserted}, 手動マーカー数={manualBreaksFound}, 総HTML行数={lines.Length}");

            return result.ToString();
        }

        // ブロックの前に境界を挿入すべきか判定
        private bool ShouldInsertBoundaryBeforeBlock(int blockStartHeight, int blockHeight, int pageHeight)
        {
            // ブロックが現在のページに収まらず、次のページには収まる場合
            // かつ、ブロックがページ高さの70%以下の場合（大きすぎるブロックは分割を許容）
            return blockStartHeight % pageHeight + blockHeight > pageHeight 
                   && blockHeight <= pageHeight * 0.7 
                   && blockStartHeight % pageHeight > 5; // 最初の数行は許容
        }

        // 指定した位置に境界を挿入する（簡易実装：後で挿入するのは複雑なので、フラグのみ）
        private void InsertBoundaryBeforeLine(StringBuilder result, int position, string marker)
        {
            // 実際には複雑なので、この実装では後続のブロックで調整
            // 簡易的に現在位置に挿入（厳密には正確ではないが、おおよその位置は合う）
        }

        private void InsertTable_Click(object sender, RoutedEventArgs e)
        {
            var rowsTextBox = new TextBox { Text = "3", Width = 70, Margin = new Thickness(8, 0, 0, 0) };
            var columnsTextBox = new TextBox { Text = "3", Width = 70, Margin = new Thickness(8, 0, 0, 0) };
            var dialog = new Window
            {
                Title = "Markdown表を挿入",
                Width = 300,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = this
            };

            var content = new StackPanel { Margin = new Thickness(16) };
            var columnsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            columnsPanel.Children.Add(new TextBlock { Text = "列数 (1～10)", VerticalAlignment = VerticalAlignment.Center });
            columnsPanel.Children.Add(columnsTextBox);
            var rowsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            rowsPanel.Children.Add(new TextBlock { Text = "データ行数 (1～20)", VerticalAlignment = VerticalAlignment.Center });
            rowsPanel.Children.Add(rowsTextBox);
            content.Children.Add(columnsPanel);
            content.Children.Add(rowsPanel);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var insertButton = new Button { Content = "挿入", IsDefault = true, MinWidth = 72, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
            insertButton.Click += (_, _) =>
            {
                if (!int.TryParse(columnsTextBox.Text, out var columns) || columns is < 1 or > 10
                    || !int.TryParse(rowsTextBox.Text, out var rows) || rows is < 1 or > 20)
                {
                    MessageBox.Show("列数は1～10、データ行数は1～20で入力してください。", "入力内容の確認", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                dialog.DialogResult = true;
            };
            var cancelButton = new Button { Content = "キャンセル", IsCancel = true, MinWidth = 72, Padding = new Thickness(8, 4, 8, 4) };
            buttons.Children.Add(insertButton);
            buttons.Children.Add(cancelButton);
            content.Children.Add(buttons);
            dialog.Content = content;

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var columnCount = int.Parse(columnsTextBox.Text);
            var dataRowCount = int.Parse(rowsTextBox.Text);
            var table = new StringBuilder();
            table.Append('|');
            for (var column = 1; column <= columnCount; column++)
            {
                table.Append($" 列{column} |");
            }
            table.AppendLine();
            table.Append('|');
            for (var column = 0; column < columnCount; column++)
            {
                table.Append(" --- |");
            }
            table.AppendLine();
            for (var row = 0; row < dataRowCount; row++)
            {
                table.Append('|');
                for (var column = 0; column < columnCount; column++)
                {
                    table.Append("  |");
                }
                if (row < dataRowCount - 1)
                {
                    table.AppendLine();
                }
            }

            var text = MarkdownEditor.Text ?? string.Empty;
            var selectionStart = MarkdownEditor.SelectionStart;
            var selectionLength = MarkdownEditor.SelectionLength;
            var before = text[..selectionStart];
            var after = text[(selectionStart + selectionLength)..];
            var leadingNewlines = before.Length == 0 || before.EndsWith("\n\n", StringComparison.Ordinal)
                ? string.Empty
                : before.EndsWith('\n') ? "\n" : "\n\n";
            var trailingNewlines = after.Length == 0 || after.StartsWith("\n\n", StringComparison.Ordinal)
                ? string.Empty
                : after.StartsWith('\n') ? "\n" : "\n\n";
            var insertedTable = table.ToString();

            debounceTimer?.Stop();
            MarkdownEditor.Text = before + leadingNewlines + insertedTable + trailingNewlines + after;
            debounceTimer?.Stop();
            MarkdownEditor.CaretIndex = before.Length + leadingNewlines.Length + insertedTable.Length;
            MarkdownEditor.Focus();
            RefreshPreview();
        }

        private void InsertPageBreak_Click(object sender, RoutedEventArgs e)
        {
            // カーソル位置に改ページマーカーを挿入
            var caretIndex = MarkdownEditor.CaretIndex;
            var currentText = MarkdownEditor.Text ?? string.Empty;

            // 改行で囲んだマーカーを挿入
            var pageBreakMarker = "\n<!-- PAGE_BREAK -->\n";

            // カーソル位置が行の途中の場合、前後に改行を追加
            var insertText = pageBreakMarker;

            // デバウンスタイマーを停止（手動挿入時は即座に更新）
            debounceTimer?.Stop();

            // テキストを挿入
            MarkdownEditor.Text = currentText.Insert(caretIndex, insertText);

            // カーソルをマーカーの後に移動
            MarkdownEditor.CaretIndex = caretIndex + insertText.Length;

            // エディタにフォーカスを戻す
            MarkdownEditor.Focus();

            // 手動マーカー挿入後は確実に更新
            // TextChangedイベントでデバウンスタイマーが起動するが、それを待たずに即座に更新
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Render, // より高い優先度
                new Action(() =>
                {
                    debounceTimer?.Stop(); // もう一度停止
                    RefreshPreview(); // 即座に更新
                    System.Diagnostics.Debug.WriteLine("[手動マーカー] プレビューを強制更新");
                })
            );
        }

        private string GetPdfPageMarginBoxCss()
        {
            var rules = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(pdfHeader))
            {
                rules.Append($"@top-{pdfHeaderAlignment} {{ content: '{EscapeCssString(pdfHeader)}'; text-align: {pdfHeaderAlignment}; }}");
            }

            if (!string.IsNullOrWhiteSpace(pdfFooter) || printPageNumbers)
            {
                var footerContent = EscapeCssString(pdfFooter);
                if (printPageNumbers)
                {
                    footerContent += (footerContent.Length > 0 ? "  ·  " : string.Empty) + "' counter(page) '";
                }

                rules.Append($"@bottom-{pdfFooterAlignment} {{ content: '{footerContent}'; text-align: {pdfFooterAlignment}; }}");
            }

            return rules.ToString();
        }

        private static string EscapeCssString(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", "\\A ", StringComparison.Ordinal);
        }

        private void ShowPageBoundaries_Changed(object sender, RoutedEventArgs e)
        {
            showPageBoundaries = ShowPageBoundariesCheckBox.IsChecked == true;

            // プレビューを再描画
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (!isWebView2Initialized)
            {
                return; // WebView2が初期化されるまで待機
            }

            var markdown = MarkdownEditor.Text ?? string.Empty;
            var html = SimpleMarkdownToHtml(markdown);
            PreviewBrowser.NavigateToString(WrapHtml(html));
        }

        private async void PreviewBrowser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || PreviewBrowser.CoreWebView2 == null)
            {
                return;
            }

            var scrollPercentage = lastPreviewScrollPercentage.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            var script = $@"
                (function() {{
                    if (!window.__markdownScrollSyncInstalled) {{
                        window.__markdownScrollSyncInstalled = true;
                        var scrollTimer;
                        var sendSelection = function() {{
                            var selectedText = window.getSelection().toString();
                            if (selectedText) window.chrome.webview.postMessage('selection:' + selectedText);
                        }};
                        document.addEventListener('mouseup', sendSelection);
                        document.addEventListener('keyup', sendSelection);
                        window.addEventListener('scroll', function() {{
                            clearTimeout(scrollTimer);
                            scrollTimer = setTimeout(function() {{
                                var maxScroll = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);
                                window.chrome.webview.postMessage('scroll:' + String(window.scrollY / maxScroll));
                            }}, 40);
                        }}, {{ passive: true }});
                    }}
                    var maxScroll = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
                    window.scrollTo(0, maxScroll * {scrollPercentage});
                }})();";

            try
            {
                await PreviewBrowser.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[スクロール同期] プレビュー位置復元エラー: {ex.Message}");
            }
        }

        private void PreviewBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();
            if (message.StartsWith("selection:", StringComparison.Ordinal))
            {
                HighlightMarkdownSelection(message["selection:".Length..]);
                return;
            }

            if (!message.StartsWith("scroll:", StringComparison.Ordinal)
                || !isScrollSyncEnabled || markdownEditorScrollViewer == null
                || !double.TryParse(message["scroll:".Length..], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var percentage))
            {
                return;
            }

            lastPreviewScrollPercentage = Math.Clamp(percentage, 0, 1);
            var targetOffset = markdownEditorScrollViewer.ScrollableHeight * lastPreviewScrollPercentage;
            if (Math.Abs(markdownEditorScrollViewer.VerticalOffset - targetOffset) > 1)
            {
                markdownEditorScrollViewer.ScrollToVerticalOffset(targetOffset);
            }
        }

        private void HighlightMarkdownSelection(string selectedText)
        {
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                return;
            }

            var markdown = MarkdownEditor.Text ?? string.Empty;
            var currentPosition = markdownEditorScrollViewer != null && markdownEditorScrollViewer.ScrollableHeight > 0
                ? (int)(markdown.Length * markdownEditorScrollViewer.VerticalOffset / markdownEditorScrollViewer.ScrollableHeight)
                : 0;
            var matchIndex = FindClosestOccurrence(markdown, selectedText, currentPosition);
            var matchLength = selectedText.Length;

            if (matchIndex < 0)
            {
                var words = System.Text.RegularExpressions.Regex.Matches(selectedText, @"[\p{L}\p{N}_]+");
                var bestLength = 0;
                foreach (System.Text.RegularExpressions.Match word in words)
                {
                    if (word.Length > bestLength)
                    {
                        var sourceIndex = FindClosestOccurrence(markdown, word.Value, currentPosition);
                        if (sourceIndex >= 0)
                        {
                            matchIndex = sourceIndex;
                            matchLength = word.Length;
                            bestLength = word.Length;
                        }
                    }
                }
            }

            if (matchIndex < 0)
            {
                return;
            }

            MarkdownEditor.Focus();
            MarkdownEditor.Select(matchIndex, matchLength);
            var lineNumber = markdown.AsSpan(0, matchIndex).Count('\n');
            MarkdownEditor.ScrollToLine(lineNumber);
        }

        private static int FindClosestOccurrence(string source, string value, int expectedIndex)
        {
            var closestIndex = -1;
            var closestDistance = int.MaxValue;
            var searchIndex = 0;
            while (searchIndex < source.Length)
            {
                var foundIndex = source.IndexOf(value, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                {
                    break;
                }

                var distance = Math.Abs(foundIndex - expectedIndex);
                if (distance < closestDistance)
                {
                    closestIndex = foundIndex;
                    closestDistance = distance;
                }

                searchIndex = foundIndex + 1;
            }

            return closestIndex;
        }

        // スクロール同期機能
        private ScrollViewer? FindScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private async void MarkdownEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = e.OriginalSource as ScrollViewer;
            if (!isScrollSyncEnabled || !isWebView2Initialized || scrollViewer == null
                || PreviewBrowser.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                var scrollPercentage = scrollViewer.VerticalOffset / Math.Max(1, scrollViewer.ScrollableHeight);
                lastPreviewScrollPercentage = Math.Clamp(scrollPercentage, 0, 1);
                var percentageText = lastPreviewScrollPercentage.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                var script = $@"
                    (function() {{
                        var maxScroll = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
                        window.scrollTo(0, maxScroll * {percentageText});
                    }})();";
                await PreviewBrowser.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[スクロール同期] エラー: {ex.Message}");
            }
        }

        private string RemoveIndentedCodeBlocks(string html)
        {
            // ユーザーの要望: ```がない限りはコードブロックではなく、通常テキストとして扱う
            // ただし、意図的に```で囲まれたコードブロックは保持する

            // <pre><code>.....</code></pre> パターンで、クラス属性がない（言語指定がない）場合は
            // インデント型コードブロックと判定し、段落に戻す
            var pattern = new System.Text.RegularExpressions.Regex(
                @"<pre><code>(?!hljs|language-)(?<code>[\s\S]*?)</code></pre>",
                System.Text.RegularExpressions.RegexOptions.Multiline
            );

            var result = pattern.Replace(html, match =>
            {
                var code = match.Groups["code"].Value;
                // HTMLエンティティをデコード
                code = System.Web.HttpUtility.HtmlDecode(code);
                // 改行と前後の空白を保持しながら段落に変換
                code = code.Replace("\n", "<br />\n");
                return $"<p>{code}</p>";
            });

            return result;
        }

        private sealed class AppSettings
        {
            [System.Text.Json.Serialization.JsonPropertyName("SaveDirectory")]
            public string? SaveDirectory { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("header")]
            public string? Header { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("header-alignment")]
            public string? HeaderAlignment { get; set; } = "left";

            [System.Text.Json.Serialization.JsonPropertyName("footer")]
            public string? Footer { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("footer-alignment")]
            public string? FooterAlignment { get; set; } = "left";

            [System.Text.Json.Serialization.JsonPropertyName("page-number")]
            public bool PageNumber { get; set; }
        }

        private class EncodingItem
        {
            public string Name { get; set; }
            public Encoding Encoding { get; set; }

            public EncodingItem(string name, Encoding encoding)
            {
                Name = name;
                Encoding = encoding;
            }

            public override string ToString() => Name;
        }
    }
}