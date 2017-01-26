using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Common;

namespace FaceDetection
{
	public sealed class FaceDetector
	{
		private readonly IExposeConfigurationOptions _config;
		private readonly Action<string> _logger;
		public FaceDetector(IExposeConfigurationOptions config, Action<string> logger)
		{
			if (config == null)
				throw new ArgumentNullException(nameof(config));
			if (logger == null)
				throw new ArgumentNullException(nameof(logger));

			_config = config;
			_logger = logger;
		}
		public FaceDetector(Action<string> logger) : this(DefaultConfiguration.Instance, logger) { }

		public IEnumerable<Rectangle> GetPossibleFaceRegions(DataRectangle<RGB> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var scale = _config.CalculateScale(source.Width, source.Height);
			_logger($"Loaded file - Dimensions: {source.Width}x{source.Height}, Scale: {scale}");

			var colourData = CorrectZeroResponse(source);
			_logger("Corrected zero response");

			var values = _config.IRgByCalculator(colourData);
			_logger("Calculated I/RgBy values");

			// To compute texture amplitude -
			//  1. The intensity image was smoothed with a median filter of radius 4 * SCALE (8 for Jay Kapur method)
			//  2. The result was subtracted from the original image
			//  3. The absolute values of these differences are then run through a second median filter of radius 6 * SCALE (12 for Jay Kapur method)
			var smoothedIntensity = MedianFilter(values, value => value.I, _config.TextureAmplitudeFirstPassSmoothenMultiplier * scale);
			var differenceBetweenOriginalIntensityAndSmoothIntensity = values.CombineWith(smoothedIntensity, (x, y) => Math.Abs(x.I - y));
			var textureAmplitude = MedianFilter(differenceBetweenOriginalIntensityAndSmoothIntensity, value => value, _config.TextureAmplitudeSecondPassSmoothenMultiplier * scale);
			_logger("Calculated texture amplitude");

			// The Rg and By arrays are smoothed with a median filter of radius 2 * SCALE, to reduce noise.
			var smoothedRg = MedianFilter(values, value => value.Rg, _config.RgBySmoothenMultiplier * scale);
			var smoothedBy = MedianFilter(values, value => value.By, _config.RgBySmoothenMultiplier * scale);
			var smoothedHues = smoothedRg.CombineWith(
				smoothedBy,
				(rg, by, coordinates) =>
				{
					var hue = RadianToDegree(Math.Atan2(rg, by));
					var saturation = Math.Sqrt((rg * rg) + (by * by));
					return new HueSaturation(hue, saturation, textureAmplitude[coordinates.X, coordinates.Y]);
				}
			);
			_logger("Calculated hue data");

			// Generate a mask of pixels identified as skin
			var skinMask = smoothedHues.Transform(transformer: _config.SkinFilter);
			_logger("Built initial skin mask");

			using (var skinMaskPreviewImage = new Bitmap(skinMask.Width, skinMask.Height))
			{
				skinMaskPreviewImage.SetRGB(
					skinMask.Transform(isSkin => isSkin ? new RGB(255, 255, 255) : new RGB(0, 0, 0))
				);
				skinMaskPreviewImage.Save("SkinMask1.png");
			}
			
			// Now expand the mask to include any adjacent points that match a less strict filter (which "helps to enlarge the skin map regions to include skin/background
			// border pixels, regions near hair or other features, or desaturated areas" - as per Jay Kapur, though he recommends five iterations and I think that a slightly
			// higher value may provide better results)
			for (var i = 0; i < _config.NumberOfSkinMaskRelaxedExpansions; i++)
			{
				skinMask = skinMask.CombineWith(
					smoothedHues,
					(mask, hue, coordinates) =>
					{
						if (mask)
							return true;
						if (!_config.RelaxedSkinFilter(hue))
							return false;
						var surroundingArea = GetRectangleAround(smoothedHues, coordinates, distanceToExpandLeftAndUp: 1, distanceToExpandRightAndDown: 1);
						return skinMask.AnyValuesMatch(surroundingArea, adjacentMask => adjacentMask);
					}
				);
			}
			_logger($"Expanded initial skin mask (fixed loop count of {_config.NumberOfSkinMaskRelaxedExpansions})");

			// Jay Kapur takes the skin map and multiplies by a greyscale conversion of the original image, then stretches the histogram to improve contrast, finally taking a
			// threshold of 95-240 to mark regions that show skin areas. This is approximated here by combining the skin map with greyscale'd pixels from the original data and
			// using a slightly different threshold range.
			skinMask = colourData.CombineWith(
				skinMask,
				(colour, mask) =>
				{
					if (!mask)
						return false;
					var intensity = colour.ToGreyScale();
					return (intensity >= 90) && (intensity <= 240);
				}
			);
			_logger("Completed final skin mask");

			using (var skinMaskPreviewImage = new Bitmap(skinMask.Width, skinMask.Height))
			{
				skinMaskPreviewImage.SetRGB(
					skinMask.Transform(isSkin => isSkin ? new RGB(255, 255, 255) : new RGB(0, 0, 0))
				);
				skinMaskPreviewImage.Save("SkinMask2.png");
			}

			var faceRegions = _config.FaceRegionAspectRatioFilter(
					IdentifyFacesFromSkinMask(skinMask)
				)
				.Select(faceRegion => ExpandRectangle(faceRegion, _config.PercentToExpandFinalFaceRegionBy, new Size(source.Width, source.Height)))
				.ToArray();
			_logger("Identified face regions");
			return faceRegions;
		}

