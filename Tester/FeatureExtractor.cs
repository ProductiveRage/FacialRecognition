using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Common;
using FaceClassifier;

namespace Tester
{
	public static class FeatureExtractor
	{
		public static IEnumerable<double> GetFor(Bitmap image, int blockSize, string optionalHogPreviewImagePath, Func<DataRectangle<HistogramOfGradient>, DataRectangle<HistogramOfGradient>> normaliser)
		{
			if (image == null)
				throw new ArgumentNullException(nameof(image));
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));
			if (normaliser == null)
				throw new ArgumentNullException(nameof(normaliser));

			var hogs = normaliser(HistogramOfGradientGenerator.Get(image.GetRGB().Transform(c => c.ToGreyScale()), blockSize));
			if (hogs == null)
				throw new ArgumentException("Normaliser returned null - invalid");
			if (!string.IsNullOrWhiteSpace(optionalHogPreviewImagePath))
			{
				using (var hogBitmap = hogs.GeneratePreviewImage())
				{
					hogBitmap.Save(optionalHogPreviewImagePath);
				}
			}
			return hogs.Enumerate()
				.Select(pointAndValue => pointAndValue.Item2)
				.Select(hog => new[] { hog.Degrees10, hog.Degrees30, hog.Degrees50, hog.Degrees70, hog.Degrees90, hog.Degrees110, hog.Degrees130, hog.Degrees150, hog.Degrees170 })
				.SelectMany(valueSets => valueSets);
		}
	}
}