using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR && UNITY_2021_1_OR_NEWER
using Screen = UnityEngine.Device.Screen; // To support Device Simulator on Unity 2021.1+
#endif

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
		All = ~0
	}

	public enum PopupVisibility
	{
		Always = 0,
		WhenLogReceived = 1,
		Never = 2
	}

	public class DebugLogManager : MonoBehaviour
	{
		public static DebugLogManager Instance { get; private set; }

#pragma warning disable 0649
		[Header( "Properties" )]
		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, console window will persist between scenes (i.e. not be destroyed when scene changes)" )]
		private bool singleton = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Minimum height of the console window" )]
		private float minimumHeight = 200f;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, console window can be resized horizontally, as well" )]
		private bool enableHorizontalResizing = false;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, console window's resize button will be located at bottom-right corner. Otherwise, it will be located at bottom-left corner" )]
		private bool resizeFromRight = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Minimum width of the console window" )]
		private float minimumWidth = 240f;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Opacity of the console window" )]
		[Range( 0f, 1f )]
		private float logWindowOpacity = 1f;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Opacity of the popup" )]
		[Range( 0f, 1f )]
		internal float popupOpacity = 1f;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Determines when the popup will show up (after the console window is closed)" )]
		private PopupVisibility popupVisibility = PopupVisibility.Always;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Determines which log types will show the popup on screen" )]
		private DebugLogFilter popupVisibilityLogFilter = DebugLogFilter.All;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, console window will initially be invisible" )]
		private bool startMinimized = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, pressing the Toggle Key will show/hide (i.e. toggle) the console window at runtime" )]
		private bool toggleWithKey = false;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
		[SerializeField]
		[HideInInspector]
		public InputAction toggleBinding = new InputAction( "Toggle Binding", type: InputActionType.Button, binding: "<Keyboard>/backquote", expectedControlType: "Button" );
#else
		[SerializeField]
		[HideInInspector]
		private KeyCode toggleKey = KeyCode.BackQuote;
#endif

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, the console window will have a searchbar" )]
		private bool enableSearchbar = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Width of the canvas determines whether the searchbar will be located inside the menu bar or underneath the menu bar. This way, the menu bar doesn't get too crowded on narrow screens. This value determines the minimum width of the canvas for the searchbar to appear inside the menu bar" )]
		private float topSearchbarMinWidth = 360f;

        [SerializeField, HideInInspector]
        [Tooltip("If enabled, clicking the resize button of the console window will copy all logs to clipboard. It'll also play a scale animation to give feedback.")]
        internal bool copyAllLogsOnResizeButtonClick;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, the console window will continue receiving logs in the background even if its GameObject is inactive. But the console window's GameObject needs to be activated at least once because its Awake function must be triggered for this to work" )]
		private bool receiveLogsWhileInactive = false;

		[SerializeField]
		[HideInInspector]
		private bool receiveInfoLogs = true, receiveWarningLogs = true, receiveErrorLogs = true, receiveExceptionLogs = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, the arrival times of logs will be recorded and displayed when a log is expanded" )]
		private bool captureLogTimestamps = false;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, timestamps will be displayed for logs even if they aren't expanded" )]
		internal bool alwaysDisplayTimestamps = false;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If the number of logs reach this limit, the oldest log(s) will be deleted to limit the RAM usage. It's recommended to set this value as low as possible" )]
		private int maxLogCount = int.MaxValue;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "How many log(s) to delete when the threshold is reached (all logs are iterated during this operation so it should neither be too low nor too high)" )]
		private int logsToRemoveAfterMaxLogCount = 16;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "While the console window is hidden, incoming logs will be queued but not immediately processed until the console window is opened (to avoid wasting CPU resources). When the log queue exceeds this limit, the first logs in the queue will be processed to enforce this limit. Processed logs won't increase RAM usage if they've been seen before (i.e. collapsible logs) but this is not the case for queued logs, so if a log is spammed every frame, it will fill the whole queue in an instant. Which is why there is a queue limit" )]
		private int queuedLogLimit = 256;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, the command input field at the bottom of the console window will automatically be cleared after entering a command" )]
		private bool clearCommandAfterExecution = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "Console keeps track of the previously entered commands. This value determines the capacity of the command history (you can scroll through the history via up and down arrow keys while the command input field is focused)" )]
		private int commandHistorySize = 15;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, while typing a command, all of the matching commands' signatures will be displayed in a popup" )]
		private bool showCommandSuggestions = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, on Android platform, logcat entries of the application will also be logged to the console with the prefix \"LOGCAT: \". This may come in handy especially if you want to access the native logs of your Android plugins (like Admob)" )]
		private bool receiveLogcatLogsInAndroid = false;

#pragma warning disable 0414
#pragma warning disable 0169
		[SerializeField]
		[HideInInspector]
		[Tooltip( "Native logs will be filtered using these arguments. If left blank, all native logs of the application will be logged to the console. But if you want to e.g. see Admob's logs only, you can enter \"-s Ads\" (without quotes) here" )]
		private string logcatArguments;
#pragma warning restore 0169
#pragma warning restore 0414

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, on Android and iOS devices with notch screens, the console window will be repositioned so that the cutout(s) don't obscure it" )]
		private bool avoidScreenCutout = true;

		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, on Android and iOS devices with notch screens, the console window's popup won't be obscured by the screen cutouts" )]
		internal bool popupAvoidsScreenCutout = false;

        [SerializeField]
        [Tooltip("If a log that isn't expanded is longer than this limit, it will be truncated. This greatly optimizes scrolling speed of collapsed logs if their log messages are long.")]
        internal int maxCollapsedLogLength = 200;

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("maxLogLength")]
        [Tooltip("If an expanded log is longer than this limit, it will be truncated. This optimizes scrolling speed while an expanded log is visible.")]
        internal int maxExpandedLogLength = 10000;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
		[SerializeField]
		[HideInInspector]
		[Tooltip( "If enabled, on standalone platforms, command input field will automatically be focused (start receiving keyboard input) after opening the console window" )]
		private bool autoFocusOnCommandInputField = true;
#endif

		[Header( "Visuals" )]
		[SerializeField]
		private DebugLogItem logItemPrefab;

        [SerializeField]
        internal TMP_FontAsset logItemFontOverride;

		[SerializeField]
		private TextMeshProUGUI commandSuggestionPrefab;

		// Visuals for different log types
		[SerializeField]
		private Sprite infoLog;
		[SerializeField]
		private Sprite warningLog;
		[SerializeField]
		private Sprite errorLog;

		internal static Sprite[] logSpriteRepresentations;

		// Visuals for resize button
		[SerializeField]
		private Sprite resizeIconAllDirections;
		[SerializeField]
		private Sprite resizeIconVerticalOnly;

		[SerializeField]
		private Color collapseButtonNormalColor;
		[SerializeField]
		private Color collapseButtonSelectedColor;

		[SerializeField]
		private Color filterButtonsNormalColor;
		[SerializeField]
		private Color filterButtonsSelectedColor;

		[SerializeField]
		private string commandSuggestionHighlightStart = "<color=orange>";
		[SerializeField]
		private string commandSuggestionHighlightEnd = "</color>";

		[Header( "Internal References" )]
		[SerializeField]
		private RectTransform logWindowTR;

		internal RectTransform canvasTR;

		[SerializeField]
		private RectTransform logItemsContainer;

		[SerializeField]
		private RectTransform commandSuggestionsContainer;

		[SerializeField]
		private TMP_InputField commandInputField;

		[SerializeField]
		private Button hideButton;

		[SerializeField]
		private Button clearButton;

		[SerializeField]
		private Image collapseButton;

		[SerializeField]
		private Image filterInfoButton;
		[SerializeField]
		private Image filterWarningButton;
		[SerializeField]
		private Image filterErrorButton;

		[SerializeField]
		private TextMeshProUGUI infoEntryCountText;
		[SerializeField]
		private TextMeshProUGUI warningEntryCountText;
		[SerializeField]
		private TextMeshProUGUI errorEntryCountText;

		[SerializeField]
		private RectTransform searchbar;
		[SerializeField]
		private RectTransform searchbarSlotTop;
		[SerializeField]
		private RectTransform searchbarSlotBottom;

		[SerializeField]
		private Image resizeButton;

		[SerializeField]
		private GameObject snapToBottomButton;

		// Canvas group to modify visibility of the log window
		[SerializeField]
		private CanvasGroup logWindowCanvasGroup;

		[SerializeField]
		private DebugLogPopup popupManager;

		[SerializeField]
		private ScrollRect logItemsScrollRect;
		private RectTransform logItemsScrollRectTR;
		private Vector2 logItemsScrollRectOriginalSize;

		// Recycled list view to handle the log items efficiently
		[SerializeField]
		private DebugLogRecycledListView recycledListView;
