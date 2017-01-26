using System;
using System.Linq;
using Common;

namespace FaceClassifier.Normalisation
{
	public static class GlobalNormaliser
	{
		/// <summary>
		/// This will adjust the histograms throughout the entire data, ensuring that the gradient angle bin with the largest magnitude is set to one and that all other magnitudes
		/// are adjust proportionally
		/// </summary>
		public static DataRectangle<HistogramOfGradient> Normalise(DataRectangle<HistogramOfGradient> hogs)
		{
			if (hogs == null)
				throw new ArgumentNullException(nameof(hogs));

			var maxMagnitude = hogs.Enumerate().Select(pointAndHistogram => pointAndHistogram.Item2).Max(histogram => histogram.GreatestMagnitude);
			return (maxMagnitude == 0) // TODO: Need tests, particularly around this sort of thing
				? hogs.Transform(hog => hog.Normalise())
				: hogs.Transform(hog => hog.Multiply(1 / maxMagnitude));
		}
	}
}
