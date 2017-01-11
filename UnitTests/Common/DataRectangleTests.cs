using System;
using System.Drawing;
using System.Linq;
using Common;
using Xunit;

namespace UnitTests.Common
{
	public sealed class DataRectangleTests
	{
		[Fact]
		public void Enumerate3x4WithoutFilter()
		{
			// Don't care about ordering here, only what items are returned - so the result will be ordered with LINQ and the expected array
			// will be defined in ascending order
			var data = DataRectangle.For(new[,]
			{
				{ 0, 1, 2 },
				{ 3, 4, 5 },
				{ 6, 7, 8 }
			});
			var result = data.Enumerate().Select(pointAndValue => pointAndValue.Item2).OrderBy(value => value);
			var expected = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
			Assert.Equal(expected, result);
		}

		[Fact]
		public void Enumerate3x4WithFilter()
		{
			// Don't care about ordering here, only what items are returned - so the result will be ordered with LINQ and the expected array
			// will be defined in ascending order
			var data = DataRectangle.For(new[,]
			{
				{ 0, 1, 2 },
				{ 3, 4, 5 },
				{ 6, 7, 8 }
			});
			var result = data.Enumerate((point, value) => value % 2 == 0).Select(pointAndValue => pointAndValue.Item2).OrderBy(value => value);
			var expected = new[] { 0, 2, 4, 6, 8 };
			Assert.Equal(expected, result);
		}

		[Fact]
		public void Transform3x3ByAddingOne()
		{
			var data = DataRectangle.For(new[,]
			{
				{ 0, 1, 2 },
				{ 3, 4, 5 },
				{ 6, 7, 8 }
			});
			var expected = DataRectangle.For(new[,]
			{
				{ 1, 2, 3 },
				{ 4, 5, 6 },
				{ 7, 8, 9 }
			});
			Assert.Equal(expected, data.Transform(value => value + 1), DataRectangleTestsEqualityComparer<int>.Default);
		}

		[Fact]
		public void CombineTwo2x2WithAddition()
		{
			var left = DataRectangle.For(new[,]
			{
				{ 1, 2 },
				{ 3, 4 }
			});
			var right = DataRectangle.For(new[,]
			{
				{ 2, 3 },
				{ 4, 5 }
			});
			var expected = DataRectangle.For(new[,]
			{
				{ 3, 5 },
				{ 7, 9 }
			});
			Assert.Equal(expected, left.CombineWith(right, (x, y) => x + y), DataRectangleTestsEqualityComparer<int>.Default);
		}

		[Fact]
		public void MayNotCombineDifferentSizes()
		{
			var left = DataRectangle.For(new[,]
			{
				{ 1, 2 },
				{ 3, 4 }
			});
			var right = DataRectangle.For(new[,]
			{
				{ 1, 2, 3 },
				{ 4, 5, 6 },
				{ 7, 8, 9 }
			});
			var expected = DataRectangle.For(new[,]
			{
				{ 3, 5 },
				{ 7, 9 }
			});
			Assert.Throws<ArgumentException>(() => left.CombineWith(right, (x, y) => x + y));
		}

