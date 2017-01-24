using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Common;
using FaceClassifier;
using FaceDetection;

namespace Tester
{
	/*
		1. Produce HOG (Histogram og Gradients) of content in region
		   https://medium.com/@ageitgey/machine-learning-is-fun-part-4-modern-face-recognition-with-deep-learning-c3cffc121d78#.ze7thjk9y
		   http://mccormickml.com/2013/05/09/hog-person-detector-tutorial/ and http://mccormickml.com/2013/05/07/gradient-vectors/
		   http://www.learnopencv.com/histogram-of-oriented-gradients/

		2. Use (Accord.NET) SVM to process HOG values and guess whether region is a face
		   http://accord-framework.net/docs/html/T_Accord_MachineLearning_VectorMachines_SupportVectorMachine.htm

		   i. Is it possible to use the BPSimplified neural network instead of SVM?
		   ii. Does it work with a from-scratch SVM? (http://crsouza.com/2010/04/27/kernel-support-vector-machines-for-classification-and-regression-in-c/)

		Later steps are to perform "face landmark estimation", transformations to simulate "front on" face, then full recognition
		 https://medium.com/@ageitgey/machine-learning-is-fun-part-4-modern-face-recognition-with-deep-learning-c3cffc121d78#.ze7thjk9y
		 http://www.csc.kth.se/~vahidk/papers/KazemiCVPR14.pdf
	*/
	class Program
	{
		static void Main(string[] args)
		{
			const int sampleWidth = 128;
			const int sampleHeight = sampleWidth;
			const int blockSize = 8;
			Func<DataRectangle<HistogramOfGradient>, DataRectangle<HistogramOfGradient>> normaliser = GetBlockwiseNormaliseWithRepeatedOverlapBlocks(blockSize: 2);
			const int minimumNumberOfImagesToTrainWith = 2000;

			var caltechWebFacesSourceImageFolder = new DirectoryInfo(@"CaltechWebFaces");
			if (!caltechWebFacesSourceImageFolder.Exists)
				throw new Exception("The training data images from \"Caltech 10, 000 Web Faces\" (http://www.vision.caltech.edu/Image_Datasets/Caltech_10K_WebFaces/) needs to be downloaded and extracted and placed into the \"CaltechWebFaces\" in the build folder");
			var groundTruthTextFile = new FileInfo(Path.Combine(caltechWebFacesSourceImageFolder.FullName, "WebFaces_GroundThruth.txt"));
			if (!groundTruthTextFile.Exists)
				throw new Exception("The training data \"ground truth\" from \"Caltech 10, 000 Web Faces\" (http://www.vision.caltech.edu/Image_Datasets/Caltech_10K_WebFaces/) needs to be downloaded and placed into same folder as the images");
			var caltechFaceOutputFolder = new DirectoryInfo("CaltechWebFaces-Processed");
			EmptyAndDeleteFolder(caltechFaceOutputFolder);
			var svm = SvmTrainer.TrainFromCaltechData(caltechWebFacesSourceImageFolder, groundTruthTextFile, caltechFaceOutputFolder, sampleWidth, sampleHeight, blockSize, minimumNumberOfImagesToTrainWith, normaliser);

			var skinToneSearchApproachOutputFolder = new DirectoryInfo("Results");
			EmptyAndDeleteFolder(skinToneSearchApproachOutputFolder);
			var skinToneResults = new[]
				{
					"TigerWoods.gif"
				}
				.SelectMany(filePath => ExtractPossibleFaceRegionsFromImage(new FileInfo(filePath), skinToneSearchApproachOutputFolder, sizeToSaveExtractedContentAs: new Size(sampleWidth, sampleHeight)))
				.Select(faceRegionFile =>
				{
					using (var source = new Bitmap(faceRegionFile.FullName))
					{
						using (var windowedImageForFeatureExtraction = source.ExtractImageSectionAndResize(new Rectangle(new Point(0, 0), source.Size), new Size(sampleWidth, sampleHeight)))
						{
							return new
							{
								File = faceRegionFile,
								Features = FeatureExtractor.GetFor(windowedImageForFeatureExtraction, blockSize, optionalHogPreviewImagePath: null, normaliser: normaliser).ToArray()
							};
						}
					}
				})
				.Select(detectedFace => new
				{
					File = detectedFace.File,
					Features = detectedFace.Features,
					IsFace = svm.Decide(detectedFace.Features.ToArray())
				})
				.Select(detectedFace =>
				{
					var newFilePath = Path.Combine(detectedFace.File.Directory.FullName, (detectedFace.IsFace ? "FACE-" : "NEG-") + detectedFace.File.Name);
					detectedFace.File.CopyTo(newFilePath);
					detectedFace.File.Delete();
					return new
					{
						File = new FileInfo(newFilePath),
						IsFace = detectedFace.IsFace,
						Features = detectedFace.Features
					};
				})
				.ToArray();

			foreach (var result in skinToneResults.Where(result => result.IsFace))
				Console.WriteLine("Detected face: " + result.File); // TODO: Require source image and matched region
			Console.WriteLine("Complete (press [Enter] to terminate");
			Console.ReadLine();
		}

