using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Markdig;

namespace MK_to_PDF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? currentFilePath;

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
        }

        private void MarkdownEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 簡易的にMarkdownをHTMLに変換して組み込みの WebBrowser に表示
            var markdown = MarkdownEditor.Text ?? string.Empty;
            var html = SimpleMarkdownToHtml(markdown);
            PreviewBrowser.NavigateToString(WrapHtml(html));
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
        }}
        pre {{ 
            background-color: #f4f4f4; 
            padding: 10px; 
            border-radius: 5px;
            overflow-x: auto;
        }}
        pre code {{
            background-color: transparent;
            padding: 0;
        }}
        blockquote {{ 
            border-left: 4px solid #ddd; 
            margin: 0;
            padding-left: 16px; 
            color: #666;
        }}
        table {{ 
            border-collapse: collapse; 
            width: 100%;
            margin: 10px 0;
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
        }}
    </style>
</head>
<body>{body}</body>
</html>";

        private async void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            // WebBrowser の印刷ダイアログを表示してユーザーに Microsoft Print to PDF を選ばせる方法
            try
            {
                // Try to invoke window.print() in the hosted browser
                PreviewBrowser.InvokeScript("execScript", new object[] { "window.print();", "JavaScript" });
            }
            catch
            {
                MessageBox.Show("印刷ダイアログを開けませんでした。WebView2 を導入すると自動PDF出力が可能です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            // Markdigを使用してマークダウンをHTMLに変換
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions() // テーブル、リスト、タスクリストなどの拡張機能を有効化
                .Build();

            return Markdown.ToHtml(markdown, pipeline);
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