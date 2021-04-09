using UnityEngine;

namespace IngameDebugConsole.Commands
{
	public class PlayerPrefsCommands
	{
		[ConsoleMethod( "pp.get", "Get the value of a PlayerPrefs field" )]
		public static string PlayerPrefsGet( string key, string type )
		{
			if ( !PlayerPrefs.HasKey( key ) ) return "Key Not Found";
			if ( type.ToLower() == "string" ) return PlayerPrefs.GetString( key );
			if ( type.ToLower() == "float" ) return PlayerPrefs.GetFloat( key ).ToString();
			if ( type.ToLower() == "int" ) return PlayerPrefs.GetInt( key ).ToString();
			return "Invalid types";
		}

		[ConsoleMethod( "pp.set", "Set the value of a PlayerPrefs field" )]
		public static void PlayerPrefsSet( string key, string type, string value )
		{
			if ( type.ToLower() == "string" ) PlayerPrefs.SetString( key, value );
			if ( type.ToLower() == "float" ) PlayerPrefs.SetFloat( key, float.Parse( value ) );
			if ( type.ToLower() == "int" ) PlayerPrefs.SetInt( key, int.Parse( value ) );
		}

		[ConsoleMethod( "pp.del", "Delete a PlayerPrefs field" )]
		public static void PlayerPrefsDelete(string key)
		{
			PlayerPrefs.DeleteKey( key );
		}

		[ConsoleMethod( "pp.clear", "Delete all PlayerPrefs fields" )]
		public static void PlayerPrefsClear()
		{
			PlayerPrefs.DeleteAll();
		}
	}
}