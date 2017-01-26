using Common;

namespace FaceClassifier.Normalisation
{
	public delegate DataRectangle<HistogramOfGradient> Normaliser(DataRectangle<HistogramOfGradient> hogs);
}
