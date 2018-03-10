using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// In-game Debug Console / DebugLogManager
// Author: Suleyman Yasir Kula
// 
// Receives debug entries and custom events (e.g. Clear, Collapse, Filter by Type)
// and notifies the recycled list view of changes to the list of debug entries
// 
// - Vocabulary -
// Debug/Log entry: a Debug.Log/LogError/LogWarning/LogException/LogAssertion request made by
//                   the client and intercepted by this manager object
// Debug/Log item: a visual (uGUI) representation of a debug entry
// 
// There can be a lot of debug entries in the system but there will only be a handful of log items 
// to show their properties on screen (these log items are recycled as the list is scrolled)

// An enum to represent filtered log types
namespace IngameDebugConsole
{
	public enum DebugLogFilter
	{
		None = 0,
		Info = 1,
		Warning = 2,
		Error = 4,
		All = 7
	}

	public class DebugLogManager : MonoBehaviour
	{
		private static DebugLogManager instance = null;

		// Debug console will persist between scenes
		[Header( "Properties" )]
		[SerializeField]
		private bool singleton = true;

		// Debug console will be launched in popup mode
		[SerializeField]
		private bool launchInPopupMode = false;

		// Minimum size of the console window
		[SerializeField]
		private float logWindowMinWidth = 250f;
		[SerializeField]
		private float logWindowMinHeight = 200f;

		// Should command input field be cleared after pressing Enter
		[SerializeField]
		private bool clearCommandAfterExecution = true;

		[SerializeField]
		private bool receiveLogcatLogsInAndroid = false;

		[SerializeField]
		private string logcatArguments;

		[Header( "Visuals" )]
		[SerializeField]
		private DebugLogItem logItemPrefab;

		// Visuals for different log types
		[SerializeField]
		private Sprite infoLog;
		[SerializeField]
		private Sprite warningLog;
		[SerializeField]
		private Sprite errorLog;

		private Dictionary<LogType, Sprite> logSpriteRepresentations;

		[SerializeField]
		private Color collapseButtonNormalColor;
		[SerializeField]
		private Color collapseButtonSelectedColor;

		[SerializeField]
		private Color filterButtonsNormalColor;
		[SerializeField]
		private Color filterButtonsSelectedColor;

		[Header( "Internal References" )]
		[SerializeField]
		private RectTransform logWindowTR;

		private Transform canvasTR;

		[SerializeField]
		private RectTransform logItemsContainer;

		[SerializeField]
		private Text clickedLogItemDetails;

		[SerializeField]
		private InputField commandInputField;

		[SerializeField]
		private Image collapseButton;

		[SerializeField]
		private Image filterInfoButton;
		[SerializeField]
		private Image filterWarningButton;
		[SerializeField]
		private Image filterErrorButton;

		[SerializeField]
		private Text infoEntryCountText;
		[SerializeField]
		private Text warningEntryCountText;
		[SerializeField]
		private Text errorEntryCountText;

		// Number of entries filtered by their types
		private int infoEntryCount = 0, warningEntryCount = 0, errorEntryCount = 0;

		// Canvas group to modify visibility of the log window
		[SerializeField]
		private CanvasGroup logWindowCanvasGroup;

		private bool isLogWindowVisible = true;

		[SerializeField]
		private DebugLogPopup popupManager;

		[SerializeField]
		private ScrollRect logItemsScrollRect;
		[SerializeField]
		private ScrollRect clickedLogItemDetailsScrollRect;

		// Recycled list view to handle the log items efficiently
		[SerializeField]
		private DebugLogRecycledListView recycledListView;

		// Filters to apply to the list of debug entries to show
		private bool isCollapseOn = false;
		private DebugLogFilter logFilter = DebugLogFilter.All;

		// If the last log item is completely visible (scrollbar is at the bottom),
		// scrollbar will remain at the bottom when new debug entries are received
		private bool snapToBottom = true;

		// List of unique debug entries (duplicates of entries are not kept)
		private List<DebugLogEntry> collapsedLogEntries;

		// The order the collapsedLogEntries are received 
		// (duplicate entries have the same index (value))
		private List<int> uncollapsedLogEntriesIndices;

		// Filtered list of debug entries to show
		private List<int> indicesOfListEntriesToShow;

		private List<DebugLogItem> pooledLogItems;

		// Last known position of the log window before it was closed
		private Vector3 lastPosition;
		private Vector2 windowDragDeltaPosition;

