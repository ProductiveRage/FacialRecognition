using System.Collections.Generic;
using System.Drawing;

namespace FaceDetection
{
	public interface IExposeConfigurationOptions
	{
		int TextureAmplitudeFirstPassSmoothenMultiplier { get; }
		int TextureAmplitudeSecondPassSmoothenMultiplier { get; }

		DataRectangle<IRgBy> IRgByCalculator(DataRectangle<RGB> values);
		int RgBySmoothenMultiplier { get; }

		/// <summary>
		/// A first pass is made to try to create a skin mask, this filter dictates what pixels are acceptable for that pass (taking into account hue, saturation and
		/// texture amplitude)
		/// </summary>
		bool SkinFilter(HueSaturation colour);
		/// <summary>
		/// After the first skin mask pass, a number of subsequent passes (see NumberOfSkinMaskRelaxedExpansions) are made to expand the mask to include any nearby pixels
		/// using more relaxed criteria (to make it more likely that edge pixels that are in shade, for example, are captured)
		/// </summary>
		bool RelaxedSkinFilter(HueSaturation colour);
		int NumberOfSkinMaskRelaxedExpansions { get; }

		/// <summary>
		/// Some regions may be ignore outright if their aspect ratios seem wrong (a very long, narrow region is unlikely to be a meaninful face capture, for example)
		/// </summary>
		IEnumerable<Rectangle> FaceRegionAspectRatioFilter(IEnumerable<Rectangle> areas);
		double PercentToExpandFinalFaceRegionBy { get; }
	}
}