		private static Rectangle ExpandRectangle(Rectangle area, double percentageToAdd, Size imageSize)
		{
			if ((area.Left < 0) || (area.Top < 0) || (area.Right > imageSize.Width) || (area.Bottom > imageSize.Height))
				throw new ArgumentOutOfRangeException(nameof(area));
			if (percentageToAdd < 0)
				throw new ArgumentOutOfRangeException(nameof(percentageToAdd));
			if ((imageSize.Width <= 0) || (imageSize.Height <= 0))
				throw new ArgumentOutOfRangeException(nameof(imageSize));

			area.Inflate((int)Math.Round(area.Width * percentageToAdd), (int)Math.Round(area.Height * percentageToAdd)); // Rectangle is a struct so we're not messing with the caller's Rectangle reference
			area.Intersect(new Rectangle(new Point(0, 0), imageSize));
			return area;
		}

		private IEnumerable<Rectangle> IdentifyFacesFromSkinMask(DataRectangle<bool> skinMask)
		{
			if (skinMask == null)
				throw new ArgumentNullException(nameof(skinMask));

			// Identify potential objects from positive image (build a list of all skin points, take the first one and flood fill from it - recording the results as one object
			// and remove all points from the list, then do the same for the next skin point until there are none left)
			var skinPoints = new HashSet<Point>(
				skinMask.Enumerate((point, isMasked) => isMasked).Select(point => point.Item1)
			);
			var scale = _config.CalculateScale(skinMask.Width, skinMask.Height);
			var skinObjects = new List<Point[]>();
			while (skinPoints.Any())
			{
				var currentPoint = skinPoints.First();
				var pointsInObject = TryToGetPointsInObject(skinMask, currentPoint, new Rectangle(0, 0, skinMask.Width, skinMask.Height)).ToArray();
				foreach (var point in pointsInObject)
					skinPoints.Remove(point);
				skinObjects.Add(pointsInObject);
			}
			skinObjects = skinObjects.Where(skinObject => skinObject.Length >= (64 * scale)).ToList(); // Ignore any very small regions


			using (var skinMaskPreviewImage = new Bitmap(skinMask.Width, skinMask.Height))
			{
				Func<Color, RGB> colourToRgb = c => new RGB(c.R, c.G, c.B);
				var colours = new[]
				{
					Color.Red, Color.Gray, Color.Green, Color.Blue, Color.DarkBlue, Color.Yellow, Color.White, Color.Turquoise, Color.Teal, Color.Thistle, Color.SpringGreen, Color.YellowGreen, Color.MintCream, Color.MistyRose,
					Color.Red, Color.Gray, Color.Green, Color.Blue, Color.DarkBlue, Color.Yellow, Color.White, Color.Turquoise, Color.Teal, Color.Thistle, Color.SpringGreen, Color.YellowGreen, Color.MintCream, Color.MistyRose
				};

				var allSkinObjectPoints = skinObjects.Select((o, i) => new { Points = new HashSet<Point>(o), Colour = colours[i] }).ToArray();
				skinMaskPreviewImage.SetRGB(
					skinMask.Transform((isSkin, point) =>
					{
						var firstObject = allSkinObjectPoints.FirstOrDefault(o => o.Points.Contains(point));
						return (firstObject == null) ? new RGB(0, 0, 0) : colourToRgb(firstObject.Colour);
					})
				);
				skinMaskPreviewImage.Save("SkinMask3.png");
			}

			// Look for any fully enclosed holes in each skin object (do this by flood filling from negative points and ignoring any where the fill gets to the edges of object)
			var boundsForSkinObjects = new List<Rectangle>();
			foreach (var skinObject in skinObjects)
			{
				var xValues = skinObject.Select(p => p.X).ToArray();
				var yValues = skinObject.Select(p => p.Y).ToArray();
				var left = xValues.Min();
				var top = yValues.Min();
				var skinObjectBounds = new Rectangle(left, top, width: (xValues.Max() - left) + 1, height: (yValues.Max() - top) + 1);
				var negativePointsInObject = new HashSet<Point>(
					skinMask.Enumerate((point, isMasked) => !isMasked && skinObjectBounds.Contains(point)).Select(point => point.Item1)
				);
				while (negativePointsInObject.Any())
				{
					var currentPoint = negativePointsInObject.First();
					var pointsInFilledNegativeSpace = TryToGetPointsInObject(skinMask, currentPoint, skinObjectBounds).ToArray();
					foreach (var point in pointsInFilledNegativeSpace)
						negativePointsInObject.Remove(point);

					if (pointsInFilledNegativeSpace.Any(p => (p.X == skinObjectBounds.Left) || (p.X == (skinObjectBounds.Right - 1)) || (p.Y == skinObjectBounds.Top) || (p.Y == (skinObjectBounds.Bottom - 1))))
						continue; // Ignore any negative regions that are not fully enclosed within the skin mask
					if (pointsInFilledNegativeSpace.Length <= scale)
						continue; // Ignore any very small regions (likely anomalies)
					boundsForSkinObjects.Add(skinObjectBounds); // Found a non-negligible fully-enclosed hole
					break;
				}
			}
			return boundsForSkinObjects;
		}