		private DebugLogLogcatListener logcatListener;

		void OnEnable()
		{
			// Only one instance of debug console is allowed
			if( instance == null )
			{
				instance = this;
				pooledLogItems = new List<DebugLogItem>();

				canvasTR = transform;

				// Associate sprites with log types
				logSpriteRepresentations = new Dictionary<LogType, Sprite>
				{
					{ LogType.Log, infoLog },
					{ LogType.Warning, warningLog },
					{ LogType.Error, errorLog },
					{ LogType.Exception, errorLog },
					{ LogType.Assert, errorLog }
				};

				// Initially, all log types are visible
				filterInfoButton.color = filterButtonsSelectedColor;
				filterWarningButton.color = filterButtonsSelectedColor;
				filterErrorButton.color = filterButtonsSelectedColor;

				// When collapse is disabled and all log types are visible (initial state),
				// the order of the debug entries to show on screen is the same as 
				// the order they were intercepted
				collapsedLogEntries = new List<DebugLogEntry>();
				uncollapsedLogEntriesIndices = new List<int>();
				indicesOfListEntriesToShow = uncollapsedLogEntriesIndices;

				recycledListView.SetLogItemHeight( logItemPrefab.Transform.sizeDelta.y );
				recycledListView.SetCollapsedEntriesList( collapsedLogEntries );
				recycledListView.SetEntryIndicesList( indicesOfListEntriesToShow );

				// If it is a singleton object, don't destroy it between scene changes
				if( singleton )
					DontDestroyOnLoad( gameObject );
			}
			else if( this != instance )
			{
				Destroy( gameObject );
				return;
			}

			// Intercept debug entries
			Application.logMessageReceived -= ReceivedLog;
			Application.logMessageReceived += ReceivedLog;

			if( receiveLogcatLogsInAndroid )
			{
				if( logcatListener == null )
					logcatListener = new DebugLogLogcatListener();

				logcatListener.Start( logcatArguments );
			}

			// Listen for entered commands
			commandInputField.onValidateInput -= OnValidateCommand;
			commandInputField.onValidateInput += OnValidateCommand;

			/*Debug.LogAssertion( "assert" );
			Debug.LogError( "error" );
			Debug.LogException( new System.IO.EndOfStreamException() );
			Debug.LogWarning( "warning" );
			Debug.Log( "log" );*/
		}

		void OnDisable()
		{
			// Stop receiving debug entries
			Application.logMessageReceived -= ReceivedLog;

			if( logcatListener != null )
				logcatListener.Stop();

			// Stop receiving commands
			commandInputField.onValidateInput -= OnValidateCommand;
		}

		void Start()
		{
			if( launchInPopupMode )
			{
				lastPosition = logWindowTR.position;
				popupManager.OnSetVisible();
				popupManager.SwitchFromConsoleToPopup();
			}
		}

		// Command field input is changed, check if command is submitted
		public char OnValidateCommand( string text, int charIndex, char addedChar )
		{
			// If command is submitted
			if( addedChar == '\n' )
			{
				// Clear the command field
				if( clearCommandAfterExecution )
					commandInputField.text = "";

				if( text.Length > 0 )
				{
					// Execute the command
					DebugLogConsole.ExecuteCommand( text );

					// Snap to bottom and select the latest entry
					OnSnapToBottomChanged( true );

					if( indicesOfListEntriesToShow.Count > 0 )
						OnLogClicked( indicesOfListEntriesToShow.Count - 1 );
				}

				return '\0';
			}

			return addedChar;
		}

		// A log item is clicked
		public static void OnLogClicked( int entryIndex )
		{
			// Show stack trace of the debug entry associated with the clicked log item
			instance.clickedLogItemDetails.text = instance.collapsedLogEntries[instance.indicesOfListEntriesToShow[entryIndex]].ToString();

			// Notify recycled list view
			instance.recycledListView.OnLogItemClicked( entryIndex );

			// Move scrollbar of Log Item Details scroll rect to the top
			instance.clickedLogItemDetailsScrollRect.verticalNormalizedPosition = 1f;
		}

