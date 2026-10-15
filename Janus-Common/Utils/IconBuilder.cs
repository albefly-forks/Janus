using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

using JetBrains.Annotations;

namespace Rsdn.Janus
{
	/// <summary>
	/// Собирает многоразмерную иконку из набора картинок.
	/// </summary>
	public static class IconBuilder
	{
		/// <summary>
		/// Создать иконку, содержащую все переданные картинки в качестве кадров
		/// (картинки с одинаковыми размерами пропускаются).
		/// </summary>
		[NotNull]
		public static Icon Create([NotNull] IEnumerable<Image> images)
		{
			if (images == null)
				throw new ArgumentNullException(nameof(images));

			var frames = images
				.Where(img => img != null && img.Width <= 256 && img.Height <= 256)
				.GroupBy(img => img.Size)
				.Select(g => g.First())
				.OrderBy(img => img.Width)
				.Select(EncodeFrame)
				.ToArray();
			if (frames.Length == 0)
				throw new ArgumentException("No images to build an icon from", nameof(images));

			var ms = new MemoryStream();
			var writer = new BinaryWriter(ms);
			// ICONDIR
			writer.Write((ushort)0);
			writer.Write((ushort)1);
			writer.Write((ushort)frames.Length);
			// ICONDIRENTRY
			var offset = 6 + 16 * frames.Length;
			foreach (var frame in frames)
			{
				writer.Write((byte)(frame.Size.Width % 256));
				writer.Write((byte)(frame.Size.Height % 256));
				writer.Write((byte)0);
				writer.Write((byte)0);
				writer.Write((ushort)1);
				writer.Write((ushort)32);
				writer.Write(frame.Data.Length);
				writer.Write(offset);
				offset += frame.Data.Length;
			}
			foreach (var frame in frames)
				writer.Write(frame.Data);
			writer.Flush();

			ms.Position = 0;
			return new Icon(ms);
		}

		/// <summary>
		/// Кадр иконки в формате 32-bit BGRA (BITMAPINFOHEADER + пиксели снизу вверх + маска).
		/// </summary>
		private static IconFrame EncodeFrame(Image image)
		{
			var size = image.Size;
			using (var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
			{
				using (var g = Graphics.FromImage(bmp))
					g.DrawImage(image, new Rectangle(Point.Empty, size));

				var maskStride = (size.Width + 31) / 32 * 4;
				var pixelsSize = size.Width * size.Height * 4;
				var ms = new MemoryStream();
				var writer = new BinaryWriter(ms);
				// BITMAPINFOHEADER, высота удвоена - в нее входит маска
				writer.Write(40);
				writer.Write(size.Width);
				writer.Write(size.Height * 2);
				writer.Write((ushort)1);
				writer.Write((ushort)32);
				writer.Write(0);
				writer.Write(pixelsSize);
				writer.Write(0);
				writer.Write(0);
				writer.Write(0);
				writer.Write(0);

				var data = bmp.LockBits(
					new Rectangle(Point.Empty, size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
				try
				{
					var row = new byte[size.Width * 4];
					for (var y = size.Height - 1; y >= 0; y--)
					{
						System.Runtime.InteropServices.Marshal.Copy(
							data.Scan0 + y * data.Stride, row, 0, row.Length);
						writer.Write(row);
					}
				}
				finally
				{
					bmp.UnlockBits(data);
				}

				// Маска прозрачности не нужна - прозрачность в альфа-канале
				writer.Write(new byte[maskStride * size.Height]);
				writer.Flush();
				return new IconFrame(size, ms.ToArray());
			}
		}

		private sealed class IconFrame
		{
			public IconFrame(Size size, byte[] data)
			{
				Size = size;
				Data = data;
			}

			public Size Size { get; }
			public byte[] Data { get; }
		}
	}
}
