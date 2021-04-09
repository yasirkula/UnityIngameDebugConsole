﻿using UnityEngine;

namespace IngameDebugConsole.Commands
{
	public class PlayerPrefsCommands
	{
		[ConsoleMethod( "prefs.int", "Returns the value of an Integer PlayerPrefs field" )]
		public static string PlayerPrefsGetInt( string key )
		{
			if ( !PlayerPrefs.HasKey( key ) ) return "Key Not Found";
			return PlayerPrefs.GetInt( key ).ToString();
		}

		[ConsoleMethod( "prefs.int", "Sets the value of an Integer PlayerPrefs field" )]
		public static void PlayerPrefsSetInt( string key, int value )
		{
			PlayerPrefs.SetInt( key, value );
		}

		[ConsoleMethod( "prefs.float", "Returns the value of a Float PlayerPrefs field" )]
		public static string PlayerPrefsGetFloat( string key )
		{
			if ( !PlayerPrefs.HasKey( key ) ) return "Key Not Found";
			return PlayerPrefs.GetFloat( key ).ToString();
		}

		[ConsoleMethod( "prefs.float", "Sets the value of a Float PlayerPrefs field" )]
		public static void PlayerPrefsSetFloat( string key, float value )
		{
			PlayerPrefs.SetFloat( key, value );
		}

		[ConsoleMethod( "prefs.string", "Returns the value of a String PlayerPrefs field" )]
		public static string PlayerPrefsGetString( string key )
		{
			if ( !PlayerPrefs.HasKey( key ) ) return "Key Not Found";
			return PlayerPrefs.GetString( key );
		}

		[ConsoleMethod( "prefs.string", "Sets the value of a String PlayerPrefs field" )]
		public static void PlayerPrefsSetString( string key, string value )
		{
			PlayerPrefs.SetString( key, value );
		}

		[ConsoleMethod( "prefs.delete", "Deletes a PlayerPrefs field" )]
		public static void PlayerPrefsDelete( string key )
		{
			PlayerPrefs.DeleteKey( key );
		}

		[ConsoleMethod( "prefs.clear", "Deletes all PlayerPrefs fields" )]
		public static void PlayerPrefsClear()
		{
			PlayerPrefs.DeleteAll();
		}
	}
}