using System.Collections.Generic;
using System.Drawing;
using Common;

namespace FaceDetection
{
	public interface IExposeConfigurationOptions
	{
		/// <summary>
		/// If either dimension of the input image is larger than this then it will be resized before processing so that the largest edge is this many pixels long (this potential
		/// face region rectangles returned will be scaled back up so that they correspond to the original image)
		/// </summary>
		int MaximumImageDimension { get; }

		int TextureAmplitudeFirstPassSmoothenMultiplier { get; }
		int TextureAmplitudeSecondPassSmoothenMultiplier { get; }

		/// <summary>
		/// When smoothening colour and texture amplitude data, the magnitude of smoothening depends upon the size of the source image - large images will need more smoothening
		/// to get the same effect as less processing applied to small images
		/// </summary>
		int CalculateScale(int width, int height);

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
