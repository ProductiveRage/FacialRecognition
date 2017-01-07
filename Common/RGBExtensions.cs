namespace Common
{
	public static class RGBExtensions
	{
		public static double ToGreyScale(this RGB colour)
		{
			return (0.2989 * colour.R) + (0.5870 * colour.G) + (0.1140 * colour.B); // Greyscale formula
		}
	}
}
