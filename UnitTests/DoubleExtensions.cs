using System;

namespace UnitTests
{
	public static class DoubleExtensions
	{
		/// <summary>
		/// It's often the case that two calculated Double values will very slightly different but not precisely the same. This difference may be slight enough to
		/// consider them equal. In that this method may be used to compare them, it will perform an approximate match.
		/// </summary>
		public static bool HasMinimalDifference(this double value1, double value2, int units = 100) // A default "units" value of 100 means to ignore the last couple of digits
		{
			// From https://msdn.microsoft.com/en-us/library/ya2zha7s(v=vs.110).aspx
			var lValue1 = BitConverter.DoubleToInt64Bits(value1);
			var lValue2 = BitConverter.DoubleToInt64Bits(value2);
			if ((lValue1 >> 63) != (lValue2 >> 63))
			{
				// If the signs are different, return false except for +0 and -0.
				if (value1 == value2)
					return true;

				return false;
			}

			var diff = Math.Abs(lValue1 - lValue2);
			return (diff <= (long)units);
		}
	}
}
