using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using System.Text.RegularExpressions;
#endif

// A UI element to show information about a debug entry
namespace IngameDebugConsole
{
	public class DebugLogItem : MonoBehaviour, IPointerClickHandler
	{
#pragma warning disable 0649
		// Cached components
		[SerializeField]
		private RectTransform transformComponent;
		public RectTransform Transform { get { return transformComponent; } }

		[SerializeField]
		private Image imageComponent;
		public Image Image { get { return imageComponent; } }

		[SerializeField]
		private CanvasGroup canvasGroupComponent;
		public CanvasGroup CanvasGroup { get { return canvasGroupComponent; } }

		[SerializeField]
		private TextMeshProUGUI logText;
		[SerializeField]
		private Image logTypeImage;

		// Objects related to the collapsed count of the debug entry
		[SerializeField]
		private GameObject logCountParent;
		[SerializeField]
		private TextMeshProUGUI logCountText;

		[SerializeField]
		private Button copyLogButton;
#pragma warning restore 0649

		// Debug entry to show with this log item
		private DebugLogEntry logEntry;
		public DebugLogEntry Entry { get { return logEntry; } }

		private DebugLogEntryTimestamp? logEntryTimestamp;
		public DebugLogEntryTimestamp? Timestamp { get { return logEntryTimestamp; } }

		// Index of the entry in the list of entries
		[System.NonSerialized] public int Index;

		private bool isExpanded;
		public bool Expanded { get { return isExpanded; } }

		private Vector2 logTextOriginalPosition;
		private Vector2 logTextOriginalSize;
		private float copyLogButtonHeight;

		private DebugLogRecycledListView listView;

		public void Initialize( DebugLogRecycledListView listView )
		{
			this.listView = listView;

			logTextOriginalPosition = logText.rectTransform.anchoredPosition;
			logTextOriginalSize = logText.rectTransform.sizeDelta;
			copyLogButtonHeight = ( copyLogButton.transform as RectTransform ).anchoredPosition.y + ( copyLogButton.transform as RectTransform ).sizeDelta.y + 2f; // 2f: space between text and button

            if (listView.manager.logItemFontOverride != null)
                logText.font = listView.manager.logItemFontOverride;

			copyLogButton.onClick.AddListener( CopyLog );
#if !UNITY_EDITOR && UNITY_WEBGL
			copyLogButton.gameObject.AddComponent<DebugLogItemCopyWebGL>().Initialize( this );
#endif
		}

		public void SetContent( DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp, int entryIndex, bool isExpanded )
		{
			this.logEntry = logEntry;
			this.logEntryTimestamp = logEntryTimestamp;
			this.Index = entryIndex;
			this.isExpanded = isExpanded;

			Vector2 size = transformComponent.sizeDelta;
			if( isExpanded )
			{
				size.y = listView.SelectedItemHeight;

				if( !copyLogButton.gameObject.activeSelf )
				{
					copyLogButton.gameObject.SetActive( true );

					logText.rectTransform.anchoredPosition = new Vector2( logTextOriginalPosition.x, logTextOriginalPosition.y + copyLogButtonHeight * 0.5f );
					logText.rectTransform.sizeDelta = logTextOriginalSize - new Vector2( 0f, copyLogButtonHeight );
				}
			}
			else
			{
				size.y = listView.ItemHeight;

				if( copyLogButton.gameObject.activeSelf )
				{
					copyLogButton.gameObject.SetActive( false );

					logText.rectTransform.anchoredPosition = logTextOriginalPosition;
					logText.rectTransform.sizeDelta = logTextOriginalSize;
				}
			}

			transformComponent.sizeDelta = size;

			SetText( logEntry, logEntryTimestamp, isExpanded );
			logTypeImage.sprite = DebugLogManager.logSpriteRepresentations[(int) logEntry.logType];
		}

		// Show the collapsed count of the debug entry
		public void ShowCount()
		{
			logCountText.SetText( "{0}", logEntry.count );

			if( !logCountParent.activeSelf )
				logCountParent.SetActive( true );
		}

