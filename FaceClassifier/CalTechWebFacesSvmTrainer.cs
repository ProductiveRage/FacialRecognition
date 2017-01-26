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
using FaceClassifier.Normalisation;

namespace FaceClassifier
{
	public static class CalTechWebFacesSvmTrainer
	{
		public static IClassifyPotentialFaces TrainFromCaltechData(
			DirectoryInfo caltechWebFacesSourceImageFolder,
			FileInfo groundTruthTextFile,
			int sampleWidth,
			int sampleHeight,
			int blockSize,
			int minimumNumberOfImagesToTrainWith,
			Normaliser normaliser)
		{
			if (caltechWebFacesSourceImageFolder == null)
				throw new ArgumentNullException(nameof(caltechWebFacesSourceImageFolder));
			if (groundTruthTextFile == null)
				throw new ArgumentNullException(nameof(groundTruthTextFile));
			if (sampleWidth <= 0)
				throw new ArgumentOutOfRangeException(nameof(sampleWidth));
			if (sampleHeight <= 0)
				throw new ArgumentOutOfRangeException(nameof(sampleHeight));
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException(nameof(blockSize));
			if (minimumNumberOfImagesToTrainWith <= 0)
				throw new ArgumentOutOfRangeException(nameof(minimumNumberOfImagesToTrainWith));
			if (normaliser == null)
				throw new ArgumentNullException(nameof(normaliser));

			var timer = Stopwatch.StartNew();
			var trainingDataOfHogsAndIsFace = new List<Tuple<double[], bool>>();
			var numberOfImagesThatLastProgressMessageWasShownAt = 0;
			const int numberOfImagesToProcessBeforeShowingUpdateMessage = 20;
			foreach (var imagesFromSingleReferenceImage in ExtractPositiveAndNegativeTrainingDataFromCaltechWebFaces(sampleWidth, sampleHeight, groundTruthTextFile, caltechWebFacesSourceImageFolder))
			{
				// We want to train using the same number of positive images as negative images. It's possible that we were unable to extract as many non-face regions from the source
				// image as we did face regions. In this case, discount the image and move on to the next one.
				var numberOfPositiveImagesExtracted = imagesFromSingleReferenceImage.Count(imageAndIsFaceDecision => imageAndIsFaceDecision.Item2);
				var numberOfNegativeImagesExtracted = imagesFromSingleReferenceImage.Count(imageAndIsFaceDecision => !imageAndIsFaceDecision.Item2);
				if (numberOfPositiveImagesExtracted != numberOfNegativeImagesExtracted)
				{
					foreach (var image in imagesFromSingleReferenceImage.Select(imageAndIsFaceDecision => imageAndIsFaceDecision.Item1))
						image.Dispose();
					continue;
				}

				foreach (var imageAndIsFaceDecision in imagesFromSingleReferenceImage)
				{
					var image = imageAndIsFaceDecision.Item1;
					var isFace = imageAndIsFaceDecision.Item2;
					trainingDataOfHogsAndIsFace.Add(Tuple.Create(
						FeatureExtractor.GetFor(image, blockSize, optionalHogPreviewImagePath: null, normaliser: normaliser).ToArray(),
						isFace
					));
					image.Dispose();
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
			if (trainingDataOfHogsAndIsFace.Count < minimumNumberOfImagesToTrainWith)
				throw new Exception($"After loaded all data, there are only {trainingDataOfHogsAndIsFace.Count} training images but {minimumNumberOfImagesToTrainWith} were requested");
			Console.WriteLine("Time to load image data: " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
			timer.Restart();

			var kernel = new Linear();
			var complexity = 1;
			var smo = new SequentialMinimalOptimization<Linear> { Kernel = kernel, Complexity = complexity };
			var inputs = trainingDataOfHogsAndIsFace.Select(dataAndResult => dataAndResult.Item1).ToArray();
			var outputs = trainingDataOfHogsAndIsFace.Select(dataAndResult => dataAndResult.Item2).ToArray();
			var svm = smo.Learn(inputs, outputs);
			Console.WriteLine("Time to teach SVM: " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
			timer.Restart();

			svm.Compress();
			Console.WriteLine("Time to compress SVM: " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
			timer.Restart();

			var predicted = svm.Decide(inputs);
			var error = new ZeroOneLoss(outputs).Loss(predicted);
			if (error > 0)
			{
				// TODO: Log this?
			}
			Console.WriteLine("Time to test SVM against training data: " + timer.Elapsed.TotalSeconds.ToString("0.00") + "s");
			timer.Restart();

			return new SvmClassifier(svm);
		}

		private static IEnumerable<IEnumerable<Tuple<Bitmap, bool>>> ExtractPositiveAndNegativeTrainingDataFromCaltechWebFaces(
			int sampleWidth,
			int sampleHeight,
			FileInfo groundTruthTextFile,
			DirectoryInfo caltechWebFacesSourceImageFolder)
		{
			if (sampleWidth <= 0)
				throw new ArgumentOutOfRangeException(nameof(sampleWidth));
			if (sampleHeight <= 0)
				throw new ArgumentOutOfRangeException(nameof(sampleHeight));
			if (groundTruthTextFile == null)
				throw new ArgumentNullException(nameof(groundTruthTextFile));
			if (caltechWebFacesSourceImageFolder == null)
				throw new ArgumentNullException(nameof(caltechWebFacesSourceImageFolder));

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
				var positiveImagesForFile = new List<Bitmap>();
				var negativeImagesForFile = new List<Bitmap>();
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
							positiveImagesForFile.Add(source.ExtractImageSectionAndResize(estimatedFaceRegion, new Size(sampleWidth, sampleHeight)));
							faceVariationIndex++;

							// Courtesy of http://stackoverflow.com/a/263416/3813189
							int hash;
							unchecked // Overflow is fine, just wrap
							{
								hash = (int)2166136261;
								hash = hash * 16777619 + face.LeftEye.X;
								hash = hash * 16777619 + face.LeftEye.Y;
								hash = hash * 16777619 + face.RightEye.X;
								hash = hash * 16777619 + face.RightEye.Y;
								hash = hash * 16777619 + face.Nose.X;
								hash = hash * 16777619 + face.Nose.Y;
								hash = hash * 16777619 + face.Mouth.X;
								hash = hash * 16777619 + face.Mouth.Y;
								hash = hash * 16777619 + multiplier;
							}

							// Try to extract another part of the image that isn't a face, in order to build up a negative data set to train against (extract pseudo-random regions
							// but use a consistent seed so that the same results are achieved on repeated run, given the same data / images)
							var attemptsToFindNegative = 0;
							var random = new Random(Seed: hash);
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
								negativeImagesForFile.Add(source.ExtractImageSectionAndResize(negativeRegion, new Size(sampleWidth, sampleHeight)));
								negativeVariationIndex++;
								break;
							}
						}
					}
				}
				yield return
					positiveImagesForFile.Select(image => Tuple.Create(image, true))
					.Concat(
						negativeImagesForFile.Select(image => Tuple.Create(image, false))
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

		private sealed class SvmClassifier : IClassifyPotentialFaces
		{
			private readonly SupportVectorMachine<Linear> _svm;
			public SvmClassifier(SupportVectorMachine<Linear> svm)
			{
				if (svm == null)
					throw new ArgumentNullException(nameof(svm));

				_svm = svm;
			}

			public bool IsFace(double[] features)
			{
				if (features == null)
					throw new ArgumentNullException(nameof(features));

				return _svm.Decide(features);
			}
		}
	}
}