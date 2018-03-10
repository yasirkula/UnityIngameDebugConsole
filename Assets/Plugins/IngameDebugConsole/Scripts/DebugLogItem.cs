using UnityEngine;
using UnityEngine.UI;

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
		public int Index { get { return entryIndex; } }

		private DebugLogRecycledListView manager;
		
		public void Initialize( DebugLogRecycledListView manager )
		{
			this.manager = manager;
		}

		public void SetContent( DebugLogEntry logEntry, int entryIndex, bool isExpanded )
		{
			this.logEntry = logEntry;
			this.entryIndex = entryIndex;
			
			Vector2 size = transformComponent.sizeDelta;
			if( isExpanded )
			{
				logText.horizontalOverflow = HorizontalWrapMode.Wrap;
				size.y = manager.SelectedItemHeight;
			}
			else
			{
				logText.horizontalOverflow = HorizontalWrapMode.Overflow;
				size.y = manager.ItemHeight;
			}
			transformComponent.sizeDelta = size;

			logText.text = isExpanded ? logEntry.ToString() : logEntry.logString;
			logTypeImage.sprite = logEntry.logTypeSpriteRepresentation;
		}

		// Show the collapsed count of the debug entry
		public void ShowCount()
		{
			logCountText.text = logEntry.count.ToString();
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
			manager.OnLogItemClicked( this );
		}

		public float CalculateExpandedHeight( string content )
		{
			string text = logText.text;
			HorizontalWrapMode wrapMode = logText.horizontalOverflow;

			logText.text = content;
			logText.horizontalOverflow = HorizontalWrapMode.Wrap;

			float result = logText.preferredHeight;

			logText.text = text;
			logText.horizontalOverflow = wrapMode;

			return Mathf.Max( manager.ItemHeight, result );
		}

		// Return a string containing complete information about the debug entry
		public override string ToString()
		{
			return logEntry.ToString();
		}
	}
}