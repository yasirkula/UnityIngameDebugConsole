using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace IngameDebugConsole.Commands
{
	public class SceneCommands
	{
		[ConsoleMethod( "scene.load", "Load a scene" )]
		public static void LoadScene( string sceneName, string mode, bool isAsync )
		{
			if ( SceneManager.GetSceneByName( sceneName ).IsValid() )
			{
				Debug.Log( "Scene " + sceneName + " already loaded" );
				return;
			}
			var parsedMode = ( LoadSceneMode )Enum.Parse( typeof( LoadSceneMode ), mode, true );
			if ( isAsync ) SceneManager.LoadSceneAsync( sceneName, parsedMode );
			else SceneManager.LoadScene( sceneName, parsedMode );
		}

		[ConsoleMethod( "scene.unload", "Unload a scene" )]
		public static void UnloadScene( string sceneName, bool isAsync )
		{
			if ( isAsync ) SceneManager.UnloadSceneAsync( sceneName );
			else SceneManager.UnloadScene( sceneName );
		}

		[ConsoleMethod( "scene.restart", "Restart the active scene" )]
		public static void RestartScene( bool isAsync )
		{
			LoadScene( SceneManager.GetActiveScene().name,
				LoadSceneMode.Single.ToString(),
				isAsync );
		}
	}
}