		// A debug entry is received
		void ReceivedLog( string logString, string stackTrace, LogType logType )
		{
			DebugLogEntry logEntry = new DebugLogEntry( logString, stackTrace, null );

			// Check if this entry is a duplicate (i.e. has been received before)
			int logEntryIndex = collapsedLogEntries.IndexOf( logEntry );
			bool isEntryInCollapsedEntryList = logEntryIndex != -1;
			if( !isEntryInCollapsedEntryList )
			{
				// It is not a duplicate,
				// add it to the list of unique debug entries
				logEntry.logTypeSpriteRepresentation = logSpriteRepresentations[logType];

				collapsedLogEntries.Add( logEntry );
				logEntryIndex = collapsedLogEntries.Count - 1;
			}
			else
			{
				// It is a duplicate,
				// increment the original debug item's collapsed count
				logEntry = collapsedLogEntries[logEntryIndex];
				logEntry.count++;
			}

			// Add the index of the unique debug entry to the list
			// that stores the order the debug entries are received
			uncollapsedLogEntriesIndices.Add( logEntryIndex );

			// If this debug entry matches the current filters,
			// add it to the list of debug entries to show
			if( ShouldAddEntryToFilteredEntries( logEntry.logTypeSpriteRepresentation, isEntryInCollapsedEntryList ) )
			{
				indicesOfListEntriesToShow.Add( logEntryIndex );
			}

			if( logType == LogType.Log )
			{
				infoEntryCount++;
				infoEntryCountText.text = "" + infoEntryCount;

				// If debug popup is visible, notify it of the new debug entry
				if( !isLogWindowVisible )
					popupManager.NewInfoLogArrived();
			}
			else if( logType == LogType.Warning )
			{
				warningEntryCount++;
				warningEntryCountText.text = "" + warningEntryCount;

				// If debug popup is visible, notify it of the new debug entry
				if( !isLogWindowVisible )
					popupManager.NewWarningLogArrived();
			}
			else
			{
				errorEntryCount++;
				errorEntryCountText.text = "" + errorEntryCount;

				// If debug popup is visible, notify it of the new debug entry
				if( !isLogWindowVisible )
					popupManager.NewErrorLogArrived();
			}

			// If log window is visible, update the recycled list view
			if( isLogWindowVisible )
				recycledListView.OnLogEntriesUpdated();
		}

		// If snapToBottom is enabled, force the scrollbar to the bottom
		void LateUpdate()
		{
			if( snapToBottom )
			{
				logItemsScrollRect.verticalNormalizedPosition = 0f;
			}

			if( logcatListener != null )
			{
				string log;
				while( ( log = logcatListener.GetLog() ) != null )
				{
					ReceivedLog( "LOGCAT: " + log, string.Empty, LogType.Log );
				}
			}
		}

		// Value of snapToBottom is changed (user scrolled the list manually)
		public void OnSnapToBottomChanged( bool snapToBottom )
		{
			this.snapToBottom = snapToBottom;
		}

		// Show the log window
		public void OnSetVisible()
		{
			// Set the position of the window to its last known position
			logWindowTR.position = lastPosition;

			// Update the recycled list view (in case new entries were
			// intercepted while log window was hidden)
			recycledListView.OnLogEntriesUpdated();

			logWindowCanvasGroup.interactable = true;
			logWindowCanvasGroup.blocksRaycasts = true;
			logWindowCanvasGroup.alpha = 1f;

			isLogWindowVisible = true;
		}

		// Hide the log window
		public void OnSetInvisible()
		{
			logWindowCanvasGroup.interactable = false;
			logWindowCanvasGroup.blocksRaycasts = false;
			logWindowCanvasGroup.alpha = 0f;

			isLogWindowVisible = false;
		}

		// Clear button is clicked
		public void ClearButtonPressed()
		{
			snapToBottom = true;

			infoEntryCount = 0;
			warningEntryCount = 0;
			errorEntryCount = 0;

			infoEntryCountText.text = "0";
			warningEntryCountText.text = "0";
			errorEntryCountText.text = "0";

			collapsedLogEntries.Clear();
			uncollapsedLogEntriesIndices.Clear();
			indicesOfListEntriesToShow.Clear();

			recycledListView.OnLogEntriesUpdated();

			// Clear the Selected Log Item Details text
			clickedLogItemDetails.text = "";
		}

		// Collapse button is clicked
		public void CollapseButtonPressed()
		{
			// Swap the value of collapse mode
			isCollapseOn = !isCollapseOn;

			snapToBottom = true;

			if( isCollapseOn )
			{
				collapseButton.color = collapseButtonSelectedColor;
			}
			else
			{
				collapseButton.color = collapseButtonNormalColor;
			}

			recycledListView.SetCollapseMode( isCollapseOn );

			// Determine the new list of debug entries to show
			FilterLogs();
		}

