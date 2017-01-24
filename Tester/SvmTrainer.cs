using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Math.Optimization.Losses;
using Accord.Statistics.Kernels;
using Common;
using FaceClassifier;

namespace Tester
{
	public static class SvmTrainer
	{
		public static SupportVectorMachine<Linear> TrainFromCaltechData(
			DirectoryInfo caltechWebFacesSourceImageFolder,
			FileInfo groundTruthTextFile,
			DirectoryInfo caltechFaceOutputFolder,
			int sampleWidth,
			int sampleHeight,
			int blockSize,
			int minimumNumberOfImagesToTrainWith,
			Func<DataRectangle<HistogramOfGradient>, DataRectangle<HistogramOfGradient>> normaliser)
		{
			var timer = Stopwatch.StartNew();
			var trainingDataOfHogsAndIsFace = new List<Tuple<FileInfo, double[], bool>>();
			var numberOfImagesThatLastProgressMessageWasShownAt = 0;
			const int numberOfImagesToProcessBeforeShowingUpdateMessage = 20;
			foreach (var imagesFromSingleReferenceImage in ExtractPositiveAndNegativeTrainingDataFromCaltechWebFaces(sampleWidth, sampleHeight, groundTruthTextFile, caltechWebFacesSourceImageFolder, caltechFaceOutputFolder))
			{
				var faceImages = imagesFromSingleReferenceImage.Where(entry => entry.Item2).Select(entry => entry.Item1);
				var nonFaceImages = imagesFromSingleReferenceImage.Where(entry => !entry.Item2).Select(entry => entry.Item1);
				if (faceImages.Count() != nonFaceImages.Count())
					continue;

				foreach (var entry in imagesFromSingleReferenceImage)
				{
					var imageFile = entry.Item1;
					var isFace = entry.Item2;
					using (var previewBitmap = new Bitmap(imageFile.FullName))
					{
						trainingDataOfHogsAndIsFace.Add(Tuple.Create(
							imageFile,
							FeatureExtractor.GetFor(previewBitmap, blockSize, optionalHogPreviewImagePath: imageFile.FullName + ".hog.png", normaliser: normaliser).ToArray(),
							isFace
						));
					}
				}
				var approximateNumberOfImagesProcessed = (int)Math.Floor((double)trainingDataOfHogsAndIsFace.Count / numberOfImagesToProcessBeforeShowingUpdateMessage) * numberOfImagesToProcessBeforeShowingUpdateMessage;
				if (approximateNumberOfImagesProcessed > numberOfImagesThatLastProgressMessageWasShownAt)
				{
					Console.WriteLine("Processed " + approximateNumberOfImagesProcessed + " images");
					numberOfImagesThatLastProgressMessageWasShownAt = approximateNumberOfImagesProcessed;
				}
				if (trainingDataOfHogsAndIsFace.Count >= minimumNumberOfImagesToTrainWith)
					break;
			}
			Console.WriteLine("Time to load image data: " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
			timer.Restart();

			var kernel = new Linear();
			var complexity = 1; // error 0.0076628352490421452 (exactly the same as complexity 100!)
			var smo = new SequentialMinimalOptimization<Linear> { Kernel = kernel, Complexity = complexity };
			var svm = smo.Learn(
				trainingDataOfHogsAndIsFace.Select(dataAndResult => dataAndResult.Item2).ToArray(),
				trainingDataOfHogsAndIsFace.Select(dataAndResult => dataAndResult.Item3).ToArray()
			);
			Console.WriteLine("Time to teach SVM: " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
			timer.Restart();

			svm.Compress();
			Console.WriteLine("Time to compress SVM: " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
			timer.Restart();

			var inputs = trainingDataOfHogsAndIsFace.Select(dataAndResult => dataAndResult.Item2).ToArray();
			var outputs = trainingDataOfHogsAndIsFace.Select(dataAndResult => dataAndResult.Item3).ToArray();
			var predicted = svm.Decide(inputs);
			var error = new ZeroOneLoss(outputs).Loss(predicted);

			var mistakes = trainingDataOfHogsAndIsFace
				.Zip(predicted, (trainingDataEntry, predictedResult) => new { File = trainingDataEntry.Item1, Expected = trainingDataEntry.Item3, Actual = predictedResult })
				.Where(result => result.Actual != result.Expected)
				.ToArray();
			var falsePositives = mistakes.Where(mistake => !mistake.Expected && mistake.Actual).Select(mistake => mistake.File).ToArray();
			var falseNegatives = mistakes.Where(mistake => mistake.Expected && !mistake.Actual).Select(mistake => mistake.File).ToArray();

			if (falsePositives.Any())
			{
				var falsePositivesFolder = new DirectoryInfo(Path.Combine(caltechFaceOutputFolder.FullName, "FalsePositives"));
				if (!falsePositivesFolder.Exists)
					falsePositivesFolder.Create();
				foreach (var incorrectResult in falsePositives)
					incorrectResult.CopyTo(Path.Combine(falsePositivesFolder.FullName, incorrectResult.Name));
			}
			if (falseNegatives.Any())
			{
				var falseNegativesFolder = new DirectoryInfo(Path.Combine(caltechFaceOutputFolder.FullName, "FalseNegatives"));
				if (!falseNegativesFolder.Exists)
					falseNegativesFolder.Create();
				foreach (var incorrectResult in falseNegatives)
					incorrectResult.CopyTo(Path.Combine(falseNegativesFolder.FullName, incorrectResult.Name));
			}
			return svm;
		}

		private static IEnumerable<IEnumerable<Tuple<FileInfo, bool>>> ExtractPositiveAndNegativeTrainingDataFromCaltechWebFaces(
			int sampleWidth,
			int sampleHeight,
			FileInfo groundTruthTextFile,
			DirectoryInfo caltechWebFacesSourceImageFolder,
			DirectoryInfo outputFolder)
		{
			if (sampleWidth <= 0)
				throw new ArgumentOutOfRangeException(nameof(sampleWidth));
			if (sampleHeight <= 0)
				throw new ArgumentOutOfRangeException(nameof(sampleHeight));
			if (groundTruthTextFile == null)
				throw new ArgumentNullException(nameof(groundTruthTextFile));
			if (caltechWebFacesSourceImageFolder == null)
				throw new ArgumentNullException(nameof(caltechWebFacesSourceImageFolder));
			if (outputFolder == null)
				throw new ArgumentNullException(nameof(outputFolder));

			outputFolder.Refresh();
			if (!outputFolder.Exists)
				outputFolder.Create();

			var dataForEachFilename = File.ReadLines(groundTruthTextFile.FullName)
				.Where(line => line.Trim() != "")
				.Select(line =>
				{
					var entries = line.Split(new[] {  ' ' }, StringSplitOptions.RemoveEmptyEntries);
					var values = entries.Skip(1).Select(numberString => double.Parse(numberString)).ToArray();
					if (values.Length != 8)
						throw new Exception($"Encountered line with {values.Length}, expected all to have 8");
					return new
					{
						Filename = entries.First(),
						Face = new FaceDetails(
							new Point((int)Math.Round(values[0]), (int)Math.Round(values[1])),
							new Point((int)Math.Round(values[2]), (int)Math.Round(values[3])),
							new Point((int)Math.Round(values[4]), (int)Math.Round(values[5])),
							new Point((int)Math.Round(values[6]), (int)Math.Round(values[7]))
						)
					};
				})
				.GroupBy(entry => entry.Filename, StringComparer.OrdinalIgnoreCase)
				.Select(group => new { Filename = group.Key, Faces = group.Select(entry => entry.Face).ToArray() })
				.OrderBy(file => file.Filename, StringComparer.OrdinalIgnoreCase)
				.ToArray();

			foreach (var file in dataForEachFilename)
			{
				var positiveImagesForFile = new List<FileInfo>();
				var negativeImagesForFile = new List<FileInfo>();
				using (var source = new Bitmap(Path.Combine(caltechWebFacesSourceImageFolder.FullName, file.Filename)))
				{
					var faceVariationIndex = 0;
					var negativeVariationIndex = 0;
					foreach (var face in file.Faces)
					{
						var featureBoundingBox = face.GetFeatureBoundingBox();
						var centre = new Point(
							(int)Math.Round((featureBoundingBox.Left + featureBoundingBox.Right) / 2d),
							(int)Math.Round((featureBoundingBox.Top + featureBoundingBox.Bottom) / 2d)
						);
						foreach (var multiplier in new[] { 3, 4, 5 })
						{
							// Save the face
							var estimatedFaceRegionSideLength = Math.Max(featureBoundingBox.Width, featureBoundingBox.Height) * multiplier;
							var estimatedFaceRegionSize = new Size(
								(int)Math.Round(((double)estimatedFaceRegionSideLength * sampleWidth) / sampleHeight),
								estimatedFaceRegionSideLength
							);
							var topLeftOfEstimatedFaceRegion = new Point(
								x: centre.X - (estimatedFaceRegionSize.Width / 2),
								y: centre.Y - (estimatedFaceRegionSize.Height / 2)
							);
							var estimatedFaceRegion = Rectangle.FromLTRB(
								left: Math.Max(topLeftOfEstimatedFaceRegion.X, 0),
								right: Math.Min(topLeftOfEstimatedFaceRegion.X + estimatedFaceRegionSize.Width, source.Width),
								top: Math.Max(topLeftOfEstimatedFaceRegion.Y, 0),
								bottom: Math.Min(topLeftOfEstimatedFaceRegion.Y + estimatedFaceRegionSize.Height, source.Height)
							);
							using (var extractedFace = source.ExtractImageSectionAndResize(estimatedFaceRegion, new Size(sampleWidth, sampleHeight)))
							{
								var saveAsFile = new FileInfo(Path.Combine(outputFolder.FullName, $"{file.Filename}-face{faceVariationIndex}.jpg"));
								extractedFace.Save(saveAsFile.FullName);
								positiveImagesForFile.Add(saveAsFile);
							}
							faceVariationIndex++;

							// Try to extract another part of the image that isn't a face, in order to build up a negative data set to train against
							var attemptsToFindNegative = 0;
							var random = new Random(Seed: face.GetHashCode() + multiplier);
							while (attemptsToFindNegative < 100)
							{
								var availableVariableWidth = source.Width - estimatedFaceRegionSize.Width;
								var availableVariableHeight = source.Height - estimatedFaceRegionSize.Height;
								if ((availableVariableWidth < 0) || (availableVariableHeight < 0))
									break;

								var left = (int)Math.Round(availableVariableWidth * random.NextDouble());
								var top = (int)Math.Round(availableVariableHeight * random.NextDouble());
								var negativeRegion = Rectangle.FromLTRB(
									left: left,
									right: Math.Min(left + estimatedFaceRegionSize.Width, source.Width),
									top: top,
									bottom: Math.Min(top + estimatedFaceRegionSize.Height, source.Height)
								);
								if (file.Faces.Any(faceRegion => faceRegion.GetFeatureBoundingBox().IntersectsWith(negativeRegion)))
								{
									attemptsToFindNegative++;
									continue;
								}
								using (var extractedNonFace = source.ExtractImageSectionAndResize(negativeRegion, new Size(sampleWidth, sampleHeight)))
								{
									var saveAsFile = new FileInfo(Path.Combine(outputFolder.FullName, $"{file.Filename}-neg{negativeVariationIndex}.jpg"));
									extractedNonFace.Save(saveAsFile.FullName);
									negativeImagesForFile.Add(saveAsFile);
								}
								negativeVariationIndex++;
								break;
							}
						}
					}
				}
				yield return
					positiveImagesForFile.Select(imageFile => Tuple.Create(imageFile, true))
					.Concat(
						negativeImagesForFile.Select(imageFile => Tuple.Create(imageFile, false))
					);
			}
		}

		private struct FaceDetails
		{
			public FaceDetails(Point leftEye, Point rightEye, Point nose, Point mouth)
			{
				LeftEye = leftEye;
				RightEye = rightEye;
				Nose = nose;
				Mouth = mouth;
			}
			public Point LeftEye { get; }
			public Point RightEye { get; }
			public Point Nose { get; }
			public Point Mouth { get; }
			public Rectangle GetFeatureBoundingBox()
			{
				var allPoints = new[] { LeftEye, RightEye, Nose, Mouth };
				var minX = allPoints.Min(p => p.X);
				var maxX = allPoints.Max(p => p.X);
				var minY = allPoints.Min(p => p.Y);
				var maxY = allPoints.Max(p => p.Y);
				return new Rectangle(
					new Point(minX, minY),
					new Size(maxX - minX, maxY - minY)
				);
			}
		}
	}
}