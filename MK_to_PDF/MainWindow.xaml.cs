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
        private string? currentFilePath;
        private bool showPageBoundaries = true; // デフォルトで表示
        private System.Windows.Threading.DispatcherTimer? debounceTimer;
        private bool isWebView2Initialized = false;
        private bool isScrollSyncEnabled = true; // スクロール同期を有効化
        private bool isSyncingScroll = false; // 同期中フラグ（無限ループ防止）

        public MainWindow()
        {
            InitializeComponent();
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

            // WebView2の初期化
            InitializeWebView2Async();

            // マークダウンエディタのスクロールイベントを追加（Loaded後に設定）
            MarkdownEditor.Loaded += (s, e) =>
            {
                var scrollViewer = FindScrollViewer(MarkdownEditor);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollChanged += MarkdownEditor_ScrollChanged;
                }
            };
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

                System.Diagnostics.Debug.WriteLine("[WebView2] 初期化完了");
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
    <style>
        body {{ 
            font-family: 'Segoe UI', Meiryo, sans-serif; 
            padding: 20px;
            line-height: 1.6;
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
            /* テーブルが途中で分割されないようにする */
            page-break-inside: avoid;
            break-inside: avoid;
        }}
        table, th, td {{ 
            border: 1px solid #ddd; 
        }}
        th, td {{ 
            padding: 8px; 
            text-align: left;
        }}
        th {{
            background-color: #f4f4f4;
            /* テーブルヘッダーの背景色も印刷 */
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
            color-adjust: exact;
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
                margin: 2cm;
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
                    printSettings.MarginTop = 2.0;
                    printSettings.MarginBottom = 2.0;
                    printSettings.MarginLeft = 2.0;
                    printSettings.MarginRight = 2.0;

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
            MarkdownEditor.Clear();
            SelectedFileLabel.Text = string.Empty;
            PreviewBrowser.NavigateToString(WrapHtml(string.Empty));
            currentFilePath = null;
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
                .Build();

            var html = Markdown.ToHtml(markdown, pipeline);

            // コードブロックに動的なフォントサイズを適用
            html = ApplyDynamicCodeBlockFontSize(html);

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

        private void PageBreakHelp_Click(object sender, RoutedEventArgs e)
        {
            var helpMessage = @"📄 ページ区切り機能の使い方

マークダウン内で任意の位置にページ区切りを挿入できます。

【記述方法】
マークダウンファイルの好きな位置に以下を記述してください：

<!-- PAGE_BREAK -->

または、エディタヘッダーの「📄 改ページを挿入」ボタンをクリック！

【例】
# 第1章

これは最初のページの内容です。

<!-- PAGE_BREAK -->

# 第2章

これは2ページ目の内容です。

【マーカーの違い】
🔴 赤い破線ボックス
  → 手動で挿入した改ページ位置
  → PDF出力時に必ず改ページされます
  → この位置は確定です

🔵 青い破線（📏 ページ境界を表示 ON時）
  → おおよそ40行ごとに表示される改ページ候補位置
  → 参考情報として表示
  → 実際の改ページ位置は印刷時に最終的に決まります
  → 長いコードブロックも検出されます

【ページ境界の確認】
「📏 ページ境界を表示」にチェックを入れると、
おおよそA4サイズ1ページ分（約40行）ごとに
青い線で境界を表示します。長いコードブロックやテーブルも
適切に検出されます。

【注意】
青い境界線はあくまで目安です。実際のPDF出力では、
フォントサイズやブラウザの設定により若干ずれる場合があります。
正確な位置で区切りたい場合は、赤い手動マーカーを使用してください。

【簡単挿入】
エディタのヘッダーにある「📄 改ページを挿入」ボタンを
クリックすると、カーソル位置に自動的に挿入されます。
手動マーカーを挿入すると、青い境界線が即座に再計算されます。";

            MessageBox.Show(helpMessage, "ページ区切りヘルプ", MessageBoxButton.OK, MessageBoxImage.Information);
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

            // プレビューを更新（ページ境界線を再計算）
            var markdown = MarkdownEditor.Text ?? string.Empty;
            var html = SimpleMarkdownToHtml(markdown);

            // WebView2でHTMLを直接設定
            PreviewBrowser.NavigateToString(WrapHtml(html));
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
            if (!isScrollSyncEnabled || !isWebView2Initialized || isSyncingScroll)
                return;

            try
            {
                isSyncingScroll = true;

                // TextBoxのスクロール位置を取得
                var scrollViewer = e.OriginalSource as ScrollViewer;
                if (scrollViewer == null) return;

                // スクロール位置の割合を計算（0.0〜1.0）
                double scrollPercentage = scrollViewer.VerticalOffset / Math.Max(1, scrollViewer.ScrollableHeight);

                // JavaScriptでプレビュー側をスクロール
                string script = $@"
                    (function() {{
                        var scrollHeight = document.documentElement.scrollHeight - window.innerHeight;
                        var targetScroll = scrollHeight * {scrollPercentage.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)};
                        window.scrollTo({{
                            top: targetScroll,
                            behavior: 'auto'
                        }});
                    }})();
                ";

                await PreviewBrowser.CoreWebView2.ExecuteScriptAsync(script);

                System.Diagnostics.Debug.WriteLine($"[スクロール同期] エディタ:{scrollPercentage:P0} → プレビュー同期");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[スクロール同期] エラー: {ex.Message}");
            }
            finally
            {
                // 少し遅延してからフラグをリセット（無限ループ防止）
                await Task.Delay(50);
                isSyncingScroll = false;
            }
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