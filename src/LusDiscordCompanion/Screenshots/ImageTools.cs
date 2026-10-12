using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace LusDiscordCompanion
{
    public sealed class PreparedImage
    {
        public byte[] Data;
        public string FileName;
    }

    public static class ImageTools
    {
        public const long MaxUploadBytes = 9500000;

        public static PreparedImage Prepare(string path)
        {
            var data = File.ReadAllBytes(path);
            var name = Path.GetFileName(path);

            if (string.Equals(Path.GetExtension(path), ".tga", StringComparison.OrdinalIgnoreCase))
            {
                data = TgaToPng(data);
                name = Path.ChangeExtension(name, ".png");
            }

            if (data.Length >= MaxUploadBytes)
            {
                data = ShrinkToJpeg(data);
                name = Path.ChangeExtension(name, ".jpg");
            }

            return new PreparedImage { Data = data, FileName = name };
        }

        public static byte[] TgaToPng(byte[] tga)
        {
            using (var bitmap = DecodeTga(tga))
            using (var output = new MemoryStream())
            {
                bitmap.Save(output, ImageFormat.Png);
                return output.ToArray();
            }
        }

        public static Bitmap DecodeTga(byte[] tga)
        {
            if (tga.Length < 18)
                throw new InvalidDataException("TGA file is too small.");

            int idLength = tga[0];
            int colorMapType = tga[1];
            int imageType = tga[2];
            int colorMapLength = tga[5] | (tga[6] << 8);
            int colorMapEntryBits = tga[7];
            int width = tga[12] | (tga[13] << 8);
            int height = tga[14] | (tga[15] << 8);
            int bpp = tga[16];
            bool topToBottom = (tga[17] & 0x20) != 0;

            if (imageType != 2 && imageType != 10)
                throw new InvalidDataException("Unsupported TGA image type " + imageType + ".");
            if (bpp != 24 && bpp != 32)
                throw new InvalidDataException("Unsupported TGA bit depth " + bpp + ".");
            if (width == 0 || height == 0)
                throw new InvalidDataException("TGA image has no pixels.");

            int bytesPerPixel = bpp / 8;
            int offset = 18 + idLength + (colorMapType == 1 ? colorMapLength * ((colorMapEntryBits + 7) / 8) : 0);
            int pixelCount = width * height;
            var argb = new int[pixelCount];

            Func<int, int> readPixel = at =>
                unchecked((int)0xFF000000) | (tga[at + 2] << 16) | (tga[at + 1] << 8) | tga[at];

            if (imageType == 2)
            {
                if (offset + (long)pixelCount * bytesPerPixel > tga.Length)
                    throw new InvalidDataException("TGA file is truncated.");
                for (int i = 0; i < pixelCount; i++)
                    argb[i] = readPixel(offset + i * bytesPerPixel);
            }
            else
            {
                int i = 0, pos = offset;
                while (i < pixelCount)
                {
                    if (pos >= tga.Length)
                        throw new InvalidDataException("TGA file is truncated.");
                    int header = tga[pos++];
                    int count = (header & 0x7F) + 1;
                    bool isRun = header >= 0x80;
                    if (i + count > pixelCount || pos + (isRun ? 1 : count) * bytesPerPixel > tga.Length)
                        throw new InvalidDataException("TGA RLE data is corrupt.");
                    if (isRun)
                    {
                        int pixel = readPixel(pos);
                        pos += bytesPerPixel;
                        for (int n = 0; n < count; n++)
                            argb[i++] = pixel;
                    }
                    else
                    {
                        for (int n = 0; n < count; n++, pos += bytesPerPixel)
                            argb[i++] = readPixel(pos);
                    }
                }
            }

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    int sourceRow = topToBottom ? y : height - 1 - y;
                    Marshal.Copy(argb, sourceRow * width, bits.Scan0 + y * bits.Stride, width);
                }
            }
            finally
            {
                bitmap.UnlockBits(bits);
            }
            return bitmap;
        }

        public static byte[] ShrinkToJpeg(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var source = new Bitmap(input))
            using (var parameters = new EncoderParameters(1))
            {
                var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);

                double scale = 1.0;
                while (true)
                {
                    var encoded = EncodeJpeg(source, scale, codec, parameters);
                    if (encoded.Length < MaxUploadBytes || source.Width * scale < 64)
                        return encoded;
                    scale *= 0.8;
                }
            }
        }

        static byte[] EncodeJpeg(Bitmap source, double scale, ImageCodecInfo codec, EncoderParameters parameters)
        {
            using (var output = new MemoryStream())
            {
                if (scale >= 1.0)
                {
                    source.Save(output, codec, parameters);
                    return output.ToArray();
                }

                int width = Math.Max(1, (int)(source.Width * scale));
                int height = Math.Max(1, (int)(source.Height * scale));
                using (var scaled = new Bitmap(width, height, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(source, 0, 0, width, height);
                    }
                    scaled.Save(output, codec, parameters);
                }
                return output.ToArray();
            }
        }
    }
}
