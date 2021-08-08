//#define IDG_OMIT_ELAPSED_TIME
//#define IDG_OMIT_FRAMECOUNT

using System.Text;
using UnityEngine;

// Container for a simple debug entry
namespace IngameDebugConsole
{
	public class DebugLogEntry : System.IEquatable<DebugLogEntry>
	{
		private const int HASH_NOT_CALCULATED = -623218;

		public string logString;
		public string stackTrace;

		private string completeLog;

		// Sprite to show with this entry
		public Sprite logTypeSpriteRepresentation;

		// Collapsed count
		public int count;

		private int hashValue;

		public void Initialize( string logString, string stackTrace )
		{
			this.logString = logString;
			this.stackTrace = stackTrace;

			completeLog = null;
			count = 1;
			hashValue = HASH_NOT_CALCULATED;
		}

		// Check if two entries have the same origin
		public bool Equals( DebugLogEntry other )
		{
			return this.logString == other.logString && this.stackTrace == other.stackTrace;
		}

		// Checks if logString or stackTrace contains the search term
		public bool MatchesSearchTerm( string searchTerm )
		{
			return ( logString != null && logString.IndexOf( searchTerm, System.StringComparison.OrdinalIgnoreCase ) >= 0 ) ||
				( stackTrace != null && stackTrace.IndexOf( searchTerm, System.StringComparison.OrdinalIgnoreCase ) >= 0 );
		}

		// Return a string containing complete information about this debug entry
		public override string ToString()
		{
			if( completeLog == null )
				completeLog = string.Concat( logString, "\n", stackTrace );

			return completeLog;
		}

		// Credit: https://stackoverflow.com/a/19250516/2373034
		public override int GetHashCode()
		{
			if( hashValue == HASH_NOT_CALCULATED )
			{
				unchecked
				{
					hashValue = 17;
					hashValue = hashValue * 23 + ( logString == null ? 0 : logString.GetHashCode() );
					hashValue = hashValue * 23 + ( stackTrace == null ? 0 : stackTrace.GetHashCode() );
				}
			}

			return hashValue;
		}
	}

	public struct QueuedDebugLogEntry
	{
		public readonly string logString;
		public readonly string stackTrace;
		public readonly LogType logType;

		public QueuedDebugLogEntry( string logString, string stackTrace, LogType logType )
		{
			this.logString = logString;
			this.stackTrace = stackTrace;
			this.logType = logType;
		}

		// Checks if logString or stackTrace contains the search term
		public bool MatchesSearchTerm( string searchTerm )
		{
			return ( logString != null && logString.IndexOf( searchTerm, System.StringComparison.OrdinalIgnoreCase ) >= 0 ) ||
				( stackTrace != null && stackTrace.IndexOf( searchTerm, System.StringComparison.OrdinalIgnoreCase ) >= 0 );
		}
	}

	public struct DebugLogEntryTimestamp
	{
		public readonly System.DateTime dateTime;
#if !IDG_OMIT_ELAPSED_TIME
		public readonly float elapsedSeconds;
#endif
#if !IDG_OMIT_FRAMECOUNT
		public readonly int frameCount;
#endif

		public DebugLogEntryTimestamp( System.TimeSpan localTimeUtcOffset )
		{
			// It is 10 times faster to cache local time's offset from UtcNow and add it to UtcNow to get local time at any time
			dateTime = System.DateTime.UtcNow + localTimeUtcOffset;
#if !IDG_OMIT_ELAPSED_TIME
			elapsedSeconds = Time.realtimeSinceStartup;
#endif
#if !IDG_OMIT_FRAMECOUNT
			frameCount = Time.frameCount;
#endif
		}

		public void AppendTime( StringBuilder sb )
		{
			// Add DateTime in format: [HH:mm:ss]
			sb.Append( "[" );

			int hour = dateTime.Hour;
			if( hour >= 10 )
				sb.Append( hour );
			else
				sb.Append( "0" ).Append( hour );

			sb.Append( ":" );

			int minute = dateTime.Minute;
			if( minute >= 10 )
				sb.Append( minute );
			else
				sb.Append( "0" ).Append( minute );

			sb.Append( ":" );

			int second = dateTime.Second;
			if( second >= 10 )
				sb.Append( second );
			else
				sb.Append( "0" ).Append( second );

			sb.Append( "]" );
		}

		public void AppendFullTimestamp( StringBuilder sb )
		{
			AppendTime( sb );

#if !IDG_OMIT_ELAPSED_TIME && !IDG_OMIT_FRAMECOUNT
			// Append elapsed seconds and frame count in format: [1.0s at #Frame]
			sb.Append( "[" ).Append( elapsedSeconds.ToString( "F1" ) ).Append( "s at " ).Append( "#" ).Append( frameCount ).Append( "]" );
#elif !IDG_OMIT_ELAPSED_TIME
			// Append elapsed seconds in format: [1.0s]
			sb.Append( "[" ).Append( elapsedSeconds.ToString( "F1" ) ).Append( "s]" );
#elif !IDG_OMIT_FRAMECOUNT
			// Append frame count in format: [#Frame]
			sb.Append( "[#" ).Append( frameCount ).Append( "]" );
#endif
		}
	}
}