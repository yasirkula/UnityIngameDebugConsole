using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Manager class for the debug popup
namespace IngameDebugConsole
{
	public class DebugLogPopup : MonoBehaviour, IPointerClickHandler
	{
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
		private int newInfoCount;
		private int newWarningCount;
		private int newErrorCount;

		private Color normalColor;

		private void Awake()
		{
			backgroundImage = GetComponent<Image>();
			canvasGroup = GetComponent<CanvasGroup>();

			normalColor = backgroundImage.color;
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

		// Popup is clicked
		public void OnPointerClick( PointerEventData _ ) => debugManager.ShowLogWindow();

		// Hides the log window and shows the popup
		public void Show()
		{
			canvasGroup.blocksRaycasts = true;
			canvasGroup.alpha = 1f;

			// Reset the counters
			Reset();
		}

		// Hide the popup
		public void Hide()
		{
			canvasGroup.blocksRaycasts = false;
			canvasGroup.alpha = 0f;
		}
	}
}