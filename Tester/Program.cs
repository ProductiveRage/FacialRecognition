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

			var faceDetectionResultsFolder = new DirectoryInfo("Results");
			EmptyAndDeleteFolder(faceDetectionResultsFolder);
			faceDetectionResultsFolder.Create();

			const int sampleWidth = 128;
			const int sampleHeight = sampleWidth;
			const int blockSize = 8;
			Normaliser normaliser = new OverlappingBlockwiseNormaliser(blockSize: 2).Normalise;
			const int minimumNumberOfImagesToTrainWith = 2000;

			var timer = new IntervalTimer(Console.WriteLine);
			var faceClassifier = CalTechWebFacesSvmTrainer.TrainFromCaltechData(caltechWebFacesSourceImageFolder, groundTruthTextFile, sampleWidth, sampleHeight, blockSize, minimumNumberOfImagesToTrainWith, normaliser, timer.Log);
			var possibleFaceRegionsInImages = new[]
				{
					"TigerWoods.gif"
				}
				.Select(filePath => new FileInfo(filePath))
				.Select(file => new
				{
					File = file,
					PossibleFaceImages = ExtractPossibleFaceRegionsFromImage(file, timer.Log)
				})
				.Select(fileAndPossibleFaceRegions => new
				{
					File = fileAndPossibleFaceRegions.File,
					PossibleFaceImages = fileAndPossibleFaceRegions.PossibleFaceImages
						.Select(possibleFaceImage => new
						{
							Image = possibleFaceImage.ExtractedImage,
							RegionInSource = possibleFaceImage.RegionInSource,
							IsFace = faceClassifier.IsFace(possibleFaceImage.ExtractedImage)
						})
				})
				.Select((fileAndPossibleFaceRegions, index) =>
				{
					// This will save the positive and negative matches into individual files in faceDetectionResultsFolder
					var possibleFaceRegions = fileAndPossibleFaceRegions.PossibleFaceImages.ToArray(); // Prevent repeated evaluation below
					var faceIndex = 0;
					var negativeIndex = 0;
					foreach (var possibleFaceRegion in possibleFaceRegions)
					{
						string filename;
						if (possibleFaceRegion.IsFace)
						{
							filename = "FACE_" + index + "_" + faceIndex;
							faceIndex++;
						}
						else
						{
							filename = "NEG_" + index + "_" + negativeIndex;
							negativeIndex++;
						}
						filename += "-" + fileAndPossibleFaceRegions.File.Name + ".png";
						possibleFaceRegion.Image.Save(Path.Combine(faceDetectionResultsFolder.FullName, filename));
						possibleFaceRegion.Image.Dispose();
					}
					return new
					{
						File = fileAndPossibleFaceRegions.File,
						PossibleFaceImages = fileAndPossibleFaceRegions.PossibleFaceImages
							.Select(possibleFaceImage => new
							{
								RegionInSource = possibleFaceImage.RegionInSource,
								IsFace = possibleFaceImage.IsFace
							})

					};
				})
				.Select(fileAndPossibleFaceRegions =>
				{
					// This will save a copy of the original image to the faceDetectionResultsFolder with the detected faces outlined
					using (var source = new Bitmap(fileAndPossibleFaceRegions.File.FullName))
					{
						WriteOutputFile(
							Path.Combine(faceDetectionResultsFolder.FullName, fileAndPossibleFaceRegions.File.Name) + ".png",
							source,
							fileAndPossibleFaceRegions.PossibleFaceImages.Where(possibleFaceRegion => possibleFaceRegion.IsFace).Select(possibleFaceRegion => possibleFaceRegion.RegionInSource),
							Color.GreenYellow
						);
					}
					return fileAndPossibleFaceRegions;
				})
				.ToArray(); // Evaluate the above work

			Console.WriteLine();
			Console.WriteLine($"Identified {possibleFaceRegionsInImages.Sum(file => file.PossibleFaceImages.Count())} possible face region(s) in the skin tone filter pass");
			Console.WriteLine($"{possibleFaceRegionsInImages.Sum(file => file.PossibleFaceImages.Count(possibleFace => possibleFace.IsFace))} of these was determined to be a face by the SVM filter");
			Console.WriteLine("The extracted regions may be seen in the " + faceDetectionResultsFolder.Name + " folder");
			Console.WriteLine();
			Console.WriteLine("Press [Enter] to terminate..");
			Console.ReadLine();
		}

		private static IEnumerable<PossibleFaceRegion> ExtractPossibleFaceRegionsFromImage(FileInfo file, Action<string> logger)
		{
			if (file == null)
				throw new ArgumentNullException(nameof(file));
			if (logger == null)
				throw new ArgumentNullException(nameof(logger));

			// 600 strikes a reasonable balance between successfully matching faces in my current (small) sample data while performing the work quickly
			const int maxAllowedSize = 600;

			var config = DefaultConfiguration.Instance;
			var faceDetector = new FaceDetector(config, logger);
			using (var source = new Bitmap(file.FullName))
			{
				var largestDimension = Math.Max(source.Width, source.Height);
				var scaleDown = (largestDimension > maxAllowedSize) ? ((double)largestDimension / maxAllowedSize) : 1;
				var colourData = (scaleDown > 1) ? GetResizedBitmapData(source, scaleDown) : source.GetRGB();
				var faceRegions = faceDetector.GetPossibleFaceRegions(colourData);
				if (scaleDown > 1)
					faceRegions = faceRegions.Select(region => Scale(region, scaleDown, source.Size));
				logger($"Complete - {faceRegions.Count()} region(s) identified");
				foreach (var faceRegion in faceRegions)
					yield return new PossibleFaceRegion(source.Clone(faceRegion, source.PixelFormat), faceRegion);
			}
		}

		private sealed class PossibleFaceRegion
		{
			public PossibleFaceRegion(Bitmap extractedImage, Rectangle regionInSource)
			{
				if (extractedImage == null)
					throw new ArgumentNullException(nameof(extractedImage));
				if ((regionInSource.Width != extractedImage.Width) || (regionInSource.Height != extractedImage.Height))
					throw new ArgumentException($"Specified {nameof(regionInSource)} dimensions do not match {nameof(extractedImage)} image");
				ExtractedImage = extractedImage;
				RegionInSource = regionInSource;
			}
			public Bitmap ExtractedImage { get; }
			public Rectangle RegionInSource { get; }
		}

		private static DataRectangle<RGB> GetResizedBitmapData(Bitmap source, double divideDimensionsBy)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (divideDimensionsBy <= 0)
				throw new ArgumentOutOfRangeException(nameof(divideDimensionsBy));

			var resizeTo = new Size((int)Math.Round(source.Width / divideDimensionsBy), (int)Math.Round(source.Height / divideDimensionsBy));
			using (var resizedSource = new Bitmap(source, resizeTo))
			{
				return resizedSource.GetRGB();
			}
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