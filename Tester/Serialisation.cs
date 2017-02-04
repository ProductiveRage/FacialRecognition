using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Tester
{
	public static class Serialisation
	{
		public static byte[] Serialise(object source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			using (var stream = new MemoryStream())
			{
				new BinaryFormatter().Serialize(stream, source);
				return stream.ToArray();
			}
		}

		public static T Deserialise<T>(byte[] serialisedData)
		{
			if (serialisedData == null)
				throw new ArgumentNullException(nameof(serialisedData));

			using (var stream = new MemoryStream(serialisedData))
			{
				var value = new BinaryFormatter().Deserialize(stream);
				if ((value == null) || !(value is T))
					throw new ArgumentException("Invalid content");
				return (T)value;
			}
		}
	}
}