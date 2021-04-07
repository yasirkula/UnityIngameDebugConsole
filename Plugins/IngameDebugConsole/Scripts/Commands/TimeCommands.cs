using UnityEngine;

namespace IngameDebugConsole.Commands
{
	public class TimeCommands
	{
		[ConsoleMethod("time.scale", "Set the Time.timeScale value", "value")]
		public static void SetTimeScale(float value) => Time.timeScale = value;

		[ConsoleMethod("time.currentscale", "Get the current Time.timeScale value")]
		public static float GetCurrentScale() => Time.timeScale;
	}
}
