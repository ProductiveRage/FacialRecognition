using System;
using System.Linq;
using Common;
using FaceClassifier;
using UnitTests.Common;
using Xunit;

namespace UnitTests.FaceClassifier
{
	public sealed class HistogramOfGradientGeneratorTests
	{
		[Fact]
		public void HorizontalGradient()
		{
			var horizontalGradient = DataRectangle
				.For(new double[64, 60])
				.Transform((value, point) => (double)point.X);
			var hogs = HistogramOfGradientGenerator.Get(horizontalGradient, blockSize: 8);
			Assert.True(
				hogs.Enumerate().Select(pointAndValue => pointAndValue.Item2).All(hog =>
					(hog.Degrees10 == 0) &&
					(hog.Degrees30 == 0) &&
					(hog.Degrees50 == 0) &&
					(hog.Degrees70 == 0) &&
					(hog.Degrees90 > 0) &&
					(hog.Degrees110 == 0) &&
					(hog.Degrees130 == 0) &&
					(hog.Degrees150 == 0) &&
					(hog.Degrees170 == 0)
				)
			);
		}

		[Fact]
		public void VerticalGradient()
		{
			var verticalGradient = DataRectangle
				.For(new double[64, 60])
				.Transform((value, point) => (double)point.Y);
			var hogs = HistogramOfGradientGenerator.Get(verticalGradient, blockSize: 8);
			Assert.True(
				hogs.Enumerate().Select(pointAndValue => pointAndValue.Item2).All(hog =>
					(hog.Degrees10 > 0) &&
					(hog.Degrees30 == 0) &&
					(hog.Degrees50 == 0) &&
					(hog.Degrees70 == 0) &&
					(hog.Degrees90 == 0) &&
					(hog.Degrees110 == 0) &&
					(hog.Degrees130 == 0) &&
					(hog.Degrees150 == 0) &&
					(hog.Degrees170 == hog.Degrees10)
				)
			);
		}

		[Fact]
		public void FortyFiveDegreeGradient()
		{
			// If straight up is zero degrees then getting lighter as you go UP and right is 45 degrees
			var oneHundredAndThirtyFiveDegreeGradient = DataRectangle
				.For(new double[64, 60])
				.Transform((value, point) => (double)(point.X + (60 - point.Y)));

			// 45 will be split across the 30 and 50 bins with most of it going in the 50 bin (3x as much since it's 5 degrees away from 50 and it's 15
			// from 45)
			var hogs = HistogramOfGradientGenerator.Get(oneHundredAndThirtyFiveDegreeGradient, blockSize: 8);
			Assert.True(
				hogs.Enumerate().Select(pointAndValue => pointAndValue.Item2).All(hog =>
					(hog.Degrees10 == 0) &&
					(hog.Degrees30 > 0) &&
					(hog.Degrees50.HasMinimalDifference(hog.Degrees30 * 3)) &&
					(hog.Degrees70 == 0) &&
					(hog.Degrees90 == 0) &&
					(hog.Degrees110 == 0) &&
					(hog.Degrees130 == 0) &&
					(hog.Degrees150 == 0) &&
					(hog.Degrees170 == hog.Degrees10)
				)
			);
		}

		[Fact]
		public void OneHundredAndThirtyFiveDegreeGradient()
		{
			// If straight up is zero degrees then getting lighter as you go UP and right would be 45 degrees. Getting lighter as you go DOWN and right
			// is 135 degrees.
			var oneHundredAndThirtyFiveDegreeGradient = DataRectangle
				.For(new double[64, 60])
				.Transform((value, point) => (double)(point.X + point.Y));

			// 135 will be split across the 130 and 150 bins with most of it going in the 150 bin. In fact, 3x should go in the 130 bin since it the
			// angle is (135 - 130) = 5 degrees away from 135 vs (150 - 135) = 15 degrees away from 150.
			var hogs = HistogramOfGradientGenerator.Get(oneHundredAndThirtyFiveDegreeGradient, blockSize: 8);
			Assert.True(
				hogs.Enumerate().Select(pointAndValue => pointAndValue.Item2).All(hog =>
					(hog.Degrees10 == 0) &&
					(hog.Degrees30 == 0) &&
					(hog.Degrees50 == 0) &&
					(hog.Degrees70 == 0) &&
					(hog.Degrees90 == 0) &&
					(hog.Degrees110 == 0) &&
					(hog.Degrees130 > 0) &&
					(hog.Degrees150.HasMinimalDifference(hog.Degrees130 / 3)) &&
					(hog.Degrees170 == hog.Degrees10)
				)
			);
		}

		/// <summary>
		/// I was concerned that the HoG calculations were wrong when trying to visualise it so I took a section of an image where it appeared incorrect. It turned out that
		/// it was the visualisation code that was wrong but it does no harm to include the code here as an additional test.
		/// </summary>
		[Fact]
		public void SampleSegmentFromTrainingData()
		{
			var exampleSegment = DataRectangle.For(DataRectangleTests.Rotate(new[,]
			{
				{ 38.996, 38.996, 38.996, 36.996, 34.997, 29.997, 28.997, 22.998 },
				{ 39.996, 38.996, 35.996, 33.997, 29.997, 30.997, 24.998, 13.999 },
				{ 39.996, 34.997, 30.997, 29.997, 26.997, 21.998, 12.999, 10.999 },
				{ 34.997, 33.997, 26.997, 25.997, 26.997, 19.998, 10.999, 1.000 },
				{ 34.997, 27.997, 25.997, 27.997, 16.998, 6.999, 0.000, 0.000 },
				{ 31.997, 28.997, 28.997, 18.998, 7.999, 1.000, 1.000, 0.000 },
				{ 30.997, 26.997, 22.998, 14.999, 1.000, 0.000, 0.000, 1.000 },
				{ 26.997, 22.998, 15.998, 4.000, 3.000, 0.000, 0.000, 3.000 }
			}));
			var hogs = HistogramOfGradientGenerator.Get(exampleSegment, blockSize: 8);
			Assert.Equal(hogs.Width, 1);
			Assert.Equal(hogs.Height, 1);

			// I only want to test that this goes in down-right direction, the precise values aren't that important. Normalising, multiplying each value by ten and then by
			// rounding them means that small values will be ignored and the significant angles may be compared to pre-calculated values that were confirmed to match
			// expectations. This isn't the most precise test in the world, it's more of a finger-in-the-air test against some real data.
			var hog = hogs[0, 0].Normalise().Multiply(10);
			Assert.Equal(0, Math.Round(hog.Degrees10));
			Assert.Equal(0, Math.Round(hog.Degrees30));
			Assert.Equal(0, Math.Round(hog.Degrees50));
			Assert.Equal(0, Math.Round(hog.Degrees70));
			Assert.Equal(0, Math.Round(hog.Degrees90));
			Assert.Equal(2, Math.Round(hog.Degrees110));
			Assert.Equal(6, Math.Round(hog.Degrees130));
			Assert.Equal(2, Math.Round(hog.Degrees150));
			Assert.Equal(0, Math.Round(hog.Degrees170));
		}
	}
}