		/// <summary>
		/// This will adjust the histograms throughout the entire data, ensuring that the gradient angle bin with the largest magnitude is set to one and that all other magnitudes
		/// are adjust proportionally
		/// </summary>
		private static DataRectangle<HistogramOfGradient> GloballyNormalise(DataRectangle<HistogramOfGradient> hogs)
		{
			if (hogs == null)
				throw new ArgumentNullException(nameof(hogs));

			var maxMagnitude = hogs.Enumerate().Select(pointAndHistogram => pointAndHistogram.Item2).Max(histogram => histogram.GreatestMagnitude);
			return hogs.Transform(hog => hog.Multiply(1 / maxMagnitude));
		}

		/// <summary>
		/// This will adjust the histograms in the data, ensuring that no gradient angle bin has a value greater than one. The histograms will be adjusted based upon the largest magnitude
		/// with a block of histograms - all magnitudes will be divided by the greatest magnitude of any angle within that block, such that no value anywhere will be greater than one
		/// </summary>
		private static Func<DataRectangle<HistogramOfGradient>, DataRectangle<HistogramOfGradient>> GetBlockwiseNormaliser(int blockSize)
		{
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			return hogs =>
			{
				if (hogs == null)
					throw new ArgumentNullException(nameof(hogs));
				if ((hogs.Width < blockSize) || (hogs.Height < blockSize))
					throw new ArgumentException($"too little data ({hogs.Width}x{hogs.Height}) for specified block size ({blockSize})");

				return hogs.Transform((hog, point) =>
				{
					var x2 = Math.Min(point.X + blockSize, hogs.Width);
					var x1 = x2 - blockSize;
					var y2 = Math.Min(point.Y + blockSize, hogs.Height);
					var y1 = y2 - blockSize;
					var maxMagnitudeWithinBlock = hogs
						.Enumerate((p, h) => (p.X >= x1) && (p.X <= x2) && (p.Y >= y1) && (p.Y <= y2))
						.Max(pointAndHog => pointAndHog.Item2.GreatestMagnitude);
					return hog.Multiply(1 / maxMagnitudeWithinBlock);
				});
			};
		}

		/// <summary>
		/// This will adjust the histograms in the data, ensuring that no gradient angle bin has a value greater than one. The data is split into blocks where each block contains multiple
		/// sub-blocks (also referred to as cells) and the normalisation occurs over these blocks, rather than across the entire image. The normalisation block slides across the data and
		/// so most cells are included in multiple blocks and so contribute multiple times to the final data. Because it generates additional data, this method will require more work than
		/// a simple global normalisation but it should reduce the effects of differing lighting through an image.
		/// </summary>
		private static Func<DataRectangle<HistogramOfGradient>, DataRectangle<HistogramOfGradient>> GetBlockwiseNormaliseWithRepeatedOverlapBlocks(int blockSize) // TODO: OverlapSize option (?)
		{
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));

