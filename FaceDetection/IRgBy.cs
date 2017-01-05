namespace FaceDetection
{
	public struct IRgBy
	{
		public IRgBy(double rg, double by, double i)
		{
			Rg = rg;
			By = by;
			I = i;
		}
		public double Rg { get; }
		public double By { get; }
		public double I { get; }
		public override string ToString()
		{
			return $"IRgBy:{Rg}:{By}:{I}";
		}
	}
}
