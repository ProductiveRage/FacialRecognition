using System;
using System.Linq;
using Common;

namespace FaceClassifier.Normalisation
{
	[Serializable]
	public sealed class BlockwiseNormaliser
	{
		private readonly int _blockSize;
		public BlockwiseNormaliser(int blockSize)
		{
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			_blockSize = blockSize;
		}

		/// <summary>
		/// This will adjust the histograms in the data, ensuring that no gradient angle bin has a value greater than one. The histograms will be adjusted based upon the largest magnitude
		/// with a block of histograms - all magnitudes will be divided by the greatest magnitude of any angle within that block, such that no value anywhere will be greater than one
		/// </summary>
		public DataRectangle<HistogramOfGradient> Normalise(DataRectangle<HistogramOfGradient> hogs)
		{
			if (hogs == null)
				throw new ArgumentNullException(nameof(hogs));

			if ((hogs.Width < _blockSize) || (hogs.Height < _blockSize))
				throw new ArgumentException($"too little data ({hogs.Width}x{hogs.Height}) for specified block size ({_blockSize})");

			return hogs.Transform((hog, point) =>
			{
				var x2 = Math.Min(point.X + _blockSize, hogs.Width);
				var x1 = x2 - _blockSize;
				var y2 = Math.Min(point.Y + _blockSize, hogs.Height);
				var y1 = y2 - _blockSize;
				var maxMagnitudeWithinBlock = hogs
					.Enumerate((p, h) => (p.X >= x1) && (p.X <= x2) && (p.Y >= y1) && (p.Y <= y2))
					.Max(pointAndHog => pointAndHog.Item2.GreatestMagnitude);
				return (maxMagnitudeWithinBlock == 0) // TODO: Need tests, particularly around this sort of thing
					? hog.Normalise()
					: hog.Multiply(1 / maxMagnitudeWithinBlock);
			});
		}
	}
}
