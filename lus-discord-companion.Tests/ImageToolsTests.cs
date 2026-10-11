using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace LusDiscordCompanion.Tests
{
    public class ImageToolsTests : IDisposable
    {
        private readonly TempFolder _temp = new TempFolder();

        public void Dispose() => _temp.Dispose();

        [Fact]
        public void Large4KPngIsShrunkBelowLimit()
        {
            var path = Path.Combine(_temp.Root, "big.png");
            using (var noise = NoiseBitmap(3840, 2160))
                noise.Save(path, ImageFormat.Png);
            Assert.True(new FileInfo(path).Length > ImageTools.MaxUploadBytes, "test image should exceed the limit");

            var image = ImageTools.Prepare(path);

            Assert.True(image.Data.Length < ImageTools.MaxUploadBytes);
            Assert.Equal("big.jpg", image.FileName);
            using (var result = new Bitmap(new MemoryStream(image.Data)))
                Assert.True(result.Width > 0);
        }

        [Fact]
        public void SmallPngIsUnchanged()
        {
            var path = Path.Combine(_temp.Root, "small.png");
            using (var bmp = new Bitmap(10, 10))
                bmp.Save(path, ImageFormat.Png);

            var image = ImageTools.Prepare(path);

            Assert.Equal(File.ReadAllBytes(path), image.Data);
            Assert.Equal("small.png", image.FileName);
        }

        [Theory]
        [InlineData(24, false, false)]
        [InlineData(32, false, false)]
        [InlineData(24, true, false)]
        [InlineData(32, true, true)]
        public void TgaConvertsToPng(int bpp, bool rle, bool topToBottom)
        {
            var path = Path.Combine(_temp.Root, "shot.tga");
            File.WriteAllBytes(path, BuildTga(3, 2, bpp, rle, topToBottom));

            var image = ImageTools.Prepare(path);

            Assert.Equal("shot.png", image.FileName);
            using (var png = new Bitmap(new MemoryStream(image.Data)))
            {
                Assert.Equal(3, png.Width);
                Assert.Equal(2, png.Height);
                Assert.Equal(Color.FromArgb(255, 255, 0, 0).ToArgb(), png.GetPixel(0, 0).ToArgb());
                Assert.Equal(Color.FromArgb(255, 0, 0, 255).ToArgb(), png.GetPixel(2, 1).ToArgb());
            }
        }

        private static Bitmap NoiseBitmap(int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var bytes = new byte[data.Stride * height];
            new Random(42).NextBytes(bytes);
            for (int i = 3; i < bytes.Length; i += 4)
                bytes[i] = 255;
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            bmp.UnlockBits(data);
            return bmp;
        }

        // Top row: red, green, green. Bottom row: green, green, blue.
        private static byte[] BuildTga(int width, int height, int bpp, bool rle, bool topToBottom)
        {
            Color[,] pixels =
            {
                { Color.Red, Color.Lime, Color.Lime },
                { Color.Lime, Color.Lime, Color.Blue },
            };
            int bytesPerPixel = bpp / 8;
            var output = new List<byte>
            {
                0, 0, (byte)(rle ? 10 : 2), 0, 0, 0, 0, 0, 0, 0, 0, 0,
                (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8),
                (byte)bpp, (byte)((topToBottom ? 0x20 : 0) | (bpp == 32 ? 8 : 0)),
            };

            void AddPixel(Color c)
            {
                output.Add(c.B);
                output.Add(c.G);
                output.Add(c.R);
                if (bytesPerPixel == 4)
                    output.Add(0);
            }

            for (int row = 0; row < height; row++)
            {
                int y = topToBottom ? row : height - 1 - row;
                if (!rle)
                {
                    for (int x = 0; x < width; x++)
                        AddPixel(pixels[y, x]);
                    continue;
                }
                int col = 0;
                while (col < width)
                {
                    int run = 1;
                    while (col + run < width && pixels[y, col + run] == pixels[y, col])
                        run++;
                    output.Add(run > 1 ? (byte)(0x80 | (run - 1)) : (byte)0);
                    AddPixel(pixels[y, col]);
                    col += run;
                }
            }
            return output.ToArray();
        }
    }
}
