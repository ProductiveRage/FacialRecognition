using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FaceDetection
{
	/// <summary>
	/// This ILookForPossibleFaceRegions implementation will generate possible face regions by moving a rectangle from left-to-right across the image before moving down and then all the way across
	/// again until it runs out of image. There is no filtering, it is simply returning all regions that it can generate in this manner. The largestDimensionFractionsToUseForSlidingWindowSize values
	/// will determine how big the windows are (for example, if the source image is 600x400 and the largestDimensionFractionsToUseForSlidingWindowSize values are 1/8 and 1/4 then there will be two
	/// passes made over the image, generating regions that are 75x75 and 150x150, respectively). The windowOverlapFraction specifies how far to move the window each time (for example, a value of 1/2
	/// will move the window half of its size each time.
	/// 
	/// Note: This ILookForPossibleFaceRegions implementation only exists so that I could experiment with combining a sliding window potential-face-region-detector with an SVM classifier, to see if
	/// I could drop the relatively expensive skin tone pass. I found that I got far too many false positives from the SVM stage and so I don't think that it is viable right now to remove the skin
	/// tone pass (but I'll leave this class here in case I want to revisit it). It's worth bearing in mind that, if the sliding window approach shows merit, this is an inefficient way to incorporate
	/// it into the process (it generates many regions, all of which must be extracted from the source bitmap individually and passed through the HOG feature extractor - it seems like there should be
	/// a way to reuse HOG data between many of the regions.. it may require tweaking the sliding window positions and sizes to match the block size of the HOG generator but, if that is done, then
	/// the sliding-window-plus-SVM-classifier combined process should be much faster).
	/// </summary>
	public sealed class SlidingWindowRegionGenerator : ILookForPossibleFaceRegions
	{
		public static SlidingWindowRegionGenerator DefaultConfiguration { get; } = new SlidingWindowRegionGenerator(Defaults.LargestDimensionFractionsToUseForSlidingWindowSize, Defaults.WindowOverlapFraction);

		private readonly double[] _largestDimensionFractionsToUseForSlidingWindowSize;
		private readonly double _windowOverlapFraction;
		public SlidingWindowRegionGenerator(IEnumerable<double> largestDimensionFractionsToUseForSlidingWindowSize, double windowOverlapFraction)
		{
			if (largestDimensionFractionsToUseForSlidingWindowSize == null)
				throw new ArgumentNullException(nameof(largestDimensionFractionsToUseForSlidingWindowSize));
			if ((windowOverlapFraction <= 0) || (windowOverlapFraction > 1))
				throw new ArgumentException("must be greater than zero and less than (or equal to) one", nameof(windowOverlapFraction));

			_largestDimensionFractionsToUseForSlidingWindowSize = largestDimensionFractionsToUseForSlidingWindowSize.ToArray();
			if (_largestDimensionFractionsToUseForSlidingWindowSize.Any(percentage => percentage <= 0)
			|| _largestDimensionFractionsToUseForSlidingWindowSize.Any(percentage => percentage > 1))
				throw new ArgumentException($"All values in {nameof(largestDimensionFractionsToUseForSlidingWindowSize)} must be greater than zero and less than (or equal to) one");

			_windowOverlapFraction = windowOverlapFraction;
		}

		public static class Defaults
		{
			public static IEnumerable<double> LargestDimensionFractionsToUseForSlidingWindowSize { get { return new[] { 1 / 8d, 1 / 6d, 1 / 4d, 1 / 3d }; } }
			public static double WindowOverlapFraction { get; } = 1 / 3d;
		}

		public IEnumerable<Rectangle> GetPossibleFaceRegions(Bitmap source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var largestDimension = Math.Max(source.Width, source.Height);
			var slidingWindowSizes = _largestDimensionFractionsToUseForSlidingWindowSize
				.Select(dimensionFraction => (int)Math.Round(largestDimension * dimensionFraction))
				.Where(size => size <= Math.Min(source.Width, source.Height))
				.Distinct();
			foreach (var slidingWindowSize in slidingWindowSizes)
			{
				var distanceToMoveWindowEachTime = (int)Math.Round(slidingWindowSize * _windowOverlapFraction);
				var y = 0;
				while ((y + slidingWindowSize) <= source.Height)
				{
					var x = 0;
					while ((x + slidingWindowSize) <= source.Width)
					{
						yield return new Rectangle(x, y, slidingWindowSize, slidingWindowSize);
						x += distanceToMoveWindowEachTime;
					}
					y += distanceToMoveWindowEachTime;
				}
			}
		}
	}
}