#pragma warning restore 0649

		private bool isLogWindowVisible = true;
		public bool IsLogWindowVisible { get { return isLogWindowVisible; } }

		public bool PopupEnabled
		{
			get { return popupManager.gameObject.activeSelf; }
			set { popupManager.gameObject.SetActive( value ); }
		}

		private bool screenDimensionsChanged = true;
		private float logWindowPreviousWidth;

		// Number of entries filtered by their types
		private int infoEntryCount = 0, warningEntryCount = 0, errorEntryCount = 0;
		private bool entryCountTextsDirty;

		// Number of new entries received this frame
		private int newInfoEntryCount = 0, newWarningEntryCount = 0, newErrorEntryCount = 0;

		// Filters to apply to the list of debug entries to show
		private bool isCollapseOn = false;
		private DebugLogFilter logFilter = DebugLogFilter.All;

		// Search filter
		private string searchTerm;
		private bool isInSearchMode;

		// If the last log item is completely visible (scrollbar is at the bottom),
		// scrollbar will remain at the bottom when new debug entries are received
		[System.NonSerialized]
		public bool SnapToBottom = true;

		// List of unique debug entries (duplicates of entries are not kept)
		private DynamicCircularBuffer<DebugLogEntry> collapsedLogEntries;
		private DynamicCircularBuffer<DebugLogEntryTimestamp> collapsedLogEntriesTimestamps;

		// Dictionary to quickly find if a log already exists in collapsedLogEntries
		private Dictionary<DebugLogEntry, DebugLogEntry> collapsedLogEntriesMap;

		// The order the collapsedLogEntries are received 
		// (duplicate entries have the same value)
		private DynamicCircularBuffer<DebugLogEntry> uncollapsedLogEntries;
		private DynamicCircularBuffer<DebugLogEntryTimestamp> uncollapsedLogEntriesTimestamps;

		// Filtered list of debug entries to show
		private DynamicCircularBuffer<DebugLogEntry> logEntriesToShow;
		private DynamicCircularBuffer<DebugLogEntryTimestamp> timestampsOfLogEntriesToShow;

		// The log entry that must be focused this frame
		private int indexOfLogEntryToSelectAndFocus = -1;

		// Whether or not logs list view should be updated this frame
		private bool shouldUpdateRecycledListView = true;

		// Logs that should be registered in Update-loop
		private DynamicCircularBuffer<QueuedDebugLogEntry> queuedLogEntries;
		private DynamicCircularBuffer<DebugLogEntryTimestamp> queuedLogEntriesTimestamps;
		private object logEntriesLock;
		private int pendingLogToAutoExpand;

		// Command suggestions that match the currently entered command
		private List<TextMeshProUGUI> commandSuggestionInstances;
		private int visibleCommandSuggestionInstances = 0;
		private List<ConsoleMethodInfo> matchingCommandSuggestions;
		private List<int> commandCaretIndexIncrements;
		private string commandInputFieldPrevCommand;
		private string commandInputFieldPrevCommandName;
		private int commandInputFieldPrevParamCount = -1;
		private int commandInputFieldPrevCaretPos = -1;
		private int commandInputFieldPrevCaretArgumentIndex = -1;

		// Value of the command input field when autocomplete was first requested
		private string commandInputFieldAutoCompleteBase;
		private bool commandInputFieldAutoCompletedNow;

		// Pools for memory efficiency
		private Stack<DebugLogEntry> pooledLogEntries;
		private Stack<DebugLogItem> pooledLogItems;

		/// Variables used by <see cref="RemoveOldestLogs"/>
		private bool anyCollapsedLogRemoved;
		private int removedLogEntriesToShowCount;

		// History of the previously entered commands
		private CircularBuffer<string> commandHistory;
		private int commandHistoryIndex = -1;
		private string unfinishedCommand;

		// StringBuilder used by various functions
		internal StringBuilder sharedStringBuilder;

        /// <summary>
        /// Used for <see cref="TMP_Text.SetText(char[])"/>.
        /// </summary>
        [System.NonSerialized]
        internal char[] textBuffer = new char[4096];

		// Offset of DateTime.Now from DateTime.UtcNow
		private System.TimeSpan localTimeUtcOffset;

		// Last recorded values of Time.realtimeSinceStartup and Time.frameCount on the main thread (because these Time properties can't be accessed from other threads)
#if !IDG_OMIT_ELAPSED_TIME
		private float lastElapsedSeconds;
#endif
#if !IDG_OMIT_FRAMECOUNT
		private int lastFrameCount;
#endif

		private DebugLogEntryTimestamp dummyLogEntryTimestamp;

		// Required in ValidateScrollPosition() function
		private PointerEventData nullPointerEventData;

		private System.Action<DebugLogEntry> poolLogEntryAction;
		private System.Action<DebugLogEntry> removeUncollapsedLogEntryAction;
		private System.Predicate<DebugLogEntry> shouldRemoveCollapsedLogEntryPredicate;
		private System.Predicate<DebugLogEntry> shouldRemoveLogEntryToShowPredicate;
		private System.Action<DebugLogEntry, int> updateLogEntryCollapsedIndexAction;

		// Callbacks for log window show/hide events
		public System.Action OnLogWindowShown, OnLogWindowHidden;

		private bool isQuittingApplication;

#if !UNITY_EDITOR && UNITY_ANDROID && UNITY_ANDROID_JNI
		private DebugLogLogcatListener logcatListener;