			return hogs =>
			{
				if (hogs == null)
					throw new ArgumentNullException(nameof(hogs));
				if ((hogs.Width < blockSize) || (hogs.Height < blockSize))
					throw new ArgumentException($"too little data ({hogs.Width}x{hogs.Height}) for specified block size ({blockSize})");

				// For each overlapping block, we normalise and add the block contents to the normalisedHogs set. Blocks will be counted multiple times, which
				// is why the returned DataRectangle is larger than the input one. For example, given a 3x2 rectangle of histograms, there are data cells -
				//
				//  1 2 3
				//  4 5 6
				//
				// The normalised data will be four across because the first block to normalise will have histograms {{1,2},{4,5}} normalised and then the second block
				// will have {{2,3},{5,6}} normalised. This means that we have two blocks of 2x2 which combine to create a new output rectangle that is 4x2. The middle
				// elements (2 and 5) appear multiple times, which is expected.
				var numberOfOverlappingBlocksAcross = hogs.Width - (blockSize - 1);
				var numberOfOverlappingBlocksDown = hogs.Height - (blockSize - 1);
				var normalisedHogs = new HistogramOfGradient[numberOfOverlappingBlocksAcross * blockSize, numberOfOverlappingBlocksDown * blockSize];
				for (var x = 0; x < numberOfOverlappingBlocksAcross; x++)
				{
					for (var y = 0; y < numberOfOverlappingBlocksDown; y++)
					{
						var hogsInBlock = hogs.Slice(Rectangle.FromLTRB(
							left: x,
							right: x + blockSize,
							top: y,
							bottom: y + blockSize
						));
						var maxMagnitudeWithinBlock = hogsInBlock.Enumerate().Max(pointAndHog => pointAndHog.Item2.GreatestMagnitude);
						var normalisedHogsInBlock = hogsInBlock.Transform(hog => hog.Multiply(1 / maxMagnitudeWithinBlock));
						for (var i = 0; i < blockSize; i++)
						{
							for (var j = 0; j < blockSize; j++)
							{
								normalisedHogs[(x * blockSize) + i, (y * blockSize) + j] = normalisedHogsInBlock[i, j];
							}
						}
					}
				}
				return new DataRectangle<HistogramOfGradient>(normalisedHogs);
			};
		}

		private static IEnumerable<FileInfo> ExtractPossibleFaceRegionsFromImage(FileInfo file, DirectoryInfo saveRegionsTo, Size sizeToSaveExtractedContentAs)
		{
			if (file == null)
				throw new ArgumentNullException(nameof(file));
			if (saveRegionsTo == null)
				throw new ArgumentNullException(nameof(saveRegionsTo));
			if ((sizeToSaveExtractedContentAs.Width <= 0) || (sizeToSaveExtractedContentAs.Height <= 0))
				throw new ArgumentOutOfRangeException(nameof(sizeToSaveExtractedContentAs));

			var outputFilename = "Output.png";

			// 600 strikes a reasonable balance between successfully matching faces in my current (small) sample data while performing the work quickly
			const int maxAllowedSize = 600;

			var config = DefaultConfiguration.Instance;
			var timer = new IntervalTimer(Console.WriteLine);
			var faceDetector = new FaceDetector(config, timer.Log);
			using (var source = new Bitmap(file.FullName))
			{
				var largestDimension = Math.Max(source.Width, source.Height);
				var scaleDown = (largestDimension > maxAllowedSize) ? ((double)largestDimension / maxAllowedSize) : 1;
				var colourData = (scaleDown > 1) ? GetResizedBitmapData(source, scaleDown) : source.GetRGB();

				var faceRegions = faceDetector.GetPossibleFaceRegions(colourData);
				if (scaleDown > 1)
					faceRegions = faceRegions.Select(region => Scale(region, scaleDown, source.Size));
				faceRegions = faceRegions.Select(region =>
				{
					region = ToDesiredAspectRatio(region, sizeToSaveExtractedContentAs.Width / sizeToSaveExtractedContentAs.Height);
					region.Intersect(new Rectangle(new Point(0, 0), source.Size));
					return region;
				});
				WriteOutputFile(outputFilename, source, faceRegions, Color.GreenYellow);
				timer.Log($"Complete (written to {outputFilename}), {faceRegions.Count()} region(s) identified");

				foreach (var indexedFaceRegion in faceRegions.Select((faceRegion, index) => new { Index = index, FaceRegion = faceRegion }))
				{
					using (var resizedAndCentredFaceContent = source.ExtractImageSectionAndResize(indexedFaceRegion.FaceRegion, sizeToSaveExtractedContentAs))
					{
						var fileToSaveInto = new FileInfo(Path.Combine(saveRegionsTo.FullName, $"{file.Name}-face{indexedFaceRegion.Index}.png"));
						if (!fileToSaveInto.Directory.Exists)
							fileToSaveInto.Directory.Create();
						resizedAndCentredFaceContent.Save(fileToSaveInto.FullName);
						yield return fileToSaveInto;
					}
				}
			}
		}

		private static Rectangle ToDesiredAspectRatio(Rectangle region, double aspectRatio)
		{
			if (aspectRatio <= 0)
				throw new ArgumentOutOfRangeException(nameof(aspectRatio));

			var regionAspectRatio = (double)region.Width / region.Height;
			if (regionAspectRatio > aspectRatio)
			{
				var requiredHeight = region.Width / aspectRatio;
				var additionalHeightRequiredEitherSide = (int)Math.Round((requiredHeight - region.Height) / 2);
				region.Inflate(width: 0, height: additionalHeightRequiredEitherSide);
			}
			else if (regionAspectRatio < aspectRatio)
			{
				var requiredWidth = region.Height * aspectRatio;
				var additionalWidthRequiredEitherSide = (int)Math.Round((requiredWidth - region.Width) / 2);
				region.Inflate(width: additionalWidthRequiredEitherSide, height: 0);
			}
			return region;
		}

		private static DataRectangle<RGB> GetResizedBitmapData(Bitmap source, double scale)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (scale <= 0)
				throw new ArgumentOutOfRangeException(nameof(scale));

			using (var resizedSource = GetResizedBitmap(source, scale))
			{
				return resizedSource.GetRGB();
			}
		}

		private static Bitmap GetResizedBitmap(Bitmap source, double divideDimensionsBy)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (divideDimensionsBy <= 0)
				throw new ArgumentOutOfRangeException(nameof(divideDimensionsBy));

			return new Bitmap(source, new Size((int)Math.Round(source.Width / divideDimensionsBy), (int)Math.Round(source.Height / divideDimensionsBy)));
		}

		private static Rectangle Scale(Rectangle region, double scale, Size limits)
		{
			if (scale <= 0)
				throw new ArgumentOutOfRangeException(nameof(scale));
			if ((limits.Width <= 0) || (limits.Height <= 0))
				throw new ArgumentOutOfRangeException(nameof(limits));

			// Need to ensure that we don't exceed the limits of the original image when scaling regions back up (there could be rounding errors that result in invalid
			// regions when scaling up that we need to be careful of)
			var left = (int)Math.Round(region.X * scale);
			var top = (int)Math.Round(region.Y * scale);
			var width = (int)Math.Round(region.Width * scale);
			var height = (int)Math.Round(region.Height * scale);
			return Rectangle.FromLTRB(
				left: left,
				top: top,
				right: Math.Min(left + width, limits.Width),
				bottom: Math.Min(top + height, limits.Height)
			);
		}

		private static void WriteOutputFile(string outputFilename, Bitmap source, IEnumerable<Rectangle> faceRegions, Color outline)
		{
			if (string.IsNullOrWhiteSpace(outputFilename))
				throw new ArgumentException($"Null/blank {nameof(outputFilename)} specified");
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (faceRegions == null)
				throw new ArgumentNullException(nameof(faceRegions));

			// If the original image uses a palette (ie. an indexed PixelFormat) then GDI+ can't draw rectangle on it so we'll just create a fresh bitmap every time to
			// be on the safe side
			using (var annotatedBitMap = new Bitmap(source.Width, source.Height))
			{
				using (var g = Graphics.FromImage(annotatedBitMap))
				{
					g.DrawImage(source, 0, 0);
					if (faceRegions.Any())
					{
						using (var pen = new Pen(outline, width: 1))
						{
							g.DrawRectangles(pen, faceRegions.ToArray());
						}
					}
				}
				annotatedBitMap.Save(outputFilename);
			}
		}

		private static void EmptyAndDeleteFolder(DirectoryInfo folder)
		{
			while (folder.Exists)
			{
				try
				{
					folder.Delete(recursive: true);
				}
				catch { }
				folder.Refresh();
				System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));
			}
		}
	}
}