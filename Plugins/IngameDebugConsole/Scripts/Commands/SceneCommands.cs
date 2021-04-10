using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace IngameDebugConsole.Commands
{
	public class SceneCommands
	{
		[ConsoleMethod( "scene.load", "Loads a scene" )]
		public static void LoadScene( string sceneName, LoadSceneMode mode )
		{
			if ( SceneManager.GetSceneByName( sceneName ).IsValid() )
			{
				Debug.Log( "Scene " + sceneName + " already loaded" );
				return;
			}
			SceneManager.LoadScene( sceneName, mode );
		}

		[ConsoleMethod( "scene.loadasync", "Loads a scene asynchronously" )]
		public static void LoadSceneAsync( string sceneName, LoadSceneMode mode )
		{
			if ( SceneManager.GetSceneByName( sceneName ).IsValid() )
			{
				Debug.Log( "Scene " + sceneName + " already loaded" );
				return;
			}
			SceneManager.LoadSceneAsync( sceneName, mode );
		}

		[ConsoleMethod( "scene.unload", "Unloads a scene" )]
		public static void UnloadScene( string sceneName )
		{
			SceneManager.UnloadSceneAsync( sceneName );
		}

		[ConsoleMethod( "scene.restart", "Restarts the active scene" )]
		public static void RestartScene()
		{
			SceneManager.LoadScene( SceneManager.GetActiveScene().name, LoadSceneMode.Single );
		}
	}
}