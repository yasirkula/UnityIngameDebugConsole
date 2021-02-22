using System;
using System.Security.Cryptography;
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

		private static MD5 Md5 = MD5.Create();
		private static byte[] HashingBuffer = new byte[128];

		public struct Md5Hash
		{
			public long value1;
			public long value2;
		}
		Md5Hash hashValue;

		public void Initialize(string logString, string stackTrace)
		{
			this.logString = logString;
			this.stackTrace = stackTrace;

			completeLog = null;
			count = 1;
			hashValue = new Md5Hash { value1 = 0, value2 = 0 };
		}

		// Check if two entries have the same origin
		public bool Equals( DebugLogEntry other )
		{
			return GetMD5Hash().value1 == other.GetMD5Hash().value1 && GetMD5Hash().value2 == other.GetMD5Hash().value2;
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

		public override int GetHashCode()
		{
			return (int)GetMD5Hash().value1;
		}

		public Md5Hash GetMD5Hash()
		{
			if (hashValue.value1 == 0 && hashValue.value2 == 0)
			{
				Md5.Initialize();
				Md5TransformString(Md5, HashingBuffer, logString);
				Md5TransformString(Md5, HashingBuffer, stackTrace);
				Md5.TransformFinalBlock(HashingBuffer, 0, 0);
				hashValue.value1 = BitConverter.ToInt64(Md5.Hash, 0);
				hashValue.value2 = BitConverter.ToInt64(Md5.Hash, 8);
			}
			return hashValue;
		}

		private static void Md5TransformString(MD5 md5, byte[] hashingBuffer, string str)
		{
			var startOfByteRange = 0;
			var strLenInBytes = str.Length * 2;
			while (startOfByteRange < strLenInBytes)
			{
				var translatedBytesLen = Math.Min(strLenInBytes - startOfByteRange, hashingBuffer.Length);
				for (var i = 0; i < translatedBytesLen; i += 2)
				{
					var ch = Convert.ToUInt16(str[i / 2]);
					hashingBuffer[i] = (byte)ch;
					hashingBuffer[i + 1] = (byte)(ch >> 8);
				}
				var hashedBytesLen = md5.TransformBlock(hashingBuffer, 0, translatedBytesLen, null, 0);
				startOfByteRange += hashedBytesLen;
			}
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
}