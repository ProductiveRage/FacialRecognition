using System;
using System.Collections.Generic;
using System.Drawing;
using FaceDetection;

namespace Tester
{
	public class JayKapurOptions : IExposeConfigurationOptions
	{
		public static IExposeConfigurationOptions Instance => new JayKapurOptions();
		protected JayKapurOptions() { }
		public virtual int TextureAmplitudeFirstPassSmoothenMultiplier { get { return 8; } }
		public virtual int TextureAmplitudeSecondPassSmoothenMultiplier { get { return 12; } }
		public virtual DataRectangle<IRgBy> IRgByCalculator(DataRectangle<RGB> values)
		{
			if (values == null)
				throw new ArgumentNullException(nameof(values));

			// See http://web.archive.org/web/20090723024922/http:/geocities.com/jaykapur/face.html
			Func<byte, double> L = x => (105 * Math.Log10(x + 1));
			return values.Transform(
				value => new IRgBy(
					rg: L(value.R) - L(value.G),
					by: L(value.B) - ((L(value.G) + L(value.R)) / 2),
					i: (L(value.R) + L(value.B) + L(value.G)) / 3
				)
			);
		}
		public virtual int RgBySmoothenMultiplier { get { return 2; } }
		public virtual bool SkinFilter(HueSaturation colour)
		{
			// See http://web.archive.org/web/20090723024922/http:/geocities.com/jaykapur/face.html
			return (
					((colour.Hue >= 120) && (colour.Hue <= 160) && (colour.Saturation >= 10) && (colour.Saturation <= 60)) ||
					((colour.Hue >= 150) && (colour.Hue <= 180) && (colour.Saturation >= 20) && (colour.Saturation <= 80))
				)
				&& (colour.TextureAmplitude <= 4.5);
		}
		public virtual int NumberOfSkinMaskRelaxedExpansions { get { return 5; } }
		public virtual bool RelaxedSkinFilter(HueSaturation colour)
		{
			// See http://web.archive.org/web/20090723024922/http:/geocities.com/jaykapur/face.html
			return (colour.Hue >= 110) && (colour.Hue <= 180) && (colour.Saturation >= 0) && (colour.Saturation <= 180);
		}
		public virtual IEnumerable<Rectangle> FaceRegionAspectRatioFilter(IEnumerable<Rectangle> areas)
		{
			if (areas == null)
				throw new ArgumentNullException(nameof(areas));
			return areas;
		}
		public virtual double PercentToExpandFinalFaceRegionBy { get { return 0; } }
		public virtual Color OutlineColour { get { return Color.GreenYellow; } }
	}
}
