using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
using System.Text.RegularExpressions;
#endif

// A UI element to show information about a debug entry
namespace IngameDebugConsole
{
	public class DebugLogItem : MonoBehaviour, IPointerClickHandler
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
		public void OnPointerClick( PointerEventData eventData )
		{
#if UNITY_EDITOR
			if( eventData.button == PointerEventData.InputButton.Right )
			{
				Match regex = Regex.Match( logEntry.stackTrace, @"\(at .*\.cs:[0-9]+\)$", RegexOptions.Multiline );
				if( regex.Success )
				{
					string line = logEntry.stackTrace.Substring( regex.Index + 4, regex.Length - 5 );
					int lineSeparator = line.IndexOf( ':' );
					MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>( line.Substring( 0, lineSeparator ) );
					if( script != null )
						AssetDatabase.OpenAsset( script, int.Parse( line.Substring( lineSeparator + 1 ) ) );
				}
			}
			else
				manager.OnLogItemClicked( this );
#else
			manager.OnLogItemClicked( this );
#endif
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