		// Filtering mode of info logs has been changed
		public void FilterLogButtonPressed()
		{
			logFilter = logFilter ^ DebugLogFilter.Info;

			if( ( logFilter & DebugLogFilter.Info ) == DebugLogFilter.Info )
				filterInfoButton.color = filterButtonsSelectedColor;
			else
				filterInfoButton.color = filterButtonsNormalColor;

			FilterLogs();
		}

		// Filtering mode of warning logs has been changed
		public void FilterWarningButtonPressed()
		{
			logFilter = logFilter ^ DebugLogFilter.Warning;

			if( ( logFilter & DebugLogFilter.Warning ) == DebugLogFilter.Warning )
				filterWarningButton.color = filterButtonsSelectedColor;
			else
				filterWarningButton.color = filterButtonsNormalColor;

			FilterLogs();
		}

		// Filtering mode of error logs has been changed
		public void FilterErrorButtonPressed()
		{
			logFilter = logFilter ^ DebugLogFilter.Error;

			if( ( logFilter & DebugLogFilter.Error ) == DebugLogFilter.Error )
				filterErrorButton.color = filterButtonsSelectedColor;
			else
				filterErrorButton.color = filterButtonsNormalColor;

			FilterLogs();
		}

		// Determine the filtered list of debug entries to show on screen
		private void FilterLogs()
		{
			if( logFilter == DebugLogFilter.None )
			{
				// Show no entry
				indicesOfListEntriesToShow = new List<int>();
			}
			else if( logFilter == DebugLogFilter.All )
			{
				if( isCollapseOn )
				{
					// All the unique debug entries will be listed just once.
					// So, list of debug entries to show is the same as the
					// order these unique debug entries are added to collapsedLogEntries
					indicesOfListEntriesToShow = new List<int>( collapsedLogEntries.Count );
					for( int i = 0; i < collapsedLogEntries.Count; i++ )
					{
						indicesOfListEntriesToShow.Add( i );
					}
				}
				else
				{
					// Special (and most common) case: when all log types are enabled 
					// and collapse mode is disabled, list of debug entries to show is 
					// the same as the order all the debug entries are received.
					// So, don't create a new list of indices
					indicesOfListEntriesToShow = uncollapsedLogEntriesIndices;
				}
			}
			else
			{
				// Show only the debug entries that match the current filter
				bool isInfoEnabled = ( logFilter & DebugLogFilter.Info ) == DebugLogFilter.Info;
				bool isWarningEnabled = ( logFilter & DebugLogFilter.Warning ) == DebugLogFilter.Warning;
				bool isErrorEnabled = ( logFilter & DebugLogFilter.Error ) == DebugLogFilter.Error;

				if( isCollapseOn )
				{
					indicesOfListEntriesToShow = new List<int>( collapsedLogEntries.Count );
					for( int i = 0; i < collapsedLogEntries.Count; i++ )
					{
						DebugLogEntry logEntry = collapsedLogEntries[i];
						if( logEntry.logTypeSpriteRepresentation == infoLog && isInfoEnabled )
							indicesOfListEntriesToShow.Add( i );
						else if( logEntry.logTypeSpriteRepresentation == warningLog && isWarningEnabled )
							indicesOfListEntriesToShow.Add( i );
						else if( logEntry.logTypeSpriteRepresentation == errorLog && isErrorEnabled )
							indicesOfListEntriesToShow.Add( i );
					}
				}
				else
				{
					indicesOfListEntriesToShow = new List<int>( uncollapsedLogEntriesIndices.Count );
					for( int i = 0; i < uncollapsedLogEntriesIndices.Count; i++ )
					{
						DebugLogEntry logEntry = collapsedLogEntries[uncollapsedLogEntriesIndices[i]];
						if( logEntry.logTypeSpriteRepresentation == infoLog && isInfoEnabled )
							indicesOfListEntriesToShow.Add( uncollapsedLogEntriesIndices[i] );
						else if( logEntry.logTypeSpriteRepresentation == warningLog && isWarningEnabled )
							indicesOfListEntriesToShow.Add( uncollapsedLogEntriesIndices[i] );
						else if( logEntry.logTypeSpriteRepresentation == errorLog && isErrorEnabled )
							indicesOfListEntriesToShow.Add( uncollapsedLogEntriesIndices[i] );
					}
				}
			}

			// Clear the Selected Log Item Details text
			clickedLogItemDetails.text = "";

			// Update the recycled list view
			recycledListView.SetEntryIndicesList( indicesOfListEntriesToShow );
		}

