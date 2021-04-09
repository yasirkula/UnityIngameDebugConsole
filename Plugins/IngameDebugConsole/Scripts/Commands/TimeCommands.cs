using UnityEngine;

namespace IngameDebugConsole.Commands
{
	public class TimeCommands
	{
		[ConsoleMethod( "time.scale", "Sets the Time.timeScale value" )]
		public static void SetTimeScale( float value )
		{
			Time.timeScale = Mathf.Max( value, 0f );
		}

		[ConsoleMethod( "time.scale", "Returns the current Time.timeScale value" )]
		public static float GetCurrentScale()
		{
			return Time.timeScale;
		}
	}
}