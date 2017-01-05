namespace FaceDetection
{
	public struct HueSaturation
	{
		public HueSaturation(double hue, double saturation, double textureAmplitude)
		{
			Hue = hue;
			Saturation = saturation;
			TextureAmplitude = textureAmplitude;
		}
		public double Hue { get; }
		public double Saturation { get; }
		public double TextureAmplitude { get; }
		public override string ToString()
		{
			return $"HS:{Hue}:{Saturation}:{TextureAmplitude}";
		}
	}
}