		// Hide the collapsed count of the debug entry
		public void HideCount()
		{
			if( logCountParent.activeSelf )
				logCountParent.SetActive( false );
		}

		// Update the debug entry's displayed timestamp
		public void UpdateTimestamp( DebugLogEntryTimestamp timestamp )
		{
			logEntryTimestamp = timestamp;

			if( isExpanded || listView.manager.alwaysDisplayTimestamps )
				SetText( logEntry, timestamp, isExpanded );
		}

        private void SetText(DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp, bool isExpanded)
        {
            string text = isExpanded ? logEntry.ToString() : logEntry.logString;
            int maxLogLength = isExpanded ? listView.manager.maxExpandedLogLength : listView.manager.maxCollapsedLogLength;

            if (!logEntryTimestamp.HasValue || (!isExpanded && !listView.manager.alwaysDisplayTimestamps))
            {
                if (text.Length <= maxLogLength)
                    logText.text = text;
                else
                {
                    if (listView.manager.textBuffer.Length < maxLogLength)
                        listView.manager.textBuffer = new char[maxLogLength];

                    text.CopyTo(0, listView.manager.textBuffer, 0, maxLogLength);
                    logText.SetText(listView.manager.textBuffer, 0, maxLogLength);
                }
            }
            else
            {
                StringBuilder sb = listView.manager.sharedStringBuilder;
                sb.Length = 0;

                if (isExpanded)
                {
                    logEntryTimestamp.Value.AppendFullTimestamp(sb);
                    sb.Append(": ").Append(text, 0, Mathf.Min(text.Length, maxLogLength - sb.Length));
                }
                else
                {
                    logEntryTimestamp.Value.AppendTime(sb);
                    sb.Append(" ").Append(text, 0, Mathf.Min(text.Length, maxLogLength - sb.Length));
                }

                if (listView.manager.textBuffer.Length < sb.Length)
                    listView.manager.textBuffer = new char[sb.Length];

                sb.CopyTo(0, listView.manager.textBuffer, 0, sb.Length);
                logText.SetText(listView.manager.textBuffer, 0, sb.Length);
            }
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
				listView.OnLogItemClicked( this );
#else
			listView.OnLogItemClicked( this );
#endif
		}

		private void CopyLog()
		{
#if UNITY_EDITOR || !UNITY_WEBGL
			string log = GetCopyContent();
			if( !string.IsNullOrEmpty( log ) )
				GUIUtility.systemCopyBuffer = log;
#endif
		}

		internal string GetCopyContent()
		{
			if( !logEntryTimestamp.HasValue )
				return logEntry.ToString();
			else
			{
				StringBuilder sb = listView.manager.sharedStringBuilder;
				sb.Length = 0;

				logEntryTimestamp.Value.AppendFullTimestamp( sb );
				sb.Append( ": " ).Append( logEntry.ToString() );

				return sb.ToString();
			}
		}

		/// Here, we're using <see cref="TMP_Text.GetRenderedValues(bool)"/> instead of <see cref="TMP_Text.preferredHeight"/> because the latter doesn't take
		/// <see cref="TMP_Text.maxVisibleCharacters"/> into account. However, for <see cref="TMP_Text.GetRenderedValues(bool)"/> to work, we need to give it
		/// enough space (increase log item's height) and let it regenerate its mesh <see cref="TMP_Text.ForceMeshUpdate"/>.
		public float CalculateExpandedHeight( DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp )
		{
			string text = logText.text;
			Vector2 size = ( transform as RectTransform ).sizeDelta;

			( transform as RectTransform ).sizeDelta = new Vector2( size.x, 10000f );
			SetText( logEntry, logEntryTimestamp, true );
			logText.ForceMeshUpdate();
			float result = logText.GetRenderedValues( true ).y + copyLogButtonHeight;

			( transform as RectTransform ).sizeDelta = size;
			logText.text = text;

			return Mathf.Max( listView.ItemHeight, result );
		}

		// Return a string containing complete information about the debug entry
		public override string ToString()
		{
			return logEntry.ToString();
		}
	}
}