using UnityEngine;
using UnityEngine.UI;

// In-game Debug Console / DebugLogItem
// Author: Suleyman Yasir Kula
// 
// A UI element to show information about a debug entry
namespace IngameDebugConsole
{
	public class DebugLogItem : MonoBehaviour
	{
		// Cached components
		[SerializeField]
		private RectTransform transformComponent;
		public RectTransform Transform { get { return transformComponent; } }

		[SerializeField]
		private Image imageComponent;
		public Image Image { get { return imageComponent; } }

		[SerializeField]
		private Text logText;
		[SerializeField]
		private Image logTypeImage;

		// Objects related to the collapsed count of the debug entry
		[SerializeField]
		private GameObject logCountParent;
		[SerializeField]
		private Text logCountText;

		// Debug entry to show with this log item
		private DebugLogEntry logEntry;

		// Index of the entry in the list of entries
		private int entryIndex;

		public void SetContent( DebugLogEntry logEntry, int entryIndex )
		{
			this.logEntry = logEntry;
			this.entryIndex = entryIndex;

			logText.text = logEntry.logString;
			logTypeImage.sprite = logEntry.logTypeSpriteRepresentation;
		}

		// Show the collapsed count of the debug entry
		public void ShowCount()
		{
			logCountText.text = "" + logEntry.count;
			logCountParent.SetActive( true );
		}

		// Hide the collapsed count of the debug entry
		public void HideCount()
		{
			logCountParent.SetActive( false );
		}

		// This log item is clicked, show the debug entry's stack trace
		public void Clicked()
		{
			DebugLogManager.OnLogClicked( entryIndex );
		}

		// Return a string containing complete information about the debug entry
		public override string ToString()
		{
			return logEntry.ToString();
		}
	}
}