		// Based on code from https://simpledevcode.wordpress.com/2015/12/29/flood-fill-algorithm-using-c-net/
		private static IEnumerable<Point> TryToGetPointsInObject(DataRectangle<bool> mask, Point startAt, Rectangle limitTo)
		{
			if (mask == null)
				throw new ArgumentNullException(nameof(mask));
			if ((limitTo.Left < 0) || (limitTo.Right > mask.Width) || (limitTo.Top < 0) || (limitTo.Bottom > mask.Height))
				throw new ArgumentOutOfRangeException(nameof(limitTo));
			if ((startAt.X < limitTo.Left) || (startAt.X > limitTo.Right) || (startAt.Y < limitTo.Top) || (startAt.Y > limitTo.Bottom))
				throw new ArgumentOutOfRangeException(nameof(startAt));

			var valueAtOriginPoint = mask[startAt.X, startAt.Y];

			var pixels = new Stack<Point>();
			pixels.Push(startAt);

			var filledPixels = new HashSet<Point>();
			while (pixels.Count > 0)
			{
				var currentPoint = pixels.Pop();
				if ((currentPoint.X < limitTo.Left) || (currentPoint.X >= limitTo.Right) || (currentPoint.Y < limitTo.Top) || (currentPoint.Y >= limitTo.Bottom)) // make sure we stay within bounds
					continue;

				if ((mask[currentPoint.X, currentPoint.Y] == valueAtOriginPoint) && !filledPixels.Contains(currentPoint))
				{
					filledPixels.Add(new Point(currentPoint.X, currentPoint.Y));
					pixels.Push(new Point(currentPoint.X - 1, currentPoint.Y));
					pixels.Push(new Point(currentPoint.X + 1, currentPoint.Y));
					pixels.Push(new Point(currentPoint.X, currentPoint.Y - 1));
					pixels.Push(new Point(currentPoint.X, currentPoint.Y + 1));
				}
			}
			return filledPixels;
		}


