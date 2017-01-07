using System.Collections.Generic;
using Common;

namespace UnitTests.Common
{
	public sealed class DataRectangleTestsEqualityComparer<T> : IEqualityComparer<DataRectangle<T>>
	{
		public static DataRectangleTestsEqualityComparer<T> Default => new DataRectangleTestsEqualityComparer<T>();

		private readonly IEqualityComparer<T> _elementComparer;
		private DataRectangleTestsEqualityComparer()
		{
			_elementComparer = EqualityComparer<T>.Default;
		}

		public bool Equals(DataRectangle<T> x, DataRectangle<T> y)
		{
			if ((x == null) && (y == null))
				return true;
			if ((x == null) || (y == null))
				return false;
			if ((x.Width != y.Width) || (x.Height != y.Height))
				return false;
			for (var i = 0; i < x.Width; i++)
			{
				for (var j = 0; j < x.Height; j++)
				{
					if (!_elementComparer.Equals(x[i, j], y[i, j]))
						return false;
				}
			}
			return true;
		}

		public int GetHashCode(DataRectangle<T> obj)
		{
			return 0; // For unit tests, we always want to do deep comparisons so it's fine to always return zero for the hash code
		}
	}
}
