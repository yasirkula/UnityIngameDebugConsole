using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace IngameDebugConsole.Commands
{
	public class SceneCommands
	{
		[ConsoleMethod( "scene.load", "Load a scene", "sceneName", "mode" )]
		public static void LoadScene( string sceneName, string mode )
		{
			if ( SceneManager.GetSceneByName( sceneName ).IsValid() )
			{
				Debug.Log( $"Scene {sceneName} already loaded" );
				return;
			}
			var parsedMode = ( LoadSceneMode )Enum.Parse( typeof( LoadSceneMode ), mode, true );
			SceneManager.LoadSceneAsync( sceneName, parsedMode );
		}

		[ConsoleMethod( "scene.unload", "Unload a scene", "sceneName" )]
		public static void UnloadScene( string sceneName ) =>
			SceneManager.UnloadSceneAsync( sceneName );
	}
}