#endif

		private void Awake()
		{
			// Only one instance of debug console is allowed
			if( !Instance )
			{
				Instance = this;

				// If it is a singleton object, don't destroy it between scene changes
				if( singleton )
					DontDestroyOnLoad( gameObject );
			}
			else if( Instance != this )
			{
				Destroy( gameObject );
				return;
			}

			pooledLogEntries = new Stack<DebugLogEntry>( 64 );
			pooledLogItems = new Stack<DebugLogItem>( 16 );
			commandSuggestionInstances = new List<TextMeshProUGUI>( 8 );
			matchingCommandSuggestions = new List<ConsoleMethodInfo>( 8 );
			commandCaretIndexIncrements = new List<int>( 8 );
			queuedLogEntries = new DynamicCircularBuffer<QueuedDebugLogEntry>( Mathf.Clamp( queuedLogLimit, 16, 4096 ) );
			commandHistory = new CircularBuffer<string>( commandHistorySize );

			logEntriesLock = new object();
			sharedStringBuilder = new StringBuilder( 1024 );

			canvasTR = (RectTransform) transform;
			logItemsScrollRectTR = (RectTransform) logItemsScrollRect.transform;
			logItemsScrollRectOriginalSize = logItemsScrollRectTR.sizeDelta;

			// Associate sprites with log types
			logSpriteRepresentations = new Sprite[5];
			logSpriteRepresentations[(int) LogType.Log] = infoLog;
			logSpriteRepresentations[(int) LogType.Warning] = warningLog;
			logSpriteRepresentations[(int) LogType.Error] = errorLog;
			logSpriteRepresentations[(int) LogType.Exception] = errorLog;
			logSpriteRepresentations[(int) LogType.Assert] = errorLog;

			// Initially, all log types are visible
			filterInfoButton.color = filterButtonsSelectedColor;
			filterWarningButton.color = filterButtonsSelectedColor;
			filterErrorButton.color = filterButtonsSelectedColor;

			resizeButton.sprite = enableHorizontalResizing ? resizeIconAllDirections : resizeIconVerticalOnly;

			collapsedLogEntries = new DynamicCircularBuffer<DebugLogEntry>( 128 );
			collapsedLogEntriesMap = new Dictionary<DebugLogEntry, DebugLogEntry>( 128, new DebugLogEntryContentEqualityComparer() );
			uncollapsedLogEntries = new DynamicCircularBuffer<DebugLogEntry>( 256 );
			logEntriesToShow = new DynamicCircularBuffer<DebugLogEntry>( 256 );

			if( captureLogTimestamps )
			{
				collapsedLogEntriesTimestamps = new DynamicCircularBuffer<DebugLogEntryTimestamp>( 128 );
				uncollapsedLogEntriesTimestamps = new DynamicCircularBuffer<DebugLogEntryTimestamp>( 256 );
				timestampsOfLogEntriesToShow = new DynamicCircularBuffer<DebugLogEntryTimestamp>( 256 );
				queuedLogEntriesTimestamps = new DynamicCircularBuffer<DebugLogEntryTimestamp>( queuedLogEntries.Capacity );
			}

			recycledListView.Initialize( this, logEntriesToShow, timestampsOfLogEntriesToShow, logItemPrefab.Transform.sizeDelta.y );

			if( minimumWidth < 100f )
				minimumWidth = 100f;
			if( minimumHeight < 200f )
				minimumHeight = 200f;

			if( !resizeFromRight )
			{
				RectTransform resizeButtonTR = (RectTransform) resizeButton.GetComponentInParent<DebugLogResizeListener>().transform;
				resizeButtonTR.anchorMin = new Vector2( 0f, resizeButtonTR.anchorMin.y );
				resizeButtonTR.anchorMax = new Vector2( 0f, resizeButtonTR.anchorMax.y );
				resizeButtonTR.pivot = new Vector2( 0f, resizeButtonTR.pivot.y );

				( (RectTransform) commandInputField.transform ).anchoredPosition += new Vector2( resizeButtonTR.sizeDelta.x, 0f );
			}

			if( enableSearchbar )
				searchbar.GetComponent<TMP_InputField>().onValueChanged.AddListener( SearchTermChanged );
			else
			{
				searchbar = null;
				searchbarSlotTop.gameObject.SetActive( false );
				searchbarSlotBottom.gameObject.SetActive( false );
			}

			filterInfoButton.gameObject.SetActive( receiveInfoLogs );
			filterWarningButton.gameObject.SetActive( receiveWarningLogs );
			filterErrorButton.gameObject.SetActive( receiveErrorLogs || receiveExceptionLogs );

			if( commandSuggestionsContainer.gameObject.activeSelf )
				commandSuggestionsContainer.gameObject.SetActive( false );

			// Register to UI events
			commandInputField.onValidateInput += OnValidateCommand;
			commandInputField.onValueChanged.AddListener( OnEditCommand );
			commandInputField.onEndEdit.AddListener( OnEndEditCommand );
			hideButton.onClick.AddListener( HideLogWindow );
			clearButton.onClick.AddListener( ClearLogs );
			collapseButton.GetComponent<Button>().onClick.AddListener( CollapseButtonPressed );
			filterInfoButton.GetComponent<Button>().onClick.AddListener( FilterLogButtonPressed );
			filterWarningButton.GetComponent<Button>().onClick.AddListener( FilterWarningButtonPressed );
			filterErrorButton.GetComponent<Button>().onClick.AddListener( FilterErrorButtonPressed );
			snapToBottomButton.GetComponent<Button>().onClick.AddListener( () => SnapToBottom = true );

			localTimeUtcOffset = System.DateTime.Now - System.DateTime.UtcNow;
			dummyLogEntryTimestamp = new DebugLogEntryTimestamp();
			nullPointerEventData = new PointerEventData( null );

			poolLogEntryAction = PoolLogEntry;
			removeUncollapsedLogEntryAction = RemoveUncollapsedLogEntry;
			shouldRemoveCollapsedLogEntryPredicate = ShouldRemoveCollapsedLogEntry;
			shouldRemoveLogEntryToShowPredicate = ShouldRemoveLogEntryToShow;
			updateLogEntryCollapsedIndexAction = UpdateLogEntryCollapsedIndex;

			if( receiveLogsWhileInactive )
			{
				Application.logMessageReceivedThreaded -= ReceivedLog;
				Application.logMessageReceivedThreaded += ReceivedLog;
			}

			// OnApplicationQuit isn't reliable on some Unity versions when Application.wantsToQuit is used; Application.quitting is the only reliable solution on those versions
			// https://issuetracker.unity3d.com/issues/onapplicationquit-method-is-called-before-application-dot-wantstoquit-event-is-raised
			Application.quitting += OnApplicationQuitting;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			toggleBinding.performed += ( context ) =>
			{
				if( toggleWithKey )
				{
					if( isLogWindowVisible )
						HideLogWindow();
					else
						ShowLogWindow();
				}
			};

			// On new Input System, scroll sensitivity is much higher than legacy Input system
			logItemsScrollRect.scrollSensitivity *= 0.25f;
#endif
		}

		private void OnEnable()
		{
			if( Instance != this )
				return;

			if( !receiveLogsWhileInactive )
			{
				Application.logMessageReceivedThreaded -= ReceivedLog;
				Application.logMessageReceivedThreaded += ReceivedLog;
			}

			if( receiveLogcatLogsInAndroid )
			{
#if UNITY_ANDROID
#if UNITY_ANDROID_JNI
#if !UNITY_EDITOR
				if( logcatListener == null )
					logcatListener = new DebugLogLogcatListener();

				logcatListener.Start( logcatArguments );
#endif
#else
				Debug.LogWarning( "Android JNI module must be enabled in Package Manager for \"Receive Logcat Logs In Android\" to work." );
#endif
#endif
			}

#if IDG_ENABLE_HELPER_COMMANDS || IDG_ENABLE_LOGS_SAVE_COMMAND
			DebugLogConsole.AddCommand( "logs.save", "Saves logs to persistentDataPath", SaveLogsToFile );
			DebugLogConsole.AddCommand<string>( "logs.save", "Saves logs to the specified file", SaveLogsToFile );
#endif

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			if( toggleWithKey )
				toggleBinding.Enable();
#endif

			//Debug.LogAssertion( "assert" );
			//Debug.LogError( "error" );
			//Debug.LogException( new System.IO.EndOfStreamException() );
			//Debug.LogWarning( "warning" );
			//Debug.Log( "log" );
		}

		private void OnDisable()
		{
			if( Instance != this )
				return;

			if( !receiveLogsWhileInactive )
				Application.logMessageReceivedThreaded -= ReceivedLog;

#if !UNITY_EDITOR && UNITY_ANDROID && UNITY_ANDROID_JNI
			if( logcatListener != null )
				logcatListener.Stop();
#endif

			DebugLogConsole.RemoveCommand( "logs.save" );

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			if( toggleBinding.enabled )
				toggleBinding.Disable();
#endif
		}

		private void Start()
		{
			if( startMinimized )
			{
				HideLogWindow();

				if( popupVisibility != PopupVisibility.Always )
					popupManager.Hide();
			}
			else
				ShowLogWindow();

			PopupEnabled = ( popupVisibility != PopupVisibility.Never );
		}

		private void OnDestroy()
		{
			if( Instance == this )
				Instance = null;

			if( receiveLogsWhileInactive )
				Application.logMessageReceivedThreaded -= ReceivedLog;

			Application.quitting -= OnApplicationQuitting;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			maxLogCount = Mathf.Max( 2, maxLogCount );
			logsToRemoveAfterMaxLogCount = Mathf.Max( 1, logsToRemoveAfterMaxLogCount );
			queuedLogLimit = Mathf.Max( 0, queuedLogLimit );

			if( UnityEditor.EditorApplication.isPlaying )
			{
				resizeButton.sprite = enableHorizontalResizing ? resizeIconAllDirections : resizeIconVerticalOnly;

				filterInfoButton.gameObject.SetActive( receiveInfoLogs );
				filterWarningButton.gameObject.SetActive( receiveWarningLogs );
				filterErrorButton.gameObject.SetActive( receiveErrorLogs || receiveExceptionLogs );
			}
		}
