using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
using System.Text.RegularExpressions;
#endif

// A UI element to show information about a debug entry
namespace IngameDebugConsole
{
	public class DebugLogItem : MonoBehaviour, IPointerClickHandler
	{
		#region Platform Specific Elements
#if !UNITY_2018_1_OR_NEWER
#if !UNITY_EDITOR && UNITY_ANDROID
		private static AndroidJavaClass m_ajc = null;
		private static AndroidJavaClass AJC
		{
			get
			{
				if( m_ajc == null )
					m_ajc = new AndroidJavaClass( "com.yasirkula.unity.DebugConsole" );

				return m_ajc;
			}
		}

		private static AndroidJavaObject m_context = null;
		private static AndroidJavaObject Context
		{
			get
			{
				if( m_context == null )
				{
					using( AndroidJavaObject unityClass = new AndroidJavaClass( "com.unity3d.player.UnityPlayer" ) )
					{
						m_context = unityClass.GetStatic<AndroidJavaObject>( "currentActivity" );
					}
				}

				return m_context;
			}
		}
#elif !UNITY_EDITOR && UNITY_IOS
		[System.Runtime.InteropServices.DllImport( "__Internal" )]
		private static extern void _DebugConsole_CopyText( string text );
#endif
#endif
		#endregion

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
		private Text logText;
		[SerializeField]
		private Image logTypeImage;

		// Objects related to the collapsed count of the debug entry
		[SerializeField]
		private GameObject logCountParent;
		[SerializeField]
		private Text logCountText;

		[SerializeField]
		private RectTransform copyLogButton;
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
			copyLogButtonHeight = copyLogButton.anchoredPosition.y + copyLogButton.sizeDelta.y + 2f; // 2f: space between text and button

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
				logText.horizontalOverflow = HorizontalWrapMode.Wrap;
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
				logText.horizontalOverflow = HorizontalWrapMode.Overflow;
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
			logCountText.text = logEntry.count.ToString();

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

		private void SetText( DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp, bool isExpanded )
		{
			if( !logEntryTimestamp.HasValue || ( !isExpanded && !listView.manager.alwaysDisplayTimestamps ) )
				logText.text = isExpanded ? logEntry.ToString() : logEntry.logString;
			else
			{
				StringBuilder sb = listView.manager.sharedStringBuilder;
				sb.Length = 0;

				if( isExpanded )
				{
					logEntryTimestamp.Value.AppendFullTimestamp( sb );
					sb.Append( ": " ).Append( logEntry.ToString() );
				}
				else
				{
					logEntryTimestamp.Value.AppendTime( sb );
					sb.Append( " " ).Append( logEntry.logString );
				}

				logText.text = sb.ToString();
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

		public void CopyLog()
		{
#if UNITY_EDITOR || !UNITY_WEBGL
			string log = GetCopyContent();
			if( string.IsNullOrEmpty( log ) )
				return;

#if UNITY_EDITOR || UNITY_2018_1_OR_NEWER || ( !UNITY_ANDROID && !UNITY_IOS )
			GUIUtility.systemCopyBuffer = log;
#elif UNITY_ANDROID
			AJC.CallStatic( "CopyText", Context, log );
#elif UNITY_IOS
			_DebugConsole_CopyText( log );
#endif
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

		public float CalculateExpandedHeight( DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp )
		{
			string text = logText.text;
			HorizontalWrapMode wrapMode = logText.horizontalOverflow;

			SetText( logEntry, logEntryTimestamp, true );
			logText.horizontalOverflow = HorizontalWrapMode.Wrap;

			float result = logText.preferredHeight + copyLogButtonHeight;

			logText.text = text;
			logText.horizontalOverflow = wrapMode;

			return Mathf.Max( listView.ItemHeight, result );
		}

		// Return a string containing complete information about the debug entry
		public override string ToString()
		{
			return logEntry.ToString();
		}
	}
}