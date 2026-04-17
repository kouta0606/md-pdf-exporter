using System.Configuration;
using System.Data;
using System.Windows;
using System.Runtime.Versioning;
using System.IO;

namespace MK_to_PDF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // アイコンファイルが存在しない場合は生成
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (!File.Exists(iconPath))
            {
                try
                {
                    IconGenerator.GenerateAppIcon(iconPath);
                }
                catch
                {
                    // アイコン生成に失敗してもアプリは起動
                }
            }
        }
    }

}
