using System;
using System.Drawing;
using System.Linq;
using Common;

namespace FaceDetection
{
	public static class DataRectangleExtensions
	{
		/// <summary>
		/// This reduces variance in data by breaking it into blocks and overwriting the block's data with a single value - the median value for that block
		/// </summary>
		public static DataRectangle<double> MedianFilter<TSource>(this DataRectangle<TSource> source, Func<TSource, double> valueExtractor, int blockSize)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (valueExtractor == null)
				throw new ArgumentNullException(nameof(valueExtractor));
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			var result = new double[source.Width, source.Height];
			var distanceBetweenPoints = blockSize + 1; // After each media is taken, we need to move past the current pixel and then the expansions distance to get the new centre
			for (var x = 0; x < source.Width; x += distanceBetweenPoints)
			{
				for (var y = 0; y < source.Height; y += distanceBetweenPoints)
				{
					// We start at the top-left and move across and down from there (so GetRectangleAround never expands up and left, it only expands down and right)
					var areaToMedianOver = source.GetRectangleAround(new Point(x, y), distanceToExpandLeftAndUp: 0, distanceToExpandRightAndDown: blockSize);
					var valuesToGetMedianFrom = new double[areaToMedianOver.Width * areaToMedianOver.Height];
					var i = 0;
					for (var xToGetMedianFrom = areaToMedianOver.Left; xToGetMedianFrom < areaToMedianOver.Right; xToGetMedianFrom++)
					{
						for (var yToGetMedianFrom = areaToMedianOver.Top; yToGetMedianFrom < areaToMedianOver.Bottom; yToGetMedianFrom++)
						{
							var value = source[xToGetMedianFrom, yToGetMedianFrom];
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

		public static Rectangle GetRectangleAround<T>(this DataRectangle<T> values, Point coordinates, int distanceToExpandLeftAndUp, int distanceToExpandRightAndDown)
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
	}
}
