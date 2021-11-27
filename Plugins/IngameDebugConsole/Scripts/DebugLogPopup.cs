using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// Manager class for the debug popup
namespace IngameDebugConsole
{
	public class DebugLogPopup : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		private RectTransform popupTransform;

		// Dimensions of the popup divided by 2
		private Vector2 halfSize;

		// Background image that will change color to indicate an alert
		private Image backgroundImage;

		// Canvas group to modify visibility of the popup
		private CanvasGroup canvasGroup;

#pragma warning disable 0649
		[SerializeField]
		private DebugLogManager debugManager;

		[SerializeField]
		private Text newInfoCountText;
		[SerializeField]
		private Text newWarningCountText;
		[SerializeField]
		private Text newErrorCountText;

		[SerializeField]
		private Color alertColorInfo;
		[SerializeField]
		private Color alertColorWarning;
		[SerializeField]
		private Color alertColorError;
#pragma warning restore 0649

		// Number of new debug entries since the log window has been closed
		private int newInfoCount = 0, newWarningCount = 0, newErrorCount = 0;

		private Color normalColor;

		private bool isPopupBeingDragged = false;
		private Vector2 normalizedPosition;

		// Coroutines for simple code-based animations
		private IEnumerator moveToPosCoroutine = null;

		private void Awake()
		{
			popupTransform = (RectTransform) transform;
			backgroundImage = GetComponent<Image>();
			canvasGroup = GetComponent<CanvasGroup>();

			normalColor = backgroundImage.color;

			halfSize = popupTransform.sizeDelta * 0.5f;

			Vector2 pos = popupTransform.anchoredPosition;
			if( pos.x != 0f || pos.y != 0f )
				normalizedPosition = pos.normalized; // Respect the initial popup position set in the prefab
			else
				normalizedPosition = new Vector2( 0.5f, 0f ); // Right edge by default
		}

		public void NewLogsArrived( int newInfo, int newWarning, int newError )
		{
			if( newInfo > 0 )
			{
				newInfoCount += newInfo;
				newInfoCountText.text = newInfoCount.ToString();
			}

			if( newWarning > 0 )
			{
				newWarningCount += newWarning;
				newWarningCountText.text = newWarningCount.ToString();
			}

			if( newError > 0 )
			{
				newErrorCount += newError;
				newErrorCountText.text = newErrorCount.ToString();
			}

			if( newErrorCount > 0 )
				backgroundImage.color = alertColorError;
			else if( newWarningCount > 0 )
				backgroundImage.color = alertColorWarning;
			else
				backgroundImage.color = alertColorInfo;
		}

		private void Reset()
		{
			newInfoCount = 0;
			newWarningCount = 0;
			newErrorCount = 0;

			newInfoCountText.text = "0";
			newWarningCountText.text = "0";
			newErrorCountText.text = "0";

			backgroundImage.color = normalColor;
		}

		// A simple smooth movement animation
		private IEnumerator MoveToPosAnimation( Vector2 targetPos )
		{
			float modifier = 0f;
			Vector2 initialPos = popupTransform.anchoredPosition;

			while( modifier < 1f )
			{
				modifier += 4f * Time.unscaledDeltaTime;
				popupTransform.anchoredPosition = Vector2.Lerp( initialPos, targetPos, modifier );

				yield return null;
			}
		}

		// Popup is clicked
		public void OnPointerClick( PointerEventData data )
		{
			// Hide the popup and show the log window
			if( !isPopupBeingDragged )
				debugManager.ShowLogWindow();
		}

		// Hides the log window and shows the popup
		public void Show()
		{
			canvasGroup.blocksRaycasts = true;
			canvasGroup.alpha = 1f;

			// Reset the counters
			Reset();

			// Update position in case resolution was changed while the popup was hidden
			UpdatePosition( true );
		}

		// Hide the popup
		public void Hide()
		{
			canvasGroup.blocksRaycasts = false;
			canvasGroup.alpha = 0f;

			isPopupBeingDragged = false;
		}

		public void OnBeginDrag( PointerEventData data )
		{
			isPopupBeingDragged = true;

			// If a smooth movement animation is in progress, cancel it
			if( moveToPosCoroutine != null )
			{
				StopCoroutine( moveToPosCoroutine );
				moveToPosCoroutine = null;
			}
		}

		// Reposition the popup
		public void OnDrag( PointerEventData data )
		{
			Vector2 localPoint;
			if( RectTransformUtility.ScreenPointToLocalPointInRectangle( debugManager.canvasTR, data.position, data.pressEventCamera, out localPoint ) )
				popupTransform.anchoredPosition = localPoint;
		}

		// Smoothly translate the popup to the nearest edge
		public void OnEndDrag( PointerEventData data )
		{
			isPopupBeingDragged = false;
			UpdatePosition( false );
		}

		public void UpdatePosition( bool immediately )
		{
			Vector2 canvasSize = debugManager.canvasTR.rect.size;

			float canvasWidth = canvasSize.x;
			float canvasHeight = canvasSize.y;

			// normalizedPosition allows us to glue the popup to a specific edge of the screen. It becomes useful when
			// the popup is at the right edge and we switch from portrait screen orientation to landscape screen orientation.
			// Without normalizedPosition, popup could jump to bottom or top edges instead of staying at the right edge
			Vector2 pos = immediately ? new Vector2( normalizedPosition.x * canvasWidth, normalizedPosition.y * canvasHeight ) : popupTransform.anchoredPosition;

			// Find distances to all four edges
			float distToLeft = canvasWidth * 0.5f + pos.x;
			float distToRight = canvasWidth - distToLeft;

			float distToBottom = canvasHeight * 0.5f + pos.y;
			float distToTop = canvasHeight - distToBottom;

			float horDistance = Mathf.Min( distToLeft, distToRight );
			float vertDistance = Mathf.Min( distToBottom, distToTop );

			// Find the nearest edge's coordinates
			if( horDistance < vertDistance )
			{
				if( distToLeft < distToRight )
					pos = new Vector2( canvasWidth * -0.5f + halfSize.x, pos.y );
				else
					pos = new Vector2( canvasWidth * 0.5f - halfSize.x, pos.y );

				pos.y = Mathf.Clamp( pos.y, canvasHeight * -0.5f + halfSize.y, canvasHeight * 0.5f - halfSize.y );
			}
			else
			{
				if( distToBottom < distToTop )
					pos = new Vector2( pos.x, canvasHeight * -0.5f + halfSize.y );
				else
					pos = new Vector2( pos.x, canvasHeight * 0.5f - halfSize.y );

				pos.x = Mathf.Clamp( pos.x, canvasWidth * -0.5f + halfSize.x, canvasWidth * 0.5f - halfSize.x );
			}

			normalizedPosition.Set( pos.x / canvasWidth, pos.y / canvasHeight );

			// If another smooth movement animation is in progress, cancel it
			if( moveToPosCoroutine != null )
			{
				StopCoroutine( moveToPosCoroutine );
				moveToPosCoroutine = null;
			}

			if( immediately )
				popupTransform.anchoredPosition = pos;
			else
			{
				// Smoothly translate the popup to the specified position
				moveToPosCoroutine = MoveToPosAnimation( pos );
				StartCoroutine( moveToPosCoroutine );
			}
		}
	}
}