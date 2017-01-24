using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Common
{
	public static class BitmapExtensions
	{
		public static DataRectangle<RGB> GetRGB(this Bitmap image)
		{
			if (image == null)
				throw new ArgumentNullException(nameof(image));

			return GetRGB(image, 0, 0, image.Width, image.Height);
		}

		// Based on http://stackoverflow.com/a/4748383/3813189
		private static DataRectangle<RGB>GetRGB(this Bitmap image, int startX, int startY, int w, int h)
		{
			if (image == null)
				throw new ArgumentNullException(nameof(image));
			if (startX < 0 || startX + w > image.Width)
				throw new ArgumentOutOfRangeException(nameof(startX));
			if (startY < 0 || startY + h > image.Height)
				throw new ArgumentOutOfRangeException(nameof(startY));
			if ((w <= 0) || (w > image.Width))
				throw new ArgumentOutOfRangeException(nameof(w));
			if ((h <= 0) || (h > image.Height))
				throw new ArgumentOutOfRangeException(nameof(h));

			var values = new RGB[w, h];
			var data = image.LockBits(new Rectangle(startX, startY, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
			try
			{
				var pixelData = new Byte[data.Stride];
				for (var lineIndex = 0; lineIndex < data.Height; lineIndex++)
				{
					Marshal.Copy(
						source: data.Scan0 + (lineIndex * data.Stride),
						destination: pixelData,
						startIndex: 0,
						length: data.Stride
					);
					for (var pixelOffsetWithinLine = 0; pixelOffsetWithinLine < data.Width; pixelOffsetWithinLine++)
					{
						// Note: PixelFormat.Format32bppRgb means the data is stored in memory as BGR (note: we're using Format24bppRgb but the same applies)
						const int PixelWidth = 3;
						values[pixelOffsetWithinLine, lineIndex] = new RGB(
							pixelData[pixelOffsetWithinLine * PixelWidth + 2],
							pixelData[pixelOffsetWithinLine * PixelWidth + 1],
							pixelData[pixelOffsetWithinLine * PixelWidth]
						);
					}
				}
			}
			finally
			{
				image.UnlockBits(data);
			}
			return DataRectangle.For(values);
		}

		public static void SetRGB(this Bitmap image, DataRectangle<RGB> values)
		{
			if (image == null)
				throw new ArgumentNullException(nameof(image));
			if (values == null)
				throw new ArgumentNullException(nameof(values));

			SetRGB(image, 0, 0, values);
		}

		public static void SetRGB(this Bitmap image, int startX, int startY, DataRectangle<RGB> values)
		{
			if (image == null)
				throw new ArgumentNullException(nameof(image));
			if (startX < 0)
				throw new ArgumentOutOfRangeException(nameof(startX));
			if (startY < 0)
				throw new ArgumentOutOfRangeException(nameof(startY));
			if (values == null)
				throw new ArgumentNullException(nameof(values));
			if ((startX + values.Width) > image.Width)
				throw new ArgumentException($"{nameof(startX)} and {values}' first dimension exceed the {nameof(image)} width");
			if ((startY + values.Height) > image.Height)
				throw new ArgumentException($"{nameof(startY)} and {values}' second dimension exceed the {nameof(image)} height");

			var data = image.LockBits(new Rectangle(startX, startY, values.Width, values.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
			if (data.Stride < (values.Width * 3))
				throw new Exception("Bitmap Stride is smaller than expected - not sure what happened there");
			try
			{
				var pixelData = new byte[data.Stride];
				for (var lineIndex = 0; lineIndex < data.Height; lineIndex++)
				{
					for (var pixelOffsetWithinLine = 0; pixelOffsetWithinLine < values.Width; pixelOffsetWithinLine++)
					{
						// Note: Format24bppRgb means that the data is stored in memory as BGR
						var color = values[pixelOffsetWithinLine, lineIndex];
						pixelData[(pixelOffsetWithinLine * 3)] = color.B;
						pixelData[(pixelOffsetWithinLine * 3) + 1] = color.G;
						pixelData[(pixelOffsetWithinLine * 3) + 2] = color.R;
					}
					Marshal.Copy(
						source: pixelData,
						destination: data.Scan0 + (lineIndex * data.Stride),
						startIndex: 0, // startIndex relates to the pixelData array since we've already specified the "offset" into the bitmap by passing the required IntPtr
						length: (values.Width * 3)
					);
				}
			}
			finally
			{
				image.UnlockBits(data);
			}
		}

		public static Bitmap ExtractImageSectionAndResize(this Bitmap source, Rectangle region, Size destinationSize)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if ((region.Left < 0) || (region.Top < 0) || (region.Right > source.Width) || (region.Bottom > source.Height))
				throw new ArgumentOutOfRangeException(nameof(destinationSize));
			if ((destinationSize.Width <= 0) || (destinationSize.Height <= 0))
				throw new ArgumentOutOfRangeException(nameof(destinationSize));

			using (var specifiedArea = new Bitmap(region.Width, region.Height))
			{
				using (var g = Graphics.FromImage(specifiedArea))
				{
					g.DrawImage(
						source,
						destRect: new RectangleF(0, 0, region.Width, region.Height),
						srcRect: new RectangleF(region.X, region.Y, region.Width, region.Height),
						srcUnit: GraphicsUnit.Pixel
					);
				}

				var aspectRatio = (double)region.Width / region.Height;
				var sampleAspectRatio = (double)destinationSize.Width / destinationSize.Height;
				var resizeRatio = (aspectRatio >= sampleAspectRatio) ? ((double)destinationSize.Width / region.Width) : ((double)destinationSize.Height / region.Height);
				var newDimensions = new Size((int)Math.Round(region.Width * resizeRatio), (int)Math.Round(region.Height * resizeRatio));
				var offsetX = (destinationSize.Width - newDimensions.Width) / 2;
				var offsetY = (destinationSize.Height - newDimensions.Height) / 2;

				using (var specifiedAreaResized = new Bitmap(specifiedArea, newDimensions.Width, newDimensions.Height))
				{
					var specifiedAreaResizedAndCentred = new Bitmap(destinationSize.Width, destinationSize.Height);
					try
					{
						using (var g = Graphics.FromImage(specifiedAreaResizedAndCentred))
						{
							using (var background = new SolidBrush(Color.Black))
								g.FillRectangle(background, new Rectangle(0, 0, specifiedAreaResizedAndCentred.Width, specifiedAreaResizedAndCentred.Height));
							g.DrawImage(specifiedAreaResized, new PointF(offsetX, offsetY));
						}
						return specifiedAreaResizedAndCentred;
					}
					catch
					{
						specifiedAreaResizedAndCentred.Dispose();
						throw;
					}
				}
			}
		}
	}
}