#endif

		private void OnApplicationQuitting()
		{
			isQuittingApplication = true;
		}

		// Window is resized, update the list
		private void OnRectTransformDimensionsChange()
		{
			screenDimensionsChanged = true;
		}

		private void Update()
		{
#if !IDG_OMIT_ELAPSED_TIME
			lastElapsedSeconds = Time.realtimeSinceStartup;
#endif
#if !IDG_OMIT_FRAMECOUNT
			lastFrameCount = Time.frameCount;
#endif

#if !UNITY_EDITOR && UNITY_ANDROID && UNITY_ANDROID_JNI
			if( logcatListener != null )
			{
				string log;
				while( ( log = logcatListener.GetLog() ) != null )
					ReceivedLog( "LOGCAT: " + log, string.Empty, LogType.Log );
			}
#endif

#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
			// Toggling the console with toggleKey is handled in Update instead of LateUpdate because
			// when we hide the console, we don't want the commandInputField to capture the toggleKey.
			// InputField captures input in LateUpdate so deactivating it in Update ensures that
			// no further input is captured
			if( toggleWithKey )
			{
				if( Input.GetKeyDown( toggleKey ) )
				{
					if( isLogWindowVisible )
						HideLogWindow();
					else
						ShowLogWindow();
				}
			}
#endif
		}

		private void LateUpdate()
		{
			if( isQuittingApplication )
				return;

			int numberOfLogsToProcess = isLogWindowVisible ? queuedLogEntries.Count : ( queuedLogEntries.Count - queuedLogLimit );
			ProcessQueuedLogs( numberOfLogsToProcess );

			if( uncollapsedLogEntries.Count >= maxLogCount )
			{
				/// If log window isn't visible, remove the logs over time (i.e. don't remove more than <see cref="logsToRemoveAfterMaxLogCount"/>) to avoid performance issues.
				int numberOfLogsToRemove = Mathf.Min( !isLogWindowVisible ? logsToRemoveAfterMaxLogCount : ( uncollapsedLogEntries.Count - maxLogCount + logsToRemoveAfterMaxLogCount ), uncollapsedLogEntries.Count );
				RemoveOldestLogs( numberOfLogsToRemove );
			}

			// Don't perform CPU heavy tasks if neither the log window nor the popup is visible
			if( !isLogWindowVisible && !PopupEnabled )
				return;

			int newInfoEntryCount, newWarningEntryCount, newErrorEntryCount;
			lock( logEntriesLock )
			{
				newInfoEntryCount = this.newInfoEntryCount;
				newWarningEntryCount = this.newWarningEntryCount;
				newErrorEntryCount = this.newErrorEntryCount;

				this.newInfoEntryCount = 0;
				this.newWarningEntryCount = 0;
				this.newErrorEntryCount = 0;
			}

			// Update entry count texts in a single batch
			if( newInfoEntryCount > 0 || newWarningEntryCount > 0 || newErrorEntryCount > 0 )
			{
				if( newInfoEntryCount > 0 )
				{
					infoEntryCount += newInfoEntryCount;
					if( isLogWindowVisible )
						infoEntryCountText.text = infoEntryCount.ToString();
				}

				if( newWarningEntryCount > 0 )
				{
					warningEntryCount += newWarningEntryCount;
					if( isLogWindowVisible )
						warningEntryCountText.text = warningEntryCount.ToString();
				}

				if( newErrorEntryCount > 0 )
				{
					errorEntryCount += newErrorEntryCount;
					if( isLogWindowVisible )
						errorEntryCountText.text = errorEntryCount.ToString();
				}

				// If debug popup is visible, notify it of the new debug entries
				if( !isLogWindowVisible )
				{
					entryCountTextsDirty = true;

					if( popupVisibility == PopupVisibility.WhenLogReceived && !popupManager.IsVisible )
					{
						if( ( newInfoEntryCount > 0 && ( popupVisibilityLogFilter & DebugLogFilter.Info ) == DebugLogFilter.Info ) ||
							( newWarningEntryCount > 0 && ( popupVisibilityLogFilter & DebugLogFilter.Warning ) == DebugLogFilter.Warning ) ||
							( newErrorEntryCount > 0 && ( popupVisibilityLogFilter & DebugLogFilter.Error ) == DebugLogFilter.Error ) )
						{
							popupManager.Show();
						}
					}

					if( popupManager.IsVisible )
						popupManager.NewLogsArrived( newInfoEntryCount, newWarningEntryCount, newErrorEntryCount );
				}
			}

			if( isLogWindowVisible )
			{
				// Update visible logs if necessary
				if( shouldUpdateRecycledListView )
					OnLogEntriesUpdated( false, false );

				// Automatically expand the target log (if any)
				if( indexOfLogEntryToSelectAndFocus >= 0 )
				{
					if( indexOfLogEntryToSelectAndFocus < logEntriesToShow.Count )
						recycledListView.SelectAndFocusOnLogItemAtIndex( indexOfLogEntryToSelectAndFocus );

					indexOfLogEntryToSelectAndFocus = -1;
				}

				if( entryCountTextsDirty )
				{
					infoEntryCountText.text = infoEntryCount.ToString();
					warningEntryCountText.text = warningEntryCount.ToString();
					errorEntryCountText.text = errorEntryCount.ToString();

					entryCountTextsDirty = false;
				}

				float logWindowWidth = logWindowTR.rect.width;
				if( !Mathf.Approximately( logWindowWidth, logWindowPreviousWidth ) )
				{
					logWindowPreviousWidth = logWindowWidth;

					if( searchbar )
					{
						if( logWindowWidth >= topSearchbarMinWidth )
						{
							if( searchbar.parent == searchbarSlotBottom )
							{
								searchbarSlotTop.gameObject.SetActive( true );
								searchbar.SetParent( searchbarSlotTop, false );
								searchbarSlotBottom.gameObject.SetActive( false );

								logItemsScrollRectTR.anchoredPosition = Vector2.zero;
								logItemsScrollRectTR.sizeDelta = logItemsScrollRectOriginalSize;
							}
						}
						else
						{
							if( searchbar.parent == searchbarSlotTop )
							{
								searchbarSlotBottom.gameObject.SetActive( true );
								searchbar.SetParent( searchbarSlotBottom, false );
								searchbarSlotTop.gameObject.SetActive( false );

								float searchbarHeight = searchbarSlotBottom.sizeDelta.y;
								logItemsScrollRectTR.anchoredPosition = new Vector2( 0f, searchbarHeight * -0.5f );
								logItemsScrollRectTR.sizeDelta = logItemsScrollRectOriginalSize - new Vector2( 0f, searchbarHeight );
							}
						}
					}

					recycledListView.OnViewportWidthChanged();
				}

				// If SnapToBottom is enabled, force the scrollbar to the bottom
				if( SnapToBottom )
				{
					logItemsScrollRect.verticalNormalizedPosition = 0f;

					if( snapToBottomButton.activeSelf )
						snapToBottomButton.SetActive( false );
				}
				else
				{
					float scrollPos = logItemsScrollRect.verticalNormalizedPosition;
					if( snapToBottomButton.activeSelf != ( scrollPos > 1E-6f && scrollPos < 0.9999f ) )
						snapToBottomButton.SetActive( !snapToBottomButton.activeSelf );
				}

				if( showCommandSuggestions && commandInputField.isFocused && commandInputField.caretPosition != commandInputFieldPrevCaretPos )
					RefreshCommandSuggestions( commandInputField.text );

				if( commandInputField.isFocused && commandHistory.Count > 0 )
				{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
					if( Keyboard.current != null )
#endif
					{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
						if( Keyboard.current[Key.UpArrow].wasPressedThisFrame )
#else
						if( Input.GetKeyDown( KeyCode.UpArrow ) )
#endif
						{
							if( commandHistoryIndex == -1 )
							{
								commandHistoryIndex = commandHistory.Count - 1;
								unfinishedCommand = commandInputField.text;
							}
							else if( --commandHistoryIndex < 0 )
								commandHistoryIndex = 0;

							commandInputField.text = commandHistory[commandHistoryIndex];
							commandInputField.caretPosition = commandInputField.text.Length;
						}
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
						else if( Keyboard.current[Key.DownArrow].wasPressedThisFrame && commandHistoryIndex != -1 )
#else
						else if( Input.GetKeyDown( KeyCode.DownArrow ) && commandHistoryIndex != -1 )
#endif
						{
							if( ++commandHistoryIndex < commandHistory.Count )
								commandInputField.text = commandHistory[commandHistoryIndex];
							else
							{
								commandHistoryIndex = -1;
								commandInputField.text = unfinishedCommand ?? string.Empty;
							}
						}
					}
				}
			}

			if( screenDimensionsChanged )
			{
				// Update the recycled list view
				if( isLogWindowVisible )
					recycledListView.OnViewportHeightChanged();
				else
					popupManager.UpdatePosition( true );

#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
				CheckScreenCutout();
#endif

				screenDimensionsChanged = false;
			}
		}

		public void ShowLogWindow()
		{
			// Show the log window
			logWindowCanvasGroup.blocksRaycasts = true;
			logWindowCanvasGroup.alpha = logWindowOpacity;

			popupManager.Hide();

			// Update the recycled list view 
			// (in case new entries were intercepted while log window was hidden)
			OnLogEntriesUpdated( true, true );

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
			// Focus on the command input field on standalone platforms when the console is opened
			if( autoFocusOnCommandInputField )
				StartCoroutine( ActivateCommandInputFieldCoroutine() );
#endif

			isLogWindowVisible = true;

			if( OnLogWindowShown != null )
				OnLogWindowShown();
		}

		public void HideLogWindow()
		{
			// Hide the log window
			logWindowCanvasGroup.blocksRaycasts = false;
			logWindowCanvasGroup.alpha = 0f;

			if( commandInputField.isFocused )
				commandInputField.DeactivateInputField();

			if( popupVisibility == PopupVisibility.Always )
				popupManager.Show();

			isLogWindowVisible = false;

			// Deselect the currently selected UI object (if any) when the log window is hidden to avoid edge cases: https://github.com/yasirkula/UnityIngameDebugConsole/pull/85
			if( EventSystem.current != null )
				EventSystem.current.SetSelectedGameObject( null );

			if( OnLogWindowHidden != null )
				OnLogWindowHidden();
		}

		// Command field input is changed, check if command is submitted
		private char OnValidateCommand( string text, int charIndex, char addedChar )
		{
			if( addedChar == '\t' ) // Autocomplete attempt
			{
				if( !string.IsNullOrEmpty( text ) )
				{
					if( string.IsNullOrEmpty( commandInputFieldAutoCompleteBase ) )
						commandInputFieldAutoCompleteBase = text;

					string autoCompletedCommand = DebugLogConsole.GetAutoCompleteCommand( commandInputFieldAutoCompleteBase, text );
					if( !string.IsNullOrEmpty( autoCompletedCommand ) && autoCompletedCommand != text )
					{
						commandInputFieldAutoCompletedNow = true;
						commandInputField.text = autoCompletedCommand;
						commandInputField.stringPosition = autoCompletedCommand.Length;
					}
				}

				return '\0';
			}
			else if( addedChar == '\n' ) // Command is submitted
			{
				// Clear the command field
				if( clearCommandAfterExecution )
					commandInputField.text = string.Empty;

				if( text.Length > 0 )
				{
					if( commandHistory.Count == 0 || commandHistory[commandHistory.Count - 1] != text )
						commandHistory.Add( text );

					commandHistoryIndex = -1;
					unfinishedCommand = null;

					// Execute the command
					DebugLogConsole.ExecuteCommand( text );

					// Snap to bottom and select the latest entry
					SnapToBottom = true;
				}

				return '\0';
			}

			return addedChar;
		}

		// A debug entry is received
		public void ReceivedLog( string logString, string stackTrace, LogType logType )
		{
			if( isQuittingApplication )
				return;

			switch( logType )
			{
				case LogType.Log: if( !receiveInfoLogs ) return; break;
				case LogType.Warning: if( !receiveWarningLogs ) return; break;
				case LogType.Error: if( !receiveErrorLogs ) return; break;
				case LogType.Assert:
				case LogType.Exception: if( !receiveExceptionLogs ) return; break;
			}

			QueuedDebugLogEntry queuedLogEntry = new QueuedDebugLogEntry( logString, stackTrace, logType );
			DebugLogEntryTimestamp queuedLogEntryTimestamp;
			if( queuedLogEntriesTimestamps != null )
			{
				// It is 10 times faster to cache local time's offset from UtcNow and add it to UtcNow to get local time at any time
				System.DateTime dateTime = System.DateTime.UtcNow + localTimeUtcOffset;
#if !IDG_OMIT_ELAPSED_TIME && !IDG_OMIT_FRAMECOUNT
				queuedLogEntryTimestamp = new DebugLogEntryTimestamp( dateTime, lastElapsedSeconds, lastFrameCount );
#elif !IDG_OMIT_ELAPSED_TIME
				queuedLogEntryTimestamp = new DebugLogEntryTimestamp( dateTime, lastElapsedSeconds );
#elif !IDG_OMIT_FRAMECOUNT
				queuedLogEntryTimestamp = new DebugLogEntryTimestamp( dateTime, lastFrameCount );
#else
				queuedLogEntryTimestamp = new DebugLogEntryTimestamp( dateTime );
#endif
			}
			else
				queuedLogEntryTimestamp = dummyLogEntryTimestamp;

			lock( logEntriesLock )
			{
				/// Enforce <see cref="maxLogCount"/> in queued logs, as well. That's because when it's exceeded, the oldest queued logs will
				/// be removed by <see cref="RemoveOldestLogs"/> immediately after they're processed anyways (i.e. waste of CPU and RAM).
				if( queuedLogEntries.Count + 1 >= maxLogCount )
				{
					LogType removedLogType = queuedLogEntries.RemoveFirst().logType;
					if( removedLogType == LogType.Log )
						newInfoEntryCount--;
					else if( removedLogType == LogType.Warning )
						newWarningEntryCount--;
					else
						newErrorEntryCount--;

					if( queuedLogEntriesTimestamps != null )
						queuedLogEntriesTimestamps.RemoveFirst();
				}

				queuedLogEntries.Add( queuedLogEntry );

				if( queuedLogEntriesTimestamps != null )
					queuedLogEntriesTimestamps.Add( queuedLogEntryTimestamp );

				if( logType == LogType.Log )
					newInfoEntryCount++;
				else if( logType == LogType.Warning )
					newWarningEntryCount++;
				else
					newErrorEntryCount++;
			}
		}

		// Process a number of logs waiting in the pending logs queue
		private void ProcessQueuedLogs( int numberOfLogsToProcess )
		{
			for( int i = 0; i < numberOfLogsToProcess; i++ )
			{
				QueuedDebugLogEntry logEntry;
				DebugLogEntryTimestamp timestamp;
				lock( logEntriesLock )
				{
					logEntry = queuedLogEntries.RemoveFirst();
					timestamp = queuedLogEntriesTimestamps != null ? queuedLogEntriesTimestamps.RemoveFirst() : dummyLogEntryTimestamp;
				}

				ProcessLog( logEntry, timestamp );
			}
		}

		// Present the log entry in the console
		private void ProcessLog( QueuedDebugLogEntry queuedLogEntry, DebugLogEntryTimestamp timestamp )
		{
			LogType logType = queuedLogEntry.logType;
			DebugLogEntry logEntry;
			if( pooledLogEntries.Count > 0 )
				logEntry = pooledLogEntries.Pop();
			else
				logEntry = new DebugLogEntry();

			logEntry.Initialize( queuedLogEntry.logString, queuedLogEntry.stackTrace );

			// Check if this entry is a duplicate (i.e. has been received before)
			DebugLogEntry existingLogEntry;
			bool isEntryInCollapsedEntryList = collapsedLogEntriesMap.TryGetValue( logEntry, out existingLogEntry );
			if( !isEntryInCollapsedEntryList )
			{
				// It is not a duplicate,
				// add it to the list of unique debug entries
				logEntry.logType = logType;
				logEntry.collapsedIndex = collapsedLogEntries.Count;

				collapsedLogEntries.Add( logEntry );
				collapsedLogEntriesMap[logEntry] = logEntry;

				if( collapsedLogEntriesTimestamps != null )
					collapsedLogEntriesTimestamps.Add( timestamp );
			}
			else
			{
				// It is a duplicate, pool the duplicate log entry and
				// increment the original debug item's collapsed count
				PoolLogEntry( logEntry );

				logEntry = existingLogEntry;
				logEntry.count++;

				if( collapsedLogEntriesTimestamps != null )
					collapsedLogEntriesTimestamps[logEntry.collapsedIndex] = timestamp;
			}

			uncollapsedLogEntries.Add( logEntry );

			if( uncollapsedLogEntriesTimestamps != null )
				uncollapsedLogEntriesTimestamps.Add( timestamp );

			// If this debug entry matches the current filters,
			// add it to the list of debug entries to show
			int logEntryIndexInEntriesToShow = -1;
			if( isCollapseOn && isEntryInCollapsedEntryList )
			{
				if( isLogWindowVisible || timestampsOfLogEntriesToShow != null )
				{
					if( !isInSearchMode && logFilter == DebugLogFilter.All )
						logEntryIndexInEntriesToShow = logEntry.collapsedIndex;
					else
						logEntryIndexInEntriesToShow = logEntriesToShow.IndexOf( logEntry );

					if( logEntryIndexInEntriesToShow >= 0 )
					{
						if( timestampsOfLogEntriesToShow != null )
							timestampsOfLogEntriesToShow[logEntryIndexInEntriesToShow] = timestamp;

						if( isLogWindowVisible )
							recycledListView.OnCollapsedLogEntryAtIndexUpdated( logEntryIndexInEntriesToShow );
					}
				}
			}
			else if( ( !isInSearchMode || queuedLogEntry.MatchesSearchTerm( searchTerm ) ) && ( logFilter == DebugLogFilter.All ||
			   ( logType == LogType.Log && ( ( logFilter & DebugLogFilter.Info ) == DebugLogFilter.Info ) ) ||
			   ( logType == LogType.Warning && ( ( logFilter & DebugLogFilter.Warning ) == DebugLogFilter.Warning ) ) ||
			   ( logType != LogType.Log && logType != LogType.Warning && ( ( logFilter & DebugLogFilter.Error ) == DebugLogFilter.Error ) ) ) )
			{
				logEntriesToShow.Add( logEntry );
				logEntryIndexInEntriesToShow = logEntriesToShow.Count - 1;

				if( timestampsOfLogEntriesToShow != null )
					timestampsOfLogEntriesToShow.Add( timestamp );

				shouldUpdateRecycledListView = true;
			}

			// Automatically expand this log if necessary
			if( pendingLogToAutoExpand > 0 && --pendingLogToAutoExpand <= 0 && logEntryIndexInEntriesToShow >= 0 )
				indexOfLogEntryToSelectAndFocus = logEntryIndexInEntriesToShow;
		}

		private void RemoveOldestLogs( int numberOfLogsToRemove )
		{
			if( numberOfLogsToRemove <= 0 )
				return;

			DebugLogEntry logEntryToSelectAndFocus = ( indexOfLogEntryToSelectAndFocus >= 0 && indexOfLogEntryToSelectAndFocus < logEntriesToShow.Count ) ? logEntriesToShow[indexOfLogEntryToSelectAndFocus] : null;

			anyCollapsedLogRemoved = false;
			removedLogEntriesToShowCount = 0;

			uncollapsedLogEntries.TrimStart( numberOfLogsToRemove, removeUncollapsedLogEntryAction );

			if( uncollapsedLogEntriesTimestamps != null )
				uncollapsedLogEntriesTimestamps.TrimStart( numberOfLogsToRemove );

			if( removedLogEntriesToShowCount > 0 )
			{
				logEntriesToShow.TrimStart( removedLogEntriesToShowCount );

				if( timestampsOfLogEntriesToShow != null )
					timestampsOfLogEntriesToShow.TrimStart( removedLogEntriesToShowCount );
			}

			if( anyCollapsedLogRemoved )
			{
				collapsedLogEntries.RemoveAll( shouldRemoveCollapsedLogEntryPredicate, updateLogEntryCollapsedIndexAction, collapsedLogEntriesTimestamps );

				if( isCollapseOn )
					removedLogEntriesToShowCount = logEntriesToShow.RemoveAll( shouldRemoveLogEntryToShowPredicate, null, timestampsOfLogEntriesToShow );
			}

			if( removedLogEntriesToShowCount > 0 )
			{
				if( logEntryToSelectAndFocus == null || logEntryToSelectAndFocus.count == 0 )
					indexOfLogEntryToSelectAndFocus = -1;
				else
				{
					for( int i = Mathf.Min( indexOfLogEntryToSelectAndFocus, logEntriesToShow.Count - 1 ); i >= 0; i-- )
					{
						if( logEntriesToShow[i] == logEntryToSelectAndFocus )
						{
							indexOfLogEntryToSelectAndFocus = i;
							break;
						}
					}
				}

				recycledListView.OnLogEntriesRemoved( removedLogEntriesToShowCount );

				if( isLogWindowVisible )
					OnLogEntriesUpdated( false, true );
			}
			else if( isLogWindowVisible && isCollapseOn )
				recycledListView.RefreshCollapsedLogEntryCounts();

			entryCountTextsDirty = true;
		}

		private void RemoveUncollapsedLogEntry( DebugLogEntry logEntry )
		{
			if( --logEntry.count <= 0 )
				anyCollapsedLogRemoved = true;

			if( !isCollapseOn && logEntriesToShow[removedLogEntriesToShowCount] == logEntry )
				removedLogEntriesToShowCount++;

			if( logEntry.logType == LogType.Log )
				infoEntryCount--;
			else if( logEntry.logType == LogType.Warning )
				warningEntryCount--;
			else
				errorEntryCount--;
		}

		private bool ShouldRemoveCollapsedLogEntry( DebugLogEntry logEntry )
		{
			if( logEntry.count <= 0 )
			{
				PoolLogEntry( logEntry );
				collapsedLogEntriesMap.Remove( logEntry );

				return true;
			}

			return false;
		}

		private bool ShouldRemoveLogEntryToShow( DebugLogEntry logEntry )
		{
			return logEntry.count <= 0;
		}

		private void UpdateLogEntryCollapsedIndex( DebugLogEntry logEntry, int collapsedIndex )
		{
			logEntry.collapsedIndex = collapsedIndex;
		}

		private void OnLogEntriesUpdated( bool updateAllVisibleItemContents, bool validateScrollPosition )
		{
			recycledListView.OnLogEntriesUpdated( updateAllVisibleItemContents );
			shouldUpdateRecycledListView = false;

			if( validateScrollPosition )
				ValidateScrollPosition();
		}

		private void PoolLogEntry( DebugLogEntry logEntry )
		{
			if( pooledLogEntries.Count < 4096 )
			{
				logEntry.Clear();
				pooledLogEntries.Push( logEntry );
			}
		}

		// Make sure the scroll bar of the scroll rect is adjusted properly
		internal void ValidateScrollPosition()
		{
			// When scrollbar is snapped to the very bottom of the scroll view, sometimes OnScroll alone doesn't work
			if( logItemsScrollRect.verticalNormalizedPosition <= Mathf.Epsilon )
				logItemsScrollRect.verticalNormalizedPosition = 0.0001f;

			logItemsScrollRect.OnScroll( nullPointerEventData );
		}

		// Modifies certain properties of the most recently received log
		public void AdjustLatestPendingLog( bool autoExpand, bool stripStackTrace )
		{
			lock( logEntriesLock )
			{
				if( queuedLogEntries.Count == 0 )
					return;

				if( autoExpand ) // Automatically expand the latest log in queuedLogEntries
					pendingLogToAutoExpand = queuedLogEntries.Count;

				if( stripStackTrace ) // Omit the latest log's stack trace
				{
					QueuedDebugLogEntry log = queuedLogEntries[queuedLogEntries.Count - 1];
					queuedLogEntries[queuedLogEntries.Count - 1] = new QueuedDebugLogEntry( log.logString, string.Empty, log.logType );
				}
			}
		}

		// Clear all the logs
		public void ClearLogs()
		{
			SnapToBottom = true;
			indexOfLogEntryToSelectAndFocus = -1;

			infoEntryCount = 0;
			warningEntryCount = 0;
			errorEntryCount = 0;

			infoEntryCountText.text = "0";
			warningEntryCountText.text = "0";
			errorEntryCountText.text = "0";

			collapsedLogEntries.ForEach( poolLogEntryAction );

			collapsedLogEntries.Clear();
			collapsedLogEntriesMap.Clear();
			uncollapsedLogEntries.Clear();
			logEntriesToShow.Clear();

			if( collapsedLogEntriesTimestamps != null )
			{
				collapsedLogEntriesTimestamps.Clear();
				uncollapsedLogEntriesTimestamps.Clear();
				timestampsOfLogEntriesToShow.Clear();
			}

			recycledListView.DeselectSelectedLogItem();
			OnLogEntriesUpdated( true, true );
		}

		// Collapse button is clicked
		private void CollapseButtonPressed()
		{
			// Swap the value of collapse mode
			isCollapseOn = !isCollapseOn;

			collapseButton.color = isCollapseOn ? collapseButtonSelectedColor : collapseButtonNormalColor;
			recycledListView.SetCollapseMode( isCollapseOn );

			// Determine the new list of debug entries to show
			FilterLogs();
		}

		// Filtering mode of info logs has changed
		private void FilterLogButtonPressed()
		{
			logFilter = logFilter ^ DebugLogFilter.Info;

			if( ( logFilter & DebugLogFilter.Info ) == DebugLogFilter.Info )
				filterInfoButton.color = filterButtonsSelectedColor;
			else
				filterInfoButton.color = filterButtonsNormalColor;

			FilterLogs();
		}

		// Filtering mode of warning logs has changed
		private void FilterWarningButtonPressed()
		{
			logFilter = logFilter ^ DebugLogFilter.Warning;

			if( ( logFilter & DebugLogFilter.Warning ) == DebugLogFilter.Warning )
				filterWarningButton.color = filterButtonsSelectedColor;
			else
				filterWarningButton.color = filterButtonsNormalColor;

			FilterLogs();
		}

		// Filtering mode of error logs has changed
		private void FilterErrorButtonPressed()
		{
			logFilter = logFilter ^ DebugLogFilter.Error;

			if( ( logFilter & DebugLogFilter.Error ) == DebugLogFilter.Error )
				filterErrorButton.color = filterButtonsSelectedColor;
			else
				filterErrorButton.color = filterButtonsNormalColor;

			FilterLogs();
		}

		// Search term has changed
		private void SearchTermChanged( string searchTerm )
		{
			if( searchTerm != null )
				searchTerm = searchTerm.Trim();

			this.searchTerm = searchTerm;
			bool isInSearchMode = !string.IsNullOrEmpty( searchTerm );
			if( isInSearchMode || this.isInSearchMode )
			{
				this.isInSearchMode = isInSearchMode;
				FilterLogs();
			}
		}

		// Show suggestions for the currently entered command
		private void RefreshCommandSuggestions( string command )
		{
			if( !showCommandSuggestions )
				return;

			commandInputFieldPrevCaretPos = commandInputField.caretPosition;

			// Don't recalculate the command suggestions if the input command hasn't changed (i.e. only caret's position has changed)
			bool commandChanged = command != commandInputFieldPrevCommand;
			bool commandNameOrParametersChanged = false;
			if( commandChanged )
			{
				commandInputFieldPrevCommand = command;

				matchingCommandSuggestions.Clear();
				commandCaretIndexIncrements.Clear();

				string prevCommandName = commandInputFieldPrevCommandName;
				int numberOfParameters;
				DebugLogConsole.GetCommandSuggestions( command, matchingCommandSuggestions, commandCaretIndexIncrements, ref commandInputFieldPrevCommandName, out numberOfParameters );
				if( prevCommandName != commandInputFieldPrevCommandName || numberOfParameters != commandInputFieldPrevParamCount )
				{
					commandInputFieldPrevParamCount = numberOfParameters;
					commandNameOrParametersChanged = true;
				}
			}

			int caretArgumentIndex = 0;
			int caretPos = commandInputField.caretPosition;
			for( int i = 0; i < commandCaretIndexIncrements.Count && caretPos > commandCaretIndexIncrements[i]; i++ )
				caretArgumentIndex++;

			if( caretArgumentIndex != commandInputFieldPrevCaretArgumentIndex )
				commandInputFieldPrevCaretArgumentIndex = caretArgumentIndex;
			else if( !commandChanged || !commandNameOrParametersChanged )
			{
				// Command suggestions don't need to be updated if:
				// a) neither the entered command nor the argument that the caret is hovering has changed
				// b) entered command has changed but command's name hasn't changed, parameter count hasn't changed and the argument
				//    that the caret is hovering hasn't changed (i.e. user has continued typing a parameter's value)
				return;
			}

			if( matchingCommandSuggestions.Count == 0 )
				OnEndEditCommand( command );
			else
			{
				if( !commandSuggestionsContainer.gameObject.activeSelf )
					commandSuggestionsContainer.gameObject.SetActive( true );

				int suggestionInstancesCount = commandSuggestionInstances.Count;
				int suggestionsCount = matchingCommandSuggestions.Count;

				for( int i = 0; i < suggestionsCount; i++ )
				{
					if( i >= visibleCommandSuggestionInstances )
					{
						if( i >= suggestionInstancesCount )
							commandSuggestionInstances.Add( Instantiate( commandSuggestionPrefab, commandSuggestionsContainer, false ) );
						else
							commandSuggestionInstances[i].gameObject.SetActive( true );

						visibleCommandSuggestionInstances++;
					}

					ConsoleMethodInfo suggestedCommand = matchingCommandSuggestions[i];
					sharedStringBuilder.Length = 0;
					if( caretArgumentIndex > 0 )
						sharedStringBuilder.Append( suggestedCommand.command );
					else
						sharedStringBuilder.Append( commandSuggestionHighlightStart ).Append( matchingCommandSuggestions[i].command ).Append( commandSuggestionHighlightEnd );

					if( suggestedCommand.parameters.Length > 0 )
					{
						sharedStringBuilder.Append( " " );

						// If the command name wasn't highlighted, a parameter must always be highlighted
						int caretParameterIndex = caretArgumentIndex - 1;
						if( caretParameterIndex >= suggestedCommand.parameters.Length )
							caretParameterIndex = suggestedCommand.parameters.Length - 1;

						for( int j = 0; j < suggestedCommand.parameters.Length; j++ )
						{
							if( caretParameterIndex != j )
								sharedStringBuilder.Append( suggestedCommand.parameters[j] );
							else
								sharedStringBuilder.Append( commandSuggestionHighlightStart ).Append( suggestedCommand.parameters[j] ).Append( commandSuggestionHighlightEnd );
						}
					}

					commandSuggestionInstances[i].text = sharedStringBuilder.ToString();
				}

				for( int i = visibleCommandSuggestionInstances - 1; i >= suggestionsCount; i-- )
					commandSuggestionInstances[i].gameObject.SetActive( false );

				visibleCommandSuggestionInstances = suggestionsCount;
			}
		}

		// Command input field's text has changed
		private void OnEditCommand( string command )
		{
			RefreshCommandSuggestions( command );

			if( !commandInputFieldAutoCompletedNow )
				commandInputFieldAutoCompleteBase = null;
			else // This change was caused by autocomplete
				commandInputFieldAutoCompletedNow = false;
		}

		// Command input field has lost focus
		private void OnEndEditCommand( string command )
		{
			if( commandSuggestionsContainer.gameObject.activeSelf )
				commandSuggestionsContainer.gameObject.SetActive( false );
		}

		// Debug window is being resized,
		// Set the sizeDelta property of the window accordingly while
		// preventing window dimensions from going below the minimum dimensions
		internal void Resize( PointerEventData eventData )
		{
			Vector2 localPoint;
			if( !RectTransformUtility.ScreenPointToLocalPointInRectangle( canvasTR, eventData.position, eventData.pressEventCamera, out localPoint ) )
				return;

			// To be able to maximize the log window easily:
			// - When enableHorizontalResizing is true and resizing horizontally, resize button will be grabbed from its left edge (if resizeFromRight is true) or its right edge
			// - While resizing vertically, resize button will be grabbed from its top edge
			Rect resizeButtonRect = ( (RectTransform) resizeButton.rectTransform.parent ).rect;
			float resizeButtonWidth = resizeButtonRect.width;
			float resizeButtonHeight = resizeButtonRect.height;

			Vector2 canvasPivot = canvasTR.pivot;
			Vector2 canvasSize = canvasTR.rect.size;
			Vector2 anchorMin = logWindowTR.anchorMin;

			// Horizontal resizing
			if( enableHorizontalResizing )
			{
				if( resizeFromRight )
				{
					localPoint.x += canvasPivot.x * canvasSize.x + resizeButtonWidth;
					if( localPoint.x < minimumWidth )
						localPoint.x = minimumWidth;

					Vector2 anchorMax = logWindowTR.anchorMax;
					anchorMax.x = Mathf.Clamp01( localPoint.x / canvasSize.x );
					logWindowTR.anchorMax = anchorMax;
				}
				else
				{
					localPoint.x += canvasPivot.x * canvasSize.x - resizeButtonWidth;
					if( localPoint.x > canvasSize.x - minimumWidth )
						localPoint.x = canvasSize.x - minimumWidth;

					anchorMin.x = Mathf.Clamp01( localPoint.x / canvasSize.x );
				}
			}

			// Vertical resizing
			float notchHeight = -logWindowTR.sizeDelta.y; // Size of notch screen cutouts at the top of the screen

			localPoint.y += canvasPivot.y * canvasSize.y - resizeButtonHeight;
			if( localPoint.y > canvasSize.y - minimumHeight - notchHeight )
				localPoint.y = canvasSize.y - minimumHeight - notchHeight;

			anchorMin.y = Mathf.Clamp01( localPoint.y / canvasSize.y );

			logWindowTR.anchorMin = anchorMin;

			// Update the recycled list view
			recycledListView.OnViewportHeightChanged();
		}

		// Determine the filtered list of debug entries to show on screen
		private void FilterLogs()
		{
			recycledListView.OnBeforeFilterLogs();
			logEntriesToShow.Clear();

			if( timestampsOfLogEntriesToShow != null )
				timestampsOfLogEntriesToShow.Clear();

			if( logFilter != DebugLogFilter.None )
			{
				DynamicCircularBuffer<DebugLogEntry> targetLogEntries = isCollapseOn ? collapsedLogEntries : uncollapsedLogEntries;
				DynamicCircularBuffer<DebugLogEntryTimestamp> targetLogEntriesTimestamps = isCollapseOn ? collapsedLogEntriesTimestamps : uncollapsedLogEntriesTimestamps;

				if( logFilter == DebugLogFilter.All )
				{
					if( !isInSearchMode )
					{
						logEntriesToShow.AddRange( targetLogEntries );

						if( timestampsOfLogEntriesToShow != null )
							timestampsOfLogEntriesToShow.AddRange( targetLogEntriesTimestamps );
					}
					else
					{
						for( int i = 0, count = targetLogEntries.Count; i < count; i++ )
						{
							if( targetLogEntries[i].MatchesSearchTerm( searchTerm ) )
							{
								logEntriesToShow.Add( targetLogEntries[i] );

								if( timestampsOfLogEntriesToShow != null )
									timestampsOfLogEntriesToShow.Add( targetLogEntriesTimestamps[i] );
							}
						}
					}
				}
				else
				{
					// Show only the debug entries that match the current filter
					bool isInfoEnabled = ( logFilter & DebugLogFilter.Info ) == DebugLogFilter.Info;
					bool isWarningEnabled = ( logFilter & DebugLogFilter.Warning ) == DebugLogFilter.Warning;
					bool isErrorEnabled = ( logFilter & DebugLogFilter.Error ) == DebugLogFilter.Error;

					for( int i = 0, count = targetLogEntries.Count; i < count; i++ )
					{
						DebugLogEntry logEntry = targetLogEntries[i];

						if( isInSearchMode && !logEntry.MatchesSearchTerm( searchTerm ) )
							continue;

						bool shouldShowLog = false;
						if( logEntry.logType == LogType.Log )
						{
							if( isInfoEnabled )
								shouldShowLog = true;
						}
						else if( logEntry.logType == LogType.Warning )
						{
							if( isWarningEnabled )
								shouldShowLog = true;
						}
						else if( isErrorEnabled )
							shouldShowLog = true;

						if( shouldShowLog )
						{
							logEntriesToShow.Add( logEntry );

							if( timestampsOfLogEntriesToShow != null )
								timestampsOfLogEntriesToShow.Add( targetLogEntriesTimestamps[i] );
						}
					}
				}
			}

			// Update the recycled list view
			recycledListView.OnAfterFilterLogs();
			OnLogEntriesUpdated( true, true );
		}

        public string GetAllLogs()
        {
            return GetAllLogs(int.MaxValue, float.PositiveInfinity);
        }

        /// <param name="maxLogCount">Maximum allowed log count.</param>
        /// <param name="maxElapsedTime">Maximum allowed time interval (in seconds) between now and the logs' arrival time (requires <see cref="captureLogTimestamps"/> to be enabled).</param>
        public string GetAllLogs(int maxLogCount, float maxElapsedTime)
		{
			// Process all pending logs since we want to return "all" logs
			ProcessQueuedLogs( queuedLogEntries.Count );

            int startIndex = uncollapsedLogEntries.Count - Mathf.Min(uncollapsedLogEntries.Count, maxLogCount);
            if (uncollapsedLogEntriesTimestamps != null)
            {
                float currentElapsedSeconds = Time.realtimeSinceStartup;
                while (startIndex < uncollapsedLogEntries.Count && currentElapsedSeconds - uncollapsedLogEntriesTimestamps[startIndex].elapsedSeconds > maxElapsedTime)
                    startIndex++;
            }

			int length = 0;
			int newLineLength = System.Environment.NewLine.Length;
            for (int i = startIndex; i < uncollapsedLogEntries.Count; i++)
			{
				DebugLogEntry entry = uncollapsedLogEntries[i];
				length += entry.logString.Length + entry.stackTrace.Length + newLineLength * 3;
			}

            if (uncollapsedLogEntriesTimestamps != null)
                length += (uncollapsedLogEntries.Count - startIndex) * 30;

			length += 200; // Just in case...

			StringBuilder sb = new StringBuilder( length );
            for (int i = startIndex; i < uncollapsedLogEntries.Count; i++)
			{
				DebugLogEntry entry = uncollapsedLogEntries[i];

				if( uncollapsedLogEntriesTimestamps != null )
				{
					uncollapsedLogEntriesTimestamps[i].AppendFullTimestamp( sb );
					sb.Append( ": " );
				}

				sb.AppendLine( entry.logString ).AppendLine( entry.stackTrace ).AppendLine();
			}

			sb.Append( "Current time: " ).AppendLine( ( System.DateTime.UtcNow + localTimeUtcOffset ).ToString( "F" ) );
			sb.Append( "Version: " ).AppendLine( Application.version );

			return sb.ToString();
		}

		/// <param name="logTimestamps">Is <c>null</c> if <see cref="captureLogTimestamps"/> is <c>false</c>. Indices are in sync with <paramref name="logEntries"/>.</param>
		/// <remarks>You mustn't modify the returned buffers in any way.</remarks>
		public void GetAllLogs( out DynamicCircularBuffer<DebugLogEntry> logEntries, out DynamicCircularBuffer<DebugLogEntryTimestamp> logTimestamps )
		{
			// Process all pending logs since we want to return "all" logs
			ProcessQueuedLogs( queuedLogEntries.Count );

			logEntries = uncollapsedLogEntries;
			logTimestamps = uncollapsedLogEntriesTimestamps;
		}

		public void SaveLogsToFile()
		{
			SaveLogsToFile( Path.Combine( Application.persistentDataPath, System.DateTime.Now.ToString( "dd-MM-yyyy--HH-mm-ss" ) + ".txt" ) );
		}

		public void SaveLogsToFile( string filePath )
		{
			File.WriteAllText( filePath, GetAllLogs() );
			Debug.Log( "Logs saved to: " + filePath );
		}

		// If a cutout is intersecting with debug window on notch screens, shift the window downwards
		private void CheckScreenCutout()
		{
			if( !avoidScreenCutout )
				return;

#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
			// Check if there is a cutout at the top of the screen
			int screenHeight = Screen.height;
			float safeYMax = Screen.safeArea.yMax;
			if( safeYMax < screenHeight - 1 ) // 1: a small threshold
			{
				// There is a cutout, shift the log window downwards
				float cutoutPercentage = ( screenHeight - safeYMax ) / Screen.height;
				float cutoutLocalSize = cutoutPercentage * canvasTR.rect.height;

				logWindowTR.anchoredPosition = new Vector2( 0f, -cutoutLocalSize );
				logWindowTR.sizeDelta = new Vector2( 0f, -cutoutLocalSize );
			}
			else
			{
				logWindowTR.anchoredPosition = Vector2.zero;
				logWindowTR.sizeDelta = Vector2.zero;
			}
#endif
		}

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
		private IEnumerator ActivateCommandInputFieldCoroutine()
		{
			// Waiting 1 frame before activating commandInputField ensures that the toggleKey isn't captured by it
			yield return null;
			commandInputField.ActivateInputField();

			yield return null;
			commandInputField.MoveTextEnd( false );
		}
#endif

		// Pool an unused log item
		internal void PoolLogItem( DebugLogItem logItem )
		{
			logItem.CanvasGroup.alpha = 0f;
			logItem.CanvasGroup.blocksRaycasts = false;

			pooledLogItems.Push( logItem );
		}

		// Fetch a log item from the pool
		internal DebugLogItem PopLogItem()
		{
			DebugLogItem newLogItem;

			// If pool is not empty, fetch a log item from the pool,
			// create a new log item otherwise
			if( pooledLogItems.Count > 0 )
			{
				newLogItem = pooledLogItems.Pop();
				newLogItem.CanvasGroup.alpha = 1f;
				newLogItem.CanvasGroup.blocksRaycasts = true;
			}
			else
			{
				newLogItem = (DebugLogItem) Instantiate( logItemPrefab, logItemsContainer, false );
				newLogItem.Initialize( recycledListView );
			}

			return newLogItem;
		}
	}
}