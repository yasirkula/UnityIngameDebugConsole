using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// In-game Debug Console / DebugLogPopup
// Author: Suleyman Yasir Kula
// 
// Manager class for the debug popup
namespace IngameDebugConsole
{
	public class DebugLogPopup : MonoBehaviour, IPointerClickHandler, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		private Transform popupTransform;

		// Dimensions of the popup divided by 2
		private Vector2 halfSize;

		// Background image that will change color to indicate an alert
		private Image backgroundImage;

		// Canvas group to modify visibility of the popup
		private CanvasGroup canvasGroup;

		[SerializeField]
		private DebugLogManager debugManager;

		[SerializeField]
		private GameObject dropHereText;

		[SerializeField]
		private GameObject newLogCountsParent;

		[SerializeField]
		private Text newInfoCountText;
		[SerializeField]
		private Text newWarningCountText;
		[SerializeField]
		private Text newErrorCountText;

		// Number of new debug entries since the log window has been closed
		private int newInfoCount = 0, newWarningCount = 0, newErrorCount = 0;

		[SerializeField]
		private Color normalColor;
		[SerializeField]
		private Color alertColor;

		private bool isPopupBeingDragged = false;

		// Coroutines for simple code-based animations
		private IEnumerator fadeInCoroutine = null, fadeOutCoroutine = null;
		private IEnumerator moveToPosCoroutine = null;

		void Awake()
		{
			popupTransform = transform;
			backgroundImage = GetComponent<Image>();
			canvasGroup = GetComponent<CanvasGroup>();
		}

		void Start()
		{
			// Simple translation
			if( Application.systemLanguage == SystemLanguage.Turkish )
				dropHereText.GetComponent<Text>().text = "Gizlemek için buraya sürükle";

			halfSize = ( (RectTransform) popupTransform ).sizeDelta * 0.5f * popupTransform.root.localScale.x;
		}

		public void NewInfoLogArrived()
		{
			newInfoCount++;
			newInfoCountText.text = "" + newInfoCount;

			backgroundImage.color = alertColor;
		}

		public void NewWarningLogArrived()
		{
			newWarningCount++;
			newWarningCountText.text = "" + newWarningCount;

			backgroundImage.color = alertColor;
		}

		public void NewErrorLogArrived()
		{
			newErrorCount++;
			newErrorCountText.text = "" + newErrorCount;

			backgroundImage.color = alertColor;
		}

		private void Reset()
		{
			newInfoCount = 0;
			newWarningCount = 0;
			newErrorCount = 0;

			newInfoCountText.text = "0";
			newWarningCountText.text = "0";
			newErrorCountText.text = "0";
		}

		// Show the popup on screen
		public void OnSetVisible()
		{
			canvasGroup.interactable = true;
			canvasGroup.blocksRaycasts = true;

			backgroundImage.color = normalColor;

			// If popup was fading out, stop that animation
			if( fadeOutCoroutine != null )
				StopCoroutine( fadeOutCoroutine );

			// Fade in with a simple animation
			fadeInCoroutine = FadeInAnimation();
			StartCoroutine( fadeInCoroutine );
		}

		// Hide the popup
		public void OnSetInvisible( bool instantly )
		{
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;

			if( !instantly )
			{
				// If popup was fading in, stop that animation
				if( fadeInCoroutine != null )
					StopCoroutine( fadeInCoroutine );

				// Fade out with a simple animation
				fadeOutCoroutine = FadeOutAnimation();
				StartCoroutine( fadeOutCoroutine );
			}
			else
			{
				// Fade out instantly
				canvasGroup.alpha = 0f;
			}
		}

		// A simple fade-in animation
		private IEnumerator FadeInAnimation()
		{
			float alpha = canvasGroup.alpha;

			while( alpha < 1f )
			{
				alpha += 4f * Time.deltaTime;
				canvasGroup.alpha = alpha;

				yield return null;
			}
		}


		// A simple fade-out animation
		private IEnumerator FadeOutAnimation()
		{
			float alpha = canvasGroup.alpha;

			while( alpha > 0f )
			{
				alpha -= 4f * Time.deltaTime;
				canvasGroup.alpha = alpha;

				yield return null;
			}
		}

		// A simple smooth movement animation
		private IEnumerator MoveToPosAnimation( Vector3 targetPos )
		{
			float modifier = 0f;
			Vector3 initialPos = popupTransform.position;

			while( modifier < 1f )
			{
				modifier += 4f * Time.deltaTime;
				popupTransform.position = Vector3.Lerp( initialPos, targetPos, modifier );

				yield return null;
			}
		}

		// Popup is clicked
		public void OnPointerClick( PointerEventData data )
		{
			// If log window is not visible (but instead, popup is),
			// hide the popup and show the log window again
			if( newLogCountsParent.activeSelf && !isPopupBeingDragged )
			{
				dropHereText.SetActive( true );
				newLogCountsParent.SetActive( false );

				debugManager.OnSetVisible();

				OnSetInvisible( true );
			}
		}

		// Log window is dropped onto popup
		public void OnDrop( PointerEventData data )
		{
			// Make sure that we are expecting a drop
			if( dropHereText.activeSelf )
			{
				SwitchFromConsoleToPopup();
			}
		}

		// Hides the log window and shows the popup
		public void SwitchFromConsoleToPopup()
		{
			dropHereText.SetActive( false );
			newLogCountsParent.SetActive( true );

			// Reset the counters
			Reset();

			debugManager.OnSetInvisible();
		}

		public void OnBeginDrag( PointerEventData data )
		{
			isPopupBeingDragged = true;
		}

		// Reposition the popup
		public void OnDrag( PointerEventData data )
		{
			popupTransform.position = data.position;
		}

		// Smoothly translate the popup to the nearest edge
		public void OnEndDrag( PointerEventData data )
		{
			int screenWidth = Screen.width;
			int screenHeight = Screen.height;

			Vector3 pos = popupTransform.position;

			// Find distances to all four edges
			float distToLeft = pos.x;
			float distToRight = Mathf.Abs( pos.x - screenWidth );

			float distToBottom = Mathf.Abs( pos.y );
			float distToTop = Mathf.Abs( pos.y - screenHeight );

			float horDistance = Mathf.Min( distToLeft, distToRight );
			float vertDistance = Mathf.Min( distToBottom, distToTop );

			// Find the nearest edge's coordinates
			if( horDistance < vertDistance )
			{
				if( distToLeft < distToRight )
					pos = new Vector3( halfSize.x, pos.y, 0f );
				else
					pos = new Vector3( screenWidth - halfSize.x, pos.y, 0f );
			}
			else
			{
				if( distToBottom < distToTop )
					pos = new Vector3( pos.x, halfSize.y, 0f );
				else
					pos = new Vector3( pos.x, screenHeight - halfSize.y, 0f );
			}

			// If another smooth movement animation is in progress,
			// cancel it
			if( moveToPosCoroutine != null )
				StopCoroutine( moveToPosCoroutine );

			// Smoothly translate the popup to the specified position
			moveToPosCoroutine = MoveToPosAnimation( pos );
			StartCoroutine( moveToPosCoroutine );

			isPopupBeingDragged = false;
		}
	}
}