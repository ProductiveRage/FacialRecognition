using System;
using System.Diagnostics;

namespace Tester
{
	public sealed class IntervalTimer
	{
		private readonly Action<string> _writeTo;
		private readonly Stopwatch _timerSinceLastEvent;
		private readonly Stopwatch _timer;
		public IntervalTimer(Action<string> writeTo)
		{
			if (writeTo == null)
				throw new ArgumentNullException(nameof(writeTo));

			_writeTo = writeTo;
			_timerSinceLastEvent = Stopwatch.StartNew();
			_timer = Stopwatch.StartNew();
		}
		public void Log(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentException($"Null/blank {nameof(message)} specified");

			var timeSinceLastEvent = _timerSinceLastEvent.Elapsed;
			var totalTimeSoFar = _timer.Elapsed;
			_timerSinceLastEvent.Restart();
			_writeTo($"[{Math.Round(timeSinceLastEvent.TotalMilliseconds)}ms / {Math.Round(totalTimeSoFar.TotalMilliseconds)}ms] {message}");
		}
	}
}