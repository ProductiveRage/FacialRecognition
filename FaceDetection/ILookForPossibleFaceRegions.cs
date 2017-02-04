using System.Collections.Generic;
using System.Drawing;

namespace FaceDetection
{
	public interface ILookForPossibleFaceRegions
	{
		IEnumerable<Rectangle> GetPossibleFaceRegions(Bitmap source);
	}
}
