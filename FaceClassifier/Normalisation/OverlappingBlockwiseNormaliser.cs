using System;
using System.Drawing;
using System.Linq;
using Common;

namespace FaceClassifier.Normalisation
{
	[Serializable]
	public sealed class OverlappingBlockwiseNormaliser
	{
		private readonly int _blockSize;
		public OverlappingBlockwiseNormaliser(int blockSize) // TODO: OverlapSize option (?)
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

			// For each overlapping block, we normalise and add the block contents to the normalisedHogs set. Blocks will be counted multiple times, which
			// is why the returned DataRectangle is larger than the input one. For example, given a 3x2 rectangle of histograms, there are data cells -
			//
			//  1 2 3
			//  4 5 6
			//
			// The normalised data will be four across because the first block to normalise will have histograms {{1,2},{4,5}} normalised and then the second block
			// will have {{2,3},{5,6}} normalised. This means that we have two blocks of 2x2 which combine to create a new output rectangle that is 4x2. The middle
			// elements (2 and 5) appear multiple times, which is expected.
			var numberOfOverlappingBlocksAcross = hogs.Width - (_blockSize - 1);
			var numberOfOverlappingBlocksDown = hogs.Height - (_blockSize - 1);
			var normalisedHogs = new HistogramOfGradient[numberOfOverlappingBlocksAcross * _blockSize, numberOfOverlappingBlocksDown * _blockSize];
			for (var x = 0; x < numberOfOverlappingBlocksAcross; x++)
			{
				for (var y = 0; y < numberOfOverlappingBlocksDown; y++)
				{
					var hogsInBlock = hogs.Slice(Rectangle.FromLTRB(
						left: x,
						right: x + _blockSize,
						top: y,
						bottom: y + _blockSize
					));
					var maxMagnitudeWithinBlock = hogsInBlock.Enumerate().Max(pointAndHog => pointAndHog.Item2.GreatestMagnitude);
					var normalisedHogsInBlock = (maxMagnitudeWithinBlock == 0) // TODO: Need tests, particularly around this sort of thing
						? hogsInBlock.Transform(hog => hog.Normalise())
						: hogsInBlock.Transform(hog => hog.Multiply(1 / maxMagnitudeWithinBlock));
					for (var i = 0; i < _blockSize; i++)
					{
						for (var j = 0; j < _blockSize; j++)
						{
							normalisedHogs[(x * _blockSize) + i, (y * _blockSize) + j] = normalisedHogsInBlock[i, j];
						}
					}
				}
			}
			return new DataRectangle<HistogramOfGradient>(normalisedHogs);
		}
	}
}
