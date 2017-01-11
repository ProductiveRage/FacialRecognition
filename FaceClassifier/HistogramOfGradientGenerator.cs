using System;
using System.Linq;
using Common;

namespace FaceClassifier
{
	public static class HistogramOfGradientGenerator
	{
		public static DataRectangle<HistogramOfGradient> Get(DataRectangle<RGB> source, int blockSize)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			return Get(
				source.Transform(colour => Math.Round(colour.ToGreyScale())),
				blockSize
			);
		}

		public static DataRectangle<HistogramOfGradient> Get(DataRectangle<double> source, int blockSize)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if ((blockSize <= 0) || (blockSize > source.Width) || (blockSize > source.Height))
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			return source
				.Transform((value, point) =>
				{
					// Don't calculate any values for the edge points. We could try to approximate by saying pretend that the content that would outside the available
					// space is the same as the current value but that would introduce angles that are not present in the actual image. It also makes the code below
					// simple since we don't have to worry about under or over flowing the array bounds.
					if ((point.X == 0) || (point.Y == 0) || (point.X == (source.Width - 1)) || (point.Y == (source.Height - 1)))
						return new Vector(magnitude: 0, angle: 0);

					// Get an angle and magnitude for how the intensity changes as we move right and down. It doesn't really matter if we go right-and-down or up-and-
					// left (or if we do dark-to-light or light-to-dark) so long as we're consistent - all we need is the direction in which the intensity is changing
					// (and how quickly it's changing)
					var dx = source[point.X + 1, point.Y] - source[point.X - 1, point.Y];
					var dy = source[point.X, point.Y + 1] - source[point.X, point.Y - 1];
					return new Vector(
						magnitude: Math.Sqrt((dx * dx) + (dy * dy)),
						angle: GetAngle0To180(dx, dy)
					);
				})
				.BlockOut(blockSize, GenerateHistogram);
		}

		/// <summary>
		/// Get a value from 0 (inclusive) to 180 (exclusive)
		/// </summary>
		private static byte GetAngle0To180(double increaseInIntensityX, double increaseInIntensityY)
		{
			// Since (0, 0) is the top-left of a DataRectangle (rather than the BOTTOM-left) we need to reverse the since of the change-in-Y-frequency in order to get
			// the expected gradient angles
			var angleInDegrees = (int)Math.Round(RadianToDegree(Math.Atan2(increaseInIntensityX, -increaseInIntensityY)));
			if (angleInDegrees < 0)
				angleInDegrees += 180;
			if (angleInDegrees >= 180)
				angleInDegrees -= 180;
			return (byte)angleInDegrees;
		}

		private static double RadianToDegree(double angle)
		{
			return angle * (180d / Math.PI);
		}

		private static HistogramOfGradient GenerateHistogram(DataRectangle<Vector> gradients)
		{
			if (gradients == null)
				throw new ArgumentNullException(nameof(gradients));

			// The HoG description here is very helpful: http://mccormickml.com/2013/05/09/hog-person-detector-tutorial/ (in particular, the diagram of the histogram
			// and the statements "For each gradient vector, it’s contribution to the histogram is given by the magnitude of the vector" and "We split the contribution
			// between the two closest bins"
			var bins = new double[180 / 20];
			foreach (var gradient in gradients.Enumerate().Select(pointAndValue => pointAndValue.Item2))
			{
				int bin0, bin1;
				double fractionForBin0;
				if (gradient.Angle <= 10)
				{
					// If we're all the way over on the left hand side of the graph then we split between the first and last bins since -1 degrees is the same 179.
					// At least 50% of it will go into bin0 since we're closer to the centre of bin0 than bin{last}.
					bin0 = 0;
					bin1 = bins.Length - 1;
					var distanceIntoCurrentBin = gradient.Angle;
					fractionForBin0 = 0.5 + (0.25 * (distanceIntoCurrentBin / 20));
				}
				else if (gradient.Angle >= 170)
				{
					// If we're all the way over on the right hand side of the graph then we split between the last and first bins since 181 degrees is the same 1.
					// At least 50% of it will go into bin{last} since we're closer to the centre of bin{last} than bin{0}.
					bin0 = bins.Length - 1;
					bin1 = 0;
					var distanceIntoCurrentBin = 180 - gradient.Angle;
					fractionForBin0 = 0.5 + (0.25 * (distanceIntoCurrentBin / 20));
				}
				else
				{
					// When we're somewhere in the middle, subtracting half the bin size and then dividing by the bin size will get a fractional value that is between
					// the two bin indexes that the value should be distributed across - eg. if bin size is 20 and the angle is 105 then subtract (20/2)=10 from 105 to
					// get 95 and then divide by 20 to get 4.75, this means that bins[4] and bins[5] should be updated. If an integer value is returned then the value
					// is assigned to a single bin and not spread between two.
					bin0 = (int)Math.Floor((double)(gradient.Angle - 10) / 20);
					bin1 = (int)Math.Ceiling((double)(gradient.Angle - 10) / 20);
					var bin1Centre = (bin1 * 20) + 10;
					fractionForBin0 = (double)(bin1Centre - gradient.Angle) / 20; // The further from bin1 it is, the more than bin0 gets
				}
				bins[bin0] += (gradient.Magnitude * fractionForBin0);
				bins[bin1] += (gradient.Magnitude * (1 - fractionForBin0));
			}
			return new HistogramOfGradient(bins[0], bins[1], bins[2], bins[3], bins[4], bins[5], bins[6], bins[7], bins[8]);
		}

		private struct Vector
		{
			public Vector(double magnitude, byte angle)
			{
				Magnitude = magnitude;
				Angle = angle;
			}
			public double Magnitude { get; }
			public byte Angle { get; }
		}
	}
}