		private static double RadianToDegree(double angle)
		{
			return angle * (180d / Math.PI);
		}

		/// <summary>
		/// This reduces variance in data by breaking it into blocks and overwriting the block's data with a single value - the median value for that block
		/// </summary>
		private static DataRectangle<double> MedianFilter<TSource>(DataRectangle<TSource> values, Func<TSource, double> valueExtractor, int blockSize)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));
			if (valueExtractor == null)
				throw new ArgumentNullException(nameof(valueExtractor));
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			var result = new double[values.Width, values.Height];
			var distanceBetweenPoints = blockSize + 1; // After each media is taken, we need to move past the current pixel and then the expansions distance to get the new centre
			for (var x = 0; x < values.Width; x += distanceBetweenPoints)
			{
				for (var y = 0; y < values.Height; y += distanceBetweenPoints)
				{
					// We start at the top-left and move across and down from there (so GetRectangleAround never expands up and left, it only expands down and right)
					var areaToMedianOver = GetRectangleAround(values, new Point(x, y), distanceToExpandLeftAndUp: 0, distanceToExpandRightAndDown: blockSize);
					var valuesToGetMedianFrom = new double[areaToMedianOver.Width * areaToMedianOver.Height];
					var i = 0;
					for (var xToGetMedianFrom = areaToMedianOver.Left; xToGetMedianFrom < areaToMedianOver.Right; xToGetMedianFrom++)
					{
						for (var yToGetMedianFrom = areaToMedianOver.Top; yToGetMedianFrom < areaToMedianOver.Bottom; yToGetMedianFrom++)
						{
							var value = values[xToGetMedianFrom, yToGetMedianFrom];
							valuesToGetMedianFrom[i] = valueExtractor(value);
							i++;
						}
					}
					var medianValue = valuesToGetMedianFrom.OrderBy(value => value).Skip(valuesToGetMedianFrom.Length / 2).First();
					for (var xToGetMedianFrom = areaToMedianOver.Left; xToGetMedianFrom < areaToMedianOver.Right; xToGetMedianFrom++)
					{
						for (var yToGetMedianFrom = areaToMedianOver.Top; yToGetMedianFrom < areaToMedianOver.Bottom; yToGetMedianFrom++)
							result[xToGetMedianFrom, yToGetMedianFrom] = medianValue;
					}
				}
			}
			return DataRectangle.For(result);
		}

		private static Rectangle GetRectangleAround<T>(DataRectangle<T> values, Point coordinates, int distanceToExpandLeftAndUp, int distanceToExpandRightAndDown)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));
			if ((coordinates.X < 0) || (coordinates.X >= values.Width) || (coordinates.Y < 0) || (coordinates.Y >= values.Height))
				throw new ArgumentOutOfRangeException(nameof(coordinates));
			if (distanceToExpandLeftAndUp < 0)
				throw new ArgumentOutOfRangeException(nameof(distanceToExpandLeftAndUp));
			if (distanceToExpandRightAndDown <= 0)
				throw new ArgumentOutOfRangeException(nameof(distanceToExpandRightAndDown));

			var squareMinX = Math.Max(coordinates.X - distanceToExpandLeftAndUp, 0);
			var squareMaxX = Math.Min(coordinates.X + distanceToExpandRightAndDown, values.Width - 1);
			var squareMinY = Math.Max(coordinates.Y - distanceToExpandLeftAndUp, 0);
			var squareMaxY = Math.Min(coordinates.Y + distanceToExpandRightAndDown, values.Height - 1);
			return new Rectangle(
				x: squareMinX,
				y: squareMinY,
				width: (squareMaxX - squareMinX) + 1,
				height: (squareMaxY - squareMinY) + 1
			);
		}

		private static DataRectangle<RGB> CorrectZeroResponse(DataRectangle<RGB> values)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));

			// Get the smallest value of any RGB component
			var smallestValue = values
				.Enumerate()
				.Select(point => point.Item2)
				.SelectMany(colour => new[] { colour.R, colour.G, colour.B })
				.Min();

			// Subtract this from every RGB component
			return values.Transform(value => new RGB((byte)(value.R - smallestValue), (byte)(value.G - smallestValue), (byte)(value.B - smallestValue)));
		}
	}
}