		// Does this new entry match the current filter
		private bool ShouldAddEntryToFilteredEntries( Sprite logTypeSpriteRepresentation, bool isEntryInCollapsedList )
		{
			if( logFilter == DebugLogFilter.None )
				return false;

			// Special case: if all log types are enabled and collapse mode is disabled, 
			// then don't add the entry to the list of entries to show because 
			// in this case indicesOfListEntriesToShow = uncollapsedLogEntriesIndices and
			// an incoming debug entry is added to uncollapsedLogEntriesIndices, no matter what.
			// So, if we were to add the debug entry to the indicesOfListEntriesToShow explicitly,
			// it would be a duplicate
			if( logFilter == DebugLogFilter.All )
			{
				if( isCollapseOn && !isEntryInCollapsedList )
					return true;

				return false;
			}

			if( ( logTypeSpriteRepresentation == infoLog && ( ( logFilter & DebugLogFilter.Info ) == DebugLogFilter.Info ) ) ||
				( logTypeSpriteRepresentation == warningLog && ( ( logFilter & DebugLogFilter.Warning ) == DebugLogFilter.Warning ) ) ||
				( logTypeSpriteRepresentation == errorLog && ( ( logFilter & DebugLogFilter.Error ) == DebugLogFilter.Error ) ) )
			{
				if( isCollapseOn && isEntryInCollapsedList )
					return false;

				return true;
			}

			return false;
		}

		// Debug window is about to be moved on screen,
		// cache the offset between pointer and the window position
		public void OnWindowDragStarted( BaseEventData dat )
		{
			PointerEventData eventData = (PointerEventData) dat;

			windowDragDeltaPosition = (Vector2) logWindowTR.position - eventData.position;
			lastPosition = logWindowTR.position;

			// Show the popup that the log window can be dropped onto
			popupManager.OnSetVisible();
		}

		// Debug window is being dragged,
		// set the new position of the window
		public void OnWindowDrag( BaseEventData dat )
		{
			PointerEventData eventData = (PointerEventData) dat;

			logWindowTR.position = eventData.position + windowDragDeltaPosition;
		}

		public void OnWindowDragEnded( BaseEventData dat )
		{
			// If log window is not dropped onto the popup, hide the popup
			if( isLogWindowVisible )
			{
				popupManager.OnSetInvisible( false );
			}
		}

		// Debug window is being resized,
		// Set the sizeDelta property of the window accordingly while
		// preventing window dimensions from going below the minimum dimensions
		public void OnWindowResize( BaseEventData dat )
		{
			PointerEventData eventData = (PointerEventData) dat;

			Vector2 newSize = ( eventData.position - (Vector2) logWindowTR.position ) / canvasTR.localScale.x;
			newSize.y = -newSize.y;
			if( newSize.x < logWindowMinWidth ) newSize.x = logWindowMinWidth;
			if( newSize.y < logWindowMinHeight ) newSize.y = logWindowMinHeight;
			logWindowTR.sizeDelta = newSize;

			// Update the recycled list view
			recycledListView.OnViewportDimensionsChanged();
		}

		// Pool an unused log item
		public void PoolLogItem( DebugLogItem logItem )
		{
			logItem.gameObject.SetActive( false );
			pooledLogItems.Add( logItem );
		}


		// Fetch a log item from the pool
		public DebugLogItem UnpoolLogItem()
		{
			DebugLogItem newLogItem;

			// If pool is not empty, fetch a log item from the pool,
			// create a new log item otherwise
			if( pooledLogItems.Count > 0 )
			{
				newLogItem = pooledLogItems[pooledLogItems.Count - 1];
				pooledLogItems.RemoveAt( pooledLogItems.Count - 1 );
				newLogItem.gameObject.SetActive( true );
			}
			else
			{
				newLogItem = Instantiate<DebugLogItem>( logItemPrefab );
				newLogItem.Transform.SetParent( logItemsContainer, false );
			}

			return newLogItem;
		}
	}
}