using System.Linq;
using Common;
using FaceClassifier;
using Xunit;

namespace UnitTests.FaceClassifier
{
	public sealed class HistogramOfGradientGeneratorTests
	{
		[Fact]
		public void HorizontalGradient()
		{
			var horizontalGradient = DataRectangle
				.For(new byte[64, 60])
				.Transform((value, point) => (byte)point.X);
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
				.For(new byte[64, 60])
				.Transform((value, point) => (byte)point.Y);
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
				.For(new byte[64, 60])
				.Transform((value, point) => (byte)(point.X + (60 - point.Y)));

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
				.For(new byte[64, 60])
				.Transform((value, point) => (byte)(point.X + point.Y));

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
	}
}
