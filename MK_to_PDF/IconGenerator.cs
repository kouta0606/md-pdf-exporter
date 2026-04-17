using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;

namespace MK_to_PDF
{
    [SupportedOSPlatform("windows")]
    public static class IconGenerator
    {
        public static void GenerateAppIcon(string outputPath)
        {
            // 複数のサイズのアイコンを生成
            int[] sizes = { 16, 32, 48, 64, 128, 256 };
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ICOヘッダー
            bw.Write((short)0); // Reserved
            bw.Write((short)1); // Type: 1 = Icon
            bw.Write((short)sizes.Length); // Number of images

            var imageDataList = new List<byte[]>();
            long offset = 6 + (16 * sizes.Length); // Header + Directory entries

            foreach (var size in sizes)
            {
                var imageData = GenerateIconImage(size);
                imageDataList.Add(imageData);

                // Directory entry
                bw.Write((byte)(size == 256 ? 0 : size)); // Width (0 means 256)
                bw.Write((byte)(size == 256 ? 0 : size)); // Height
                bw.Write((byte)0); // Color palette
                bw.Write((byte)0); // Reserved
                bw.Write((short)1); // Color planes
                bw.Write((short)32); // Bits per pixel
                bw.Write((int)imageData.Length); // Image data size
                bw.Write((int)offset); // Offset to image data
                offset += imageData.Length;
            }

            // Write image data
            foreach (var imageData in imageDataList)
            {
                bw.Write(imageData);
            }

            File.WriteAllBytes(outputPath, ms.ToArray());
        }

        private static byte[] GenerateIconImage(int size)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            // 背景のグラデーション
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, size, size),
                Color.FromArgb(33, 150, 243), // #2196F3
                Color.FromArgb(13, 71, 161),  // #0D47A1
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(brush, 0, 0, size, size);
            }

            // 角を丸くする
            using (var path = new GraphicsPath())
            {
                int cornerRadius = size / 8;
                path.AddArc(0, 0, cornerRadius * 2, cornerRadius * 2, 180, 90);
                path.AddArc(size - cornerRadius * 2, 0, cornerRadius * 2, cornerRadius * 2, 270, 90);
                path.AddArc(size - cornerRadius * 2, size - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 0, 90);
                path.AddArc(0, size - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
                path.CloseFigure();
                g.SetClip(path);
            }

            // 再度グラデーションを描画（クリッピング適用のため）
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, size, size),
                Color.FromArgb(33, 150, 243),
                Color.FromArgb(13, 71, 161),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(brush, 0, 0, size, size);
            }

            // テキスト "MD" を描画
            string text = "MD";
            float fontSize = size * 0.35f;
            using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var textSize = g.MeasureString(text, font);
                float x = (size - textSize.Width) / 2;
                float y = (size - textSize.Height) / 2;
                g.DrawString(text, font, textBrush, x, y);
            }

            // PDF アイコンを追加（小さな矢印）
            if (size >= 32)
            {
                int arrowSize = size / 6;
                int arrowX = size - arrowSize - size / 10;
                int arrowY = size - arrowSize - size / 10;

                using (var arrowBrush = new SolidBrush(Color.FromArgb(200, 76, 175, 80))) // #4CAF50
                using (var arrowPath = new GraphicsPath())
                {
                    arrowPath.AddPolygon(new PointF[]
                    {
                        new PointF(arrowX, arrowY),
                        new PointF(arrowX + arrowSize, arrowY + arrowSize / 2),
                        new PointF(arrowX, arrowY + arrowSize)
                    });
                    g.FillPath(arrowBrush, arrowPath);
                }
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}
