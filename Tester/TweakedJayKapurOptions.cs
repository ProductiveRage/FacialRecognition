using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using FaceDetection;

namespace Tester
{
	public class TweakedJayKapurOptions : JayKapurOptions
	{
		public new static IExposeConfigurationOptions Instance => new TweakedJayKapurOptions();
		protected TweakedJayKapurOptions() { }
		public override bool SkinFilter(HueSaturation colour)
		{
			return (
					((colour.Hue >= 105) && (colour.Hue <= 120) && (colour.Saturation >= 10) && (colour.Saturation <= 60)) || // Reduced minimum hue slightly to allow some lighter tones
					((colour.Hue >= 120) && (colour.Hue <= 160) && (colour.Saturation >= 10) && (colour.Saturation <= 60)) ||
					((colour.Hue >= 160) && (colour.Hue <= 180) && (colour.Saturation >= 30) && (colour.Saturation <= 40)) // Reduced acceptable saturation so that strong yellow tones aren't as readibly recognised
				)
				&& (colour.TextureAmplitude <= 9); // Some photos seem to need to accept a higher text amplitude, particularly if the face is a relatively small part of the image
		}
		public override IEnumerable<Rectangle> FaceRegionAspectRatioFilter(IEnumerable<Rectangle> areas)
		{
			if (areas == null)
				throw new ArgumentNullException(nameof(areas));

			// Only accept areas that seem like they are be a sensible aspect ratio
			var allowedAreas = new List<Rectangle>();
			foreach (var area in areas)
			{
				if ((area.Width <= 0) || (area.Height <= 0))
					throw new ArgumentException($"Encounted invalid {nameof(areas)} value (both dimensions must be positive)");
				var longestSideMultiple = (double)Math.Max(area.Width, area.Height) / Math.Min(area.Width, area.Height);
				if (longestSideMultiple > 2.4)
					continue;
				allowedAreas.Add(area);
			}

			// If there are any regions that overlap a lot then look for any obvious regions that may be removed (for example, sometimes there will be a good match over
			// most of a face but then a separate match that overlaps a lot - or entirely - but that is much smaller; in this case, the smaller region may be removed)
			foreach (var area in allowedAreas)
			{
				var areaOfThisArea = GetArea(area);
				var areasThatMakesThisOneObsolete = allowedAreas
					.Where(other => GetArea(other) > (areaOfThisArea * 2))
					.Where(other => { other.Intersect(area); return GetArea(other) > (0.75 * areaOfThisArea); });
				if (!areasThatMakesThisOneObsolete.Any())
					yield return area;
			}
		}
		public override double PercentToExpandFinalFaceRegionBy { get { return 0.1; } }

		private static double GetArea(Rectangle area)
		{
			return area.Width * area.Height;
		}
	}
}
