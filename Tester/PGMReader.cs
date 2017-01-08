using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Common;

namespace Tester
{
	public static class PGMReader
	{
		public static DataRectangle<byte> ReadPlainPGM(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentException($"Null/blank {nameof(path)} specified");

			using (var stream = File.OpenRead(path))
			{
				using (var reader = new BinaryReader(stream))
				{
					return ReadPlainPGM(reader);
				}
			}
		}

		public static DataRectangle<byte> ReadPlainPGM(BinaryReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException(nameof(reader));

			var header = TryToReadToEndOfLine(reader);
			if (header != "P2")
				throw new ArgumentException("Magic number missing (only support 'P2' plain PGM files) - invalid content");

			Size? dimensions = null;
			int? range = null;
			while (true)
			{
				var line = TryToReadToEndOfLine(reader);
				if (line.StartsWith("#"))
					continue;
				if (dimensions == null)
				{
					try
					{
						dimensions = ParseDimensions(line);
					}
					catch (Exception e)
					{
						throw new ArgumentException("Invalid dimensions value: " + e.Message, e);
					}
					continue;
				}
				try
				{
					range = int.Parse(line);
				}
				catch
				{
					throw new ArgumentException("Invalid dynamic range value, not an integer");
				}
				if ((range < 1) || (range > 255))
					throw new ArgumentException("Invalid dynamic range value, only support values 1-255 (inclusive)");
				break;
			}

			var pixels = new List<byte>();
			while (true)
			{
				var line = TryToReadToEndOfLine(reader);
				if (line == null)
					break;
				foreach (var segment in line.Split())
				{
					byte value;
					if (!byte.TryParse(segment, out value))
						throw new ArgumentException($"Invalid image content, encountered non-byte segment ({segment})");
					pixels.Add(value);
				}
			}
			var width = dimensions.Value.Width;
			var height = dimensions.Value.Height;
			if (pixels.Count != (width * height))
				throw new ArgumentException($"Invalid content, read {pixels.Count} pixels instead of the expected {width * height}");

			var pixelArray = new byte[width, height];
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
					pixelArray[x, y] = pixels[(y * width) + x];
			}
			return DataRectangle.For(pixelArray);
		}

		private static Size ParseDimensions(string line)
		{
			if (string.IsNullOrWhiteSpace(line))
				throw new ArgumentException($"Null/blank {nameof(line)} specified");

			var segments = line.Split();
			if (segments.Length != 2)
				throw new ArgumentException($"Invalid input - should be precisely two segments (found {segments.Length})");

			int width;
			if (!int.TryParse(segments[0], out width) || (width <= 0))
				throw new ArgumentException($"Invalid input - the two segments should represent two positive integers ('{segments[0]}')");
			int height;
			if (!int.TryParse(segments[1], out height) || (height <= 0))
				throw new ArgumentException($"Invalid input - the two segments should represent two positive integers ('{segments[1]}')");
			return new Size(width, height);
		}

		private static string TryToReadToEndOfLine(BinaryReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException(nameof(reader));

			var content = new StringBuilder();
			var gotContent = false;
			while (reader.PeekChar() != -1)
			{
				var character = reader.ReadChar();
				if (character == '\n')
					break;
				if (character == '\r')
				{
					if (reader.PeekChar() == '\n')
						reader.ReadChar();
					break;
				}
				content.Append(character);
				gotContent = true;
			}
			return gotContent ? content.ToString() : null;
		}
	}
}
