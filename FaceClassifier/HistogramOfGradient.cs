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
	}
}
