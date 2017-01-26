using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Common;
using FaceClassifier;
using FaceClassifier.Normalisation;
using FaceDetection;

namespace Tester
{
	class Program
	{
		static void Main(string[] args)
		{
			var caltechWebFacesSourceImageFolder = new DirectoryInfo(@"CaltechWebFaces");
			if (!caltechWebFacesSourceImageFolder.Exists)
				throw new Exception("The \"CaltechWebFaces\" folder must exist alongside the binary and be populated with the images from http://www.vision.caltech.edu/Image_Datasets/Caltech_10K_WebFaces/");
			var groundTruthTextFile = new FileInfo(Path.Combine(caltechWebFacesSourceImageFolder.FullName, "WebFaces_GroundThruth.txt"));
			if (!caltechWebFacesSourceImageFolder.Exists)
				throw new Exception("The \"WebFaces_GroundThruth.txt\" file must exist in the CaltechWebFaces folder, it may be downloaded from http://www.vision.caltech.edu/Image_Datasets/Caltech_10K_WebFaces/");

			var skinToneSearchApproachOutputFolder = new DirectoryInfo("Results");
			EmptyAndDeleteFolder(skinToneSearchApproachOutputFolder);

			const int sampleWidth = 128;
			const int sampleHeight = sampleWidth;
			const int blockSize = 8;
			Normaliser normaliser = new OverlappingBlockwiseNormaliser(blockSize: 2).Normalise;
			const int minimumNumberOfImagesToTrainWith = 2000;

			var faceClassifier = CalTechWebFacesSvmTrainer.TrainFromCaltechData(caltechWebFacesSourceImageFolder, groundTruthTextFile, sampleWidth, sampleHeight, blockSize, minimumNumberOfImagesToTrainWith, normaliser);
			var skinToneResults = new[]
				{
					"TigerWoods.gif"
				}
				.SelectMany(filePath => ExtractPossibleFaceRegionsFromImage(new FileInfo(filePath), skinToneSearchApproachOutputFolder, sizeToSaveExtractedContentAs: new Size(sampleWidth, sampleHeight)))
				.ToArray();
			var finalResults = skinToneResults
				.Select(faceRegionFile =>
				{
					using (var source = new Bitmap(faceRegionFile.FullName))
					{
						return new
						{
							File = faceRegionFile,
							IsFace = faceClassifier.IsFace(source),
						};
					}
				})
				.Select(detectedFace =>
				{
					var newFilePath = Path.Combine(detectedFace.File.Directory.FullName, (detectedFace.IsFace ? "FACE-" : "NEG-") + detectedFace.File.Name);
					detectedFace.File.CopyTo(newFilePath);
					detectedFace.File.Delete();
					return new FileInfo(newFilePath);
				})
				.ToArray();

			Console.WriteLine();
			Console.WriteLine($"Identified {skinToneResults.Length} possible face region(s) in the skin tone filter pass");
			Console.WriteLine($"{finalResults.Length} of these was determined to be a face by the SVM filter");
			Console.WriteLine("The extracted regions may be seen in the " + skinToneSearchApproachOutputFolder.Name + " folder");
			Console.WriteLine("Press [Enter] to terminate..");
			Console.WriteLine();
			Console.ReadLine();
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