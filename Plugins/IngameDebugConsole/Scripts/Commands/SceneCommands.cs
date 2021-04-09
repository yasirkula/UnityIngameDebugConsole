using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace IngameDebugConsole.Commands
{
	public class SceneCommands
	{
		[ConsoleMethod( "scene.load", "Load a scene" )]
		public static void LoadScene( string sceneName, LoadSceneMode mode, bool isAsync )
		{
			if ( SceneManager.GetSceneByName( sceneName ).IsValid() )
			{
				Debug.Log( "Scene " + sceneName + " already loaded" );
				return;
			}
			if ( isAsync ) SceneManager.LoadSceneAsync( sceneName, mode );
			else SceneManager.LoadScene( sceneName, mode );
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
				LoadSceneMode.Single,
				isAsync );
		}
	}
}