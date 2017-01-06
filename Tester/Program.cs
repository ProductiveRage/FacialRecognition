using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FaceDetection;

namespace Tester
{
	class Program
	{
		static void Main(string[] args)
		{
			var filename = "TigerWoods.gif";
			var outputFilename = "Output.png";

			// This strikes a reasonable balance between successfully matching faces in my current (small) sample data while performing the work quickly
			const int maxAllowedSize = 480;

			var config = DefaultConfiguration.Instance;
			var timer = new IntervalTimer(Console.WriteLine);
			var faceDetector = new FaceDetector(config, timer.Log);
			using (var source = new Bitmap(filename))
			{
				var largestDimension = Math.Max(source.Width, source.Height);
				var scaleDown = (largestDimension > maxAllowedSize) ? ((double)largestDimension / maxAllowedSize) : 1;
				var colourData = (scaleDown > 1) ? GetResizedBitmapData(source, scaleDown) : source.GetRGB();

				var faceRegions = faceDetector.GetPossibleFaceRegions(colourData);
				if (scaleDown > 1)
					faceRegions = faceRegions.Select(region => Scale(region, scaleDown));
				WriteOutputFile(outputFilename, source, faceRegions, Color.GreenYellow);
				timer.Log($"Complete (written to {outputFilename}), {faceRegions.Count()} region(s) identified");
			}
			Console.WriteLine();
			Console.WriteLine("Press [Enter] to terminate..");
			Console.ReadLine();
		}

		private static DataRectangle<RGB> GetResizedBitmapData(Bitmap source, double scale)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (scale <= 0)
				throw new ArgumentOutOfRangeException(nameof(scale));

			using (var resizedSource = GetResizedBitmap(source, scale))
			{
				return resizedSource.GetRGB();
			}
		}

		private static Bitmap GetResizedBitmap(Bitmap source, double scale)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (scale <= 0)
				throw new ArgumentOutOfRangeException(nameof(scale));

			return new Bitmap(source, new Size((int)Math.Round(source.Width / scale), (int)Math.Round(source.Height / scale)));
		}

		private static Rectangle Scale(Rectangle region, double scale)
		{
			if (scale <= 0)
				throw new ArgumentOutOfRangeException(nameof(scale));

			return new Rectangle((int)Math.Round(region.X * scale), (int)Math.Round(region.Y * scale), (int)Math.Round(region.Width * scale), (int)Math.Round(region.Height * scale));
		}

		private static void WriteOutputFile(string outputFilename, Bitmap source, IEnumerable<Rectangle> faceRegions, Color outline)
		{
			if (string.IsNullOrWhiteSpace(outputFilename))
				throw new ArgumentException($"Null/blank {nameof(outputFilename)} specified");
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (faceRegions == null)
				throw new ArgumentNullException(nameof(faceRegions));

			// If the original image uses a palette (ie. an indexed PixelFormat) then GDI+ can't draw rectangle on it so we'll just create a fresh bitmap every time to
			// be on the safe side
			using (var annotatedBitMap = new Bitmap(source.Width, source.Height))
			{
				using (var g = Graphics.FromImage(annotatedBitMap))
				{
					g.DrawImage(source, 0, 0);
					using (var pen = new Pen(outline, width: 1))
					{
						g.DrawRectangles(pen, faceRegions.ToArray());
					}
				}
				annotatedBitMap.Save(outputFilename);
			}
		}
	}
}
