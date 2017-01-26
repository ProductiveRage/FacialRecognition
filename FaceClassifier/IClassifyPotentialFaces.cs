namespace FaceClassifier
{
	public interface IClassifyPotentialFaces
	{
		bool IsFace(double[] features); // TODO: This should take a bitmap or DataRectangle<RGB> or something (and perform resizing / aspect ratio correction)
	}
}