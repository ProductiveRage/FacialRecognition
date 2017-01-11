using System;

namespace FaceClassifier
{
	public struct HistogramOfGradient
	{
		public HistogramOfGradient(double degrees10, double degrees30, double degrees50, double degrees70, double degrees90, double degrees110, double degrees130, double degrees150, double degrees170)
		{
			if (degrees10 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees10));
			if (degrees10 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees10));
			if (degrees50 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees50));
			if (degrees70 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees70));
			if (degrees90 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees90));
			if (degrees110 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees110));
			if (degrees130 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees130));
			if (degrees150 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees150));
			if (degrees170 < 0)
				throw new ArgumentOutOfRangeException(nameof(degrees170));

			Degrees10 = degrees10;
			Degrees30 = degrees30;
			Degrees50 = degrees50;
			Degrees70 = degrees70;
			Degrees90 = degrees90;
			Degrees110 = degrees110;
			Degrees130 = degrees130;
			Degrees150 = degrees150;
			Degrees170 = degrees170;
		}
		public double Degrees10 { get; }
		public double Degrees30 { get; }
		public double Degrees50 { get; }
		public double Degrees70 { get; }
		public double Degrees90 { get; }
		public double Degrees110 { get; }
		public double Degrees130 { get; }
		public double Degrees150 { get; }
		public double Degrees170 { get; }

		public double SumOfMagnitudes
		{
			get { return Degrees10 + Degrees30 + Degrees50 + Degrees70 + Degrees90 + Degrees110 + Degrees130 + Degrees150 + Degrees170; }
		}
		public double GreatestMagnitude
		{
			get { return Math.Max(Degrees10, Math.Max(Degrees30, Math.Max(Degrees50, Math.Max(Degrees70, Math.Max(Degrees90, Math.Max(Degrees110, Math.Max(Degrees130, Math.Max(Degrees150, Degrees170)))))))); }
		}

		public HistogramOfGradient Multiply(double multiplyValuesBy)
		{
			return new HistogramOfGradient(
				Degrees10 * multiplyValuesBy,
				Degrees30 * multiplyValuesBy,
				Degrees50 * multiplyValuesBy,
				Degrees70 * multiplyValuesBy,
				Degrees90 * multiplyValuesBy,
				Degrees110 * multiplyValuesBy,
				Degrees130 * multiplyValuesBy,
				Degrees150 * multiplyValuesBy,
				Degrees170 * multiplyValuesBy
			);
		}

		/// <summary>
		/// This will return a HistogramOfGradient where the magnitudes sum to one (while maintaining the ratios of the curent instance)
		/// </summary>
		public HistogramOfGradient Normalise()
		{
			var sum = SumOfMagnitudes;
			if (sum == 0)
			{
				// The sum could be zero, if a flat image was processed (where there would be no gradients). In this case we could consider returning zero but, since
				// normalising is a transformation to return a HoG that sums to one then it makes sense to return equally spread values across all nine bins.
				var value = 1d / 9;
				return new HistogramOfGradient(value, value, value, value, value, value, value, value, value);
			}

			// Normalise the content (essentially, ensure that the sum of the values comes to one)
			return new HistogramOfGradient(
				Degrees10 / sum,
				Degrees30 / sum,
				Degrees50 / sum,
				Degrees70 / sum,
				Degrees90 / sum,
				Degrees110 / sum,
				Degrees130 / sum,
				Degrees150 / sum,
				Degrees170 / sum
			);
		}
	}
}
