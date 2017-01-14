using System;
using System.Drawing;
using Common;

namespace FaceClassifier
{
	public static class DataRectangleOfHistogramOfGradientExtensions
	{
		/// <summary>
		/// It may be helpful to visualise generated Histogram of Gradient data - this method may be used to generate an image that illustrates the histograms. Each histogram
		/// will be plotted as a square (with edge of length hogPreviewCellSize) in which the gradient angles will be drawn. The source image that the data was extracted from
		/// may be passed in, in which case it will be drawn in the background so that it's possible to see which pixels contributed to which gradients. It's also possible to
		/// have the bounds of each square drawn (by passing true for outlineAreasReducedToHistograms) to make this even clearer. Note that the data represents how quickly
		/// light is changing within the image but the data is rotated by ninety degrees which means that the final image looks more akin to an edge-finding algorithm
		/// (I think that this makes it easier to visually see how the histogram data corresponds to the source image).
		/// </summary>
		public static Bitmap GeneratePreviewImage(
			this DataRectangle<HistogramOfGradient> hogs,
			int hogPreviewCellSize = 64,
			Bitmap optionalSourceImageIfOverlayingGradientOnTop = null,
			bool outlineAreasReducedToHistograms = false)
		{
			if (hogs == null)
				throw new ArgumentNullException(nameof(hogs));
			if (hogPreviewCellSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(hogPreviewCellSize));

			var hogBitmap = new Bitmap(hogs.Width * hogPreviewCellSize, hogs.Height * hogPreviewCellSize);
			var color = Color.Red;
			using (var g = Graphics.FromImage(hogBitmap))
			{
				if (optionalSourceImageIfOverlayingGradientOnTop != null)
				{
					// The optionalSourceImageIfOverlayingGradientOnTop (if non-null) should be the approximately the same aspect ratio as the HOG data but there might
					// be slight discrepancies due to the way in which blocks of pixels are taken and HoGs generated for. To be on the safe side, the source image needs
					// to be resized to fit inside the new bitmap and offset slightly if the aspect ratios weren't quite a perfect match.
					var hogDataAspectRatio = (double)hogs.Width / hogs.Height;
					var souceImageAspectRatio = (double)optionalSourceImageIfOverlayingGradientOnTop.Width / optionalSourceImageIfOverlayingGradientOnTop.Height;
					var scaleSourceImageBy = (souceImageAspectRatio >= hogDataAspectRatio)
						? (double)hogBitmap.Width / optionalSourceImageIfOverlayingGradientOnTop.Width
						: (double)hogBitmap.Height / optionalSourceImageIfOverlayingGradientOnTop.Height;
					var newSourceDimensions = new Size(
						(int)Math.Min(Math.Round(optionalSourceImageIfOverlayingGradientOnTop.Width * scaleSourceImageBy), hogBitmap.Width),
						(int)Math.Min(Math.Round(optionalSourceImageIfOverlayingGradientOnTop.Height * scaleSourceImageBy), hogBitmap.Height)
					);
					var offset = new PointF(
						(hogBitmap.Width - newSourceDimensions.Width) / 2,
						(hogBitmap.Height - newSourceDimensions.Height) / 2
					);
					using (var resizedSourceImage = new Bitmap(optionalSourceImageIfOverlayingGradientOnTop, newSourceDimensions))
					{
						g.DrawImage(resizedSourceImage, offset);
					}
				}

				using (var linePen = new Pen(Color.FromArgb(64, color), width: 4))
				{
					for (var x = 0; x < hogs.Width; x++)
					{
						for (var y = 0; y < hogs.Height; y++)
						{
							// Rotating the histogram by 90 degrees will result in the image appearing to trace edges, which makes it a little easier to envisage how it contains the content for a face
							var hog = hogs[x, y].Rotate90Degrees();
							var centreForBlockX = (x * hogPreviewCellSize) + (hogPreviewCellSize / 2);
							var centreForBlockY = (y * hogPreviewCellSize) + (hogPreviewCellSize / 2);
							var anglesAndMagnitude = new[]
							{
								Tuple.Create(10, hog.Degrees10),
								Tuple.Create(30, hog.Degrees30),
								Tuple.Create(50, hog.Degrees50),
								Tuple.Create(70, hog.Degrees70),
								Tuple.Create(90, hog.Degrees90),
								Tuple.Create(110, hog.Degrees110),
								Tuple.Create(130, hog.Degrees130),
								Tuple.Create(150, hog.Degrees150),
								Tuple.Create(170, hog.Degrees170)
							};
							var maxLengthOfLineSegmentFromCentrePoint = hogPreviewCellSize / 2;
							foreach (var angleAndMagnitude in anglesAndMagnitude)
							{
								// Note: Since we're using zero degrees to mean up but we're also taking the origin point to be the top-left of the image (rather than the bottom-left), we need
								// to invest the result of the Math.Cos calculation in order to determine how far DOWN to go
								var angle = angleAndMagnitude.Item1;
								var magnitude = angleAndMagnitude.Item2;
								var lineSegmentLength = magnitude * maxLengthOfLineSegmentFromCentrePoint;
								var across = Math.Sin(DegreeToRadian(angle)) * lineSegmentLength;
								var down = Math.Cos(DegreeToRadian(angle)) * -lineSegmentLength;
								g.DrawLine(linePen, (float)(centreForBlockX - across), (float)(centreForBlockY - down), (float)(centreForBlockX + across), (float)(centreForBlockY + down));
							}
							hogBitmap.SetPixel(centreForBlockX, centreForBlockY, color);
							if (outlineAreasReducedToHistograms)
							{
								g.DrawRectangle(
									linePen,
									(float)(centreForBlockX - maxLengthOfLineSegmentFromCentrePoint),
									(float)(centreForBlockY - maxLengthOfLineSegmentFromCentrePoint),
									hogPreviewCellSize,
									hogPreviewCellSize
								);
							}
						}
					}
				}
			}
			return hogBitmap;
		}

		private static double DegreeToRadian(double angle)
		{
			return angle / (180d / Math.PI);
		}
	}
}
