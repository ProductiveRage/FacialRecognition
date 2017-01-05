using System.Collections.Generic;
using System.Drawing;

namespace FaceDetection
{
	// TODO: Document all options
	public interface IExposeConfigurationOptions
	{
		int TextureAmplitudeFirstPassSmoothenMultiplier { get; }
		int TextureAmplitudeSecondPassSmoothenMultiplier { get; }
		DataRectangle<IRgBy> IRgByCalculator(DataRectangle<RGB> values);
		int RgBySmoothenMultiplier { get; }
		bool SkinFilter(HueSaturation colour);
		int NumberOfSkinMaskRelaxedExpansions { get; }
		bool RelaxedSkinFilter(HueSaturation colour);
		IEnumerable<Rectangle> FaceRegionAspectRatioFilter(IEnumerable<Rectangle> areas);
		double PercentToExpandFinalFaceRegionBy { get; }
	}
}
