using System;
using System.Collections.Generic;
using System.Drawing;
using Common;

namespace FaceDetection
{
	public static class DataRectangleExtensions
	{
		/// <summary>
		/// This reduces variance in data by replacing each value with the median value from a block drawn around it (it is helpful in reducing noise in an image)
		/// </summary>
		public static DataRectangle<double> MedianFilter<TSource>(this DataRectangle<TSource> source, Func<TSource, double> valueExtractor, int blockSize)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (valueExtractor == null)
				throw new ArgumentNullException(nameof(valueExtractor));
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			return source.Transform(currentValue => valueExtractor(currentValue)).MedianFilter(blockSize);
		}

		private static DataRectangle<double> MedianFilter(this DataRectangle<double> source, int blockSize)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			var result = new double[source.Width, source.Height];
			for (var x = 0; x < source.Width; x++)
			{
				for (var y = 0; y < source.Height; y++)
				{
					var top = Math.Max(0, y - (blockSize / 2));
					var bottom = Math.Min(source.Height, top + blockSize);
					var left = Math.Max(0, x - (blockSize / 2));
					var right = Math.Min(source.Width, left + blockSize);
					var blockWidth = right - left;
					var blockHeight = bottom - top;
					var valuesInArea = new List<double>(capacity: blockWidth * blockHeight);
					for (var xInner = left; xInner < right; xInner++)
					{
						for (var yInner = top; yInner < bottom; yInner++)
						{
							valuesInArea.Add(source[xInner, yInner]); // TODO: Would it be faster to directly access source array?
						}
					}
					valuesInArea.Sort();
					result[x, y] = valuesInArea[valuesInArea.Count / 2];
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
