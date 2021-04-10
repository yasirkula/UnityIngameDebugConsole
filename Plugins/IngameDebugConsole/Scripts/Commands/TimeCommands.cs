using UnityEngine;

namespace IngameDebugConsole.Commands
{
	[UnityEngine.Scripting.Preserve]
	public class TimeCommands
	{
		[ConsoleMethod( "time.scale", "Sets the Time.timeScale value" )]
		public static void SetTimeScale( float value )
		{
			Time.timeScale = Mathf.Max( value, 0f );
		}

		[ConsoleMethod( "time.scale", "Returns the current Time.timeScale value" )]
		public static float GetTimeScale()
		{
			return Time.timeScale;
		}
	}
}