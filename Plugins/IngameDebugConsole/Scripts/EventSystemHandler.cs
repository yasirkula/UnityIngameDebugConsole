using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace IngameDebugConsole
{
	// Avoid multiple EventSystems in the scene by activating the embedded EventSystem only if one doesn't already exist in the scene
	[DefaultExecutionOrder( 1000 )]
	public class EventSystemHandler : MonoBehaviour
	{
#pragma warning disable 0649
		[SerializeField]
		private GameObject embeddedEventSystem;
#pragma warning restore 0649

		private void OnEnable()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;

			ActivateEventSystemIfNeeded();
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;

			DeactivateEventSystem();
		}

		private void OnSceneLoaded( Scene scene, LoadSceneMode mode )
		{
#if UNITY_2017_2_OR_NEWER
			DeactivateEventSystem();
#endif
			ActivateEventSystemIfNeeded();
		}

		private void OnSceneUnloaded( Scene current )
		{
			// Deactivate the embedded EventSystem before changing scenes because the new scene might have its own EventSystem
			DeactivateEventSystem();
		}

		private void ActivateEventSystemIfNeeded()
		{
			if( embeddedEventSystem && !EventSystem.current )
				embeddedEventSystem.SetActive( true );
		}

		private void DeactivateEventSystem()
		{
			if( embeddedEventSystem )
				embeddedEventSystem.SetActive( false );
		}
	}
}