using System.Drawing;

namespace FaceClassifier
{
	public interface IClassifyPotentialFaces
	{
		bool IsFace(Bitmap image);
	}
}