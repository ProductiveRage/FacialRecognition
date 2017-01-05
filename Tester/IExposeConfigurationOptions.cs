using System.Collections.Generic;
using System.Drawing;
using FaceDetection;

namespace Tester
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
		bool EnableSecondSkinMaskExpansion { get; }
		IEnumerable<Rectangle> FaceRegionAspectRatioFilter(IEnumerable<Rectangle> areas);
		double PercentToExpandFinalFaceRegionBy { get; }
		Color OutlineColour { get; }
	}
}
