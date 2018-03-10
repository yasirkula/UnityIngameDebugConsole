using UnityEngine;

// In-game Debug Console / DebugLogEntry
// Author: Suleyman Yasir Kula
// 
// Container for a simple debug entry
namespace IngameDebugConsole
{
	public class DebugLogEntry : System.IEquatable<DebugLogEntry>
	{
		public string logString;
		public string stackTrace;

		// Sprite to show with this entry
		public Sprite logTypeSpriteRepresentation;

		// Collapsed count
		public int count;

		public DebugLogEntry( string logString, string stackTrace, Sprite sprite )
		{
			this.logString = logString;
			this.stackTrace = stackTrace;

			logTypeSpriteRepresentation = sprite;

			count = 1;
		}

		// Check if two entries have the same origin
		public bool Equals( DebugLogEntry other )
		{
			return this.logString == other.logString && this.stackTrace == other.stackTrace;
		}

		// Return a string containing complete information about this debug entry
		public override string ToString()
		{
			return string.Concat( logString, "\n", stackTrace );
		}
	}
}