		[Fact]
		public void Slice4x3Into2x1With1x1Offset()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{ 1,  2,  3,  4 },
				{ 5,  6,  7,  8 },
				{ 9, 10, 11, 12 }
			}));
			var expected = DataRectangle.For(Rotate(new[,]
			{
				{ 6, 7 }
			}));
			var result = data.Slice(new Rectangle(new Point(1, 1), new Size(2, 1)));
			Assert.Equal(expected, result, DataRectangleTestsEqualityComparer<int>.Default);
		}

		[Fact]
		public void Slice6x5Into3x3WithOffset1x1ThenInto2x1With1x1Offset()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{  1,  2,  3,  4,  5,  6 },
				{  7,  8,  9, 10, 11, 12 },
				{ 13, 14, 15, 16, 17, 18 },
				{ 19, 20, 21, 22, 23, 24 },
				{ 25, 26, 27, 28, 29, 30 }
			}));
			var expected = DataRectangle.For(Rotate(new[,]
			{
				{ 15, 16 }
			}));
			var result = data
				.Slice(new Rectangle(new Point(1, 1), new Size(3, 3)))
				.Slice(new Rectangle(new Point(1, 1), new Size(2, 1)));
			Assert.Equal(expected, result, DataRectangleTestsEqualityComparer<int>.Default);
		}

		[Fact]
		public void MayNotSliceNegativelyEvenWithNestedSlices()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{ 1,  2,  3,  4 },
				{ 5,  6,  7,  8 },
				{ 9, 10, 11, 12 }
			}));
			Assert.Throws<ArgumentOutOfRangeException>(() =>
			{
				data
					.Slice(new Rectangle(new Point(1, 1), new Size(3, 3)))
					.Slice(new Rectangle(new Point(-1, 1), new Size(2, 1)));
			});
		}

		[Fact]
		public void Block4x4Into2x2()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{ 1, 1, 2, 2 },
				{ 1, 1, 2, 2 },
				{ 3, 3, 4, 4 },
				{ 3, 3, 4, 4 }
			}));
			var result = data.BlockOut(
				blockSize: 2,
				reducer: blockData =>
				{
					var valuesInBlock = blockData.Enumerate().Select(pointAndValue => pointAndValue.Item2);
					if (valuesInBlock.Distinct().Count() > 1)
						throw new Exception("All of the values in each block should be consistent based upon the test data");
					return valuesInBlock.First();
				}
			);
			var expected = DataRectangle.For(Rotate(new[,]
			{
				{ 1, 2 },
				{ 3, 4 }
			}));
			Assert.Equal(expected, result, DataRectangleTestsEqualityComparer<int>.Default);
		}

		[Fact]
		public void Block4x4Into4x4()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{ 1, 1, 2, 2 },
				{ 1, 1, 2, 2 },
				{ 3, 3, 4, 4 },
				{ 3, 3, 4, 4 }
			}));
			var result = data.BlockOut(
				blockSize: 1,
				reducer: blockData =>
				{
					var valuesInBlock = blockData.Enumerate().Select(pointAndValue => pointAndValue.Item2);
					if (valuesInBlock.Count() != 1)
						throw new Exception("Expect precisely one value per block");
					return valuesInBlock.First();
				}
			);
			var expected = data;
			Assert.Equal(expected, result, DataRectangleTestsEqualityComparer<int>.Default);
		}

		/// <summary>
		/// Splitting 3x3 into 2x2 won't go perfectly, there should be one clear 2x2 block and then three 1x1 blocks around it
		/// </summary>
		[Fact]
		public void Block3x3Into2x2()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{ 1, 1, 2 },
				{ 1, 1, 2 },
				{ 3, 3, 4 }
			}));
			var result = data.BlockOut(
				blockSize: 2,
				reducer: blockData =>
				{
					var valuesInBlock = blockData.Enumerate().Select(pointAndValue => pointAndValue.Item2);
					if (valuesInBlock.Distinct().Count() > 1)
						throw new Exception("All of the values in each block should be consistent based upon the test data");
					return valuesInBlock.First();
				}
			);
			var expected = DataRectangle.For(Rotate(new[,]
			{
				{ 1, 2 },
				{ 3, 4 }
			}));
			Assert.Equal(expected, result, DataRectangleTestsEqualityComparer<int>.Default);
		}

		/// <summary>
		/// Splitting 5x5 into 4x4 won't go perfectly, there should be one clear 4x4 block and then the rest of the content is too small to block up
		/// </summary>
		[Fact]
		public void Block5x5Into4x4()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{ 1, 1, 1, 1, 2 },
				{ 1, 1, 1, 1, 2 },
				{ 1, 1, 1, 1, 2 },
				{ 1, 1, 1, 1, 2 },
				{ 3, 3, 3, 3, 4 }
			}));
			var result = data.BlockOut(
				blockSize: 4,
				reducer: blockData =>
				{
					var valuesInBlock = blockData.Enumerate().Select(pointAndValue => pointAndValue.Item2);
					if (valuesInBlock.Distinct().Count() > 1)
						throw new Exception("All of the values in each block should be consistent based upon the test data");
					return valuesInBlock.First();
				}
			);
			var expected = DataRectangle.For(Rotate(new[,]
			{
				{ 1 }
			}));
			Assert.Equal(expected, result, DataRectangleTestsEqualityComparer<int>.Default);
		}

		[Fact]
		public void AnyValuesMatchFor2x2AreaWithin6x4()
		{
			var data = DataRectangle.For(Rotate(new[,]
			{
				{  1,  2,  3,  4,  5,  6 },
				{  7,  8,  9, 10, 11, 12 },
				{ 13, 14, 15, 16, 17, 18 },
				{ 19, 20, 21, 22, 23, 24 },
				{ 25, 26, 27, 28, 29, 30 }
			}));
			var area = new Rectangle(new Point(1, 1), new Size(2, 1));
			Assert.True(
				data.AnyValuesMatch(area, value => value == 8)
			);
			Assert.True(
				data.AnyValuesMatch(area, value => value == 9)
			);
			Assert.False(
				data.AnyValuesMatch(area, value => value < 8)
			);
			Assert.False(
				data.AnyValuesMatch(area, value => value > 9)
			);
		}

		/// <summary>
		/// When declaring a 2D array using the inline initialisation syntax, the x and y values are (to my mind) reversed from how they appear in the code - if a 4x3
		/// array is defined then there will be three lines with four elements in, which would seem sensible to record as a 4x3 array (where the x value is the width
		/// of each line). However, in C#, this will actually create a 3x4 array which doesn't visually correspond to the code. This will rotate the array so that
		/// it the array is changed to the form that I think makes more sense (the Slice6x5Into3x3WithOffset1x1ThenInto2x1With1x1Offset test method is a good
		/// demonstration of this in practice).
		/// </summary>
		public static T[,] Rotate<T>(T[,] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));
			if ((data.GetLowerBound(0) != 0) || (data.GetLowerBound(1) != 0))
				throw new ArgumentException("Arrays must have zero-based indexes");

			var width = data.GetUpperBound(0) + 1;
			var height = data.GetUpperBound(1) + 1;
			var rotated = new T[height, width];
			for (var i = 0; i < width; i++)
			{
				for (var j = 0; j < height; j++)
					rotated[j, i] = data[i, j];
			}
			return rotated;
		}
	}
}
