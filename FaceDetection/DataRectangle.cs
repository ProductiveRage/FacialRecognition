using System;
using System.Collections.Generic;
using System.Drawing;

namespace FaceDetection
{
	public static class DataRectangle
	{
		public static DataRectangle<T> For<T>(T[,] values)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));
			return new DataRectangle<T>(values);
		}
	}

	public sealed class DataRectangle<T>
	{
		private readonly T[,] _protectedValues;
		public DataRectangle(T[,] values) : this(values, isolationCopyMayBeBypassed: false) { }
		private DataRectangle(T[,] values, bool isolationCopyMayBeBypassed)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));
			if ((values.GetLowerBound(0) != 0) || (values.GetLowerBound(1) != 0))
				throw new ArgumentException("Both dimensions must have lower bound zero");
			if ((values.GetUpperBound(0) == 0) || (values.GetUpperBound(1) == 0))
				throw new ArgumentException($"{nameof(values)} must have at least one element");

			Width = values.GetUpperBound(0) + 1;
			Height = values.GetUpperBound(1) + 1;
			if ((Width == 0) || (Height == 0))
				throw new ArgumentException("zero element arrays are not supported");

			if (isolationCopyMayBeBypassed)
				_protectedValues = values;
			else
			{
				_protectedValues = new T[Width, Height];
				Array.Copy(values, _protectedValues, Width * Height);
			}
		}

		/// <summary>
		/// This will always be greater than zero
		/// </summary>
		public int Width { get; }

		/// <summary>
		/// This will always be greater than zero
		/// </summary>
		public int Height { get; }

		public T this[int x, int y]
		{
			get
			{
				if ((x < 0) || (x >= Width))
					throw new ArgumentOutOfRangeException(nameof(x));
				if ((y < 0) || (y >= Height))
					throw new ArgumentOutOfRangeException(nameof(y));
				return _protectedValues[x, y];
			}
		}

		public IEnumerable<Tuple<Point, T>> Enumerate(Func<T, Point, bool> optionalFilter = null)
		{
			for (var x = 0; x < Width; x++)
			{
				for (var y = 0; y < Height; y++)
				{
					var value = _protectedValues[x, y];
					var point = new Point(x, y);
					if ((optionalFilter == null) || optionalFilter(value, point))
						yield return Tuple.Create(point, value);
				}
			}
		}

		public bool AnyValuesMatch(Rectangle area, Func<T, bool> filter)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));

			return DoValuesMatch(area, filter, minimumNumberOfMatches: 1);
		}

		public bool DoValuesMatch(Rectangle area, Func<T, bool> filter, int minimumNumberOfMatches)
		{
			if ((area.Left < 0) || (area.Right > Width) || (area.Top < 0) || (area.Bottom > Height))
				throw new ArgumentOutOfRangeException(nameof(area));
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (minimumNumberOfMatches <= 0)
				throw new ArgumentOutOfRangeException(nameof(minimumNumberOfMatches));

			var numberOfMatches = 0;
			for (var x = area.Left; x < area.Right; x++)
			{
				for (var y = area.Top; y < area.Bottom; y++)
				{
					if (filter(_protectedValues[x, y]))
					{
						numberOfMatches++;
						if (numberOfMatches >= minimumNumberOfMatches)
							return true;
					}
				}
			}
			return false;
		}

		public DataRectangle<TResult> Transform<TResult>(Func<T, TResult> transformer)
		{
			if (transformer == null)
				throw new ArgumentNullException(nameof(transformer));

			return Transform((value, coordinates) => transformer(value));
		}

		public DataRectangle<TResult> Transform<TResult>(Func<T, Point, TResult> transformer)
		{
			if (transformer == null)
				throw new ArgumentNullException(nameof(transformer));

			var transformed = new TResult[Width, Height];
			for (var x = 0; x < Width; x++)
			{
				for (var y = 0; y < Height; y++)
					transformed[x, y] = transformer(_protectedValues[x, y], new Point(x, y));
			}
			return new DataRectangle<TResult>(transformed, isolationCopyMayBeBypassed: true);
		}

		public DataRectangle<TResult> CombineWith<TOther, TResult>(DataRectangle<TOther> other, Func<T, TOther, TResult> combiner)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			if (combiner == null)
				throw new ArgumentNullException(nameof(combiner));

			return CombineWith(other, (value1, value2, coordinates) => combiner(value1, value2));
		}

		public DataRectangle<TResult> CombineWith<TOther, TResult>(DataRectangle<TOther> other, Func<T, TOther, Point, TResult> combiner)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			if ((other.Width != Width) || (other.Height != Height))
				throw new ArgumentException("other data is different shape");
			if (combiner == null)
				throw new ArgumentNullException(nameof(combiner));

			var result = new TResult[Width, Height];
			for (var x = 0; x < Width; x++)
			{
				for (var y = 0; y < Height; y++)
					result[x, y] = combiner(_protectedValues[x, y], other[x, y], new Point(x, y));
			}
			return new DataRectangle<TResult>(result, isolationCopyMayBeBypassed: true);
		}
	}
}