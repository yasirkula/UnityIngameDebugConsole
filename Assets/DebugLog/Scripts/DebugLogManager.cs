using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// In-game Debug Console / DebugLogManager
// Author: Suleyman Yasir Kula
// 
// Receives debug entries and custom events (e.g. Clear, Collapse)
// and creates/destroys log items accordingly
// 
// - Vocabulary -
// Debug/Log entry: a Debug.Log/LogError/LogWarning/LogException/LogAssertion request made by
//                   the client and intercepted by this manager object
// Debug/Log item: a visual (uGUI) representation of a debug entry
// 
// Not every debug entry is necessarly represented by a unique log item: when collapse is enabled,
// multiple debug entries can be represented by a single log item

public class DebugLogManager : MonoBehaviour
{
    private static DebugLogManager instance = null;

    // Debug console will persist between scenes
    public bool singleton = true;
    
    public GameObject logItemPrefab;

    public RectTransform logWindowTR;
    public RectTransform logItemsContainer;
    public Transform logItemsPool;
    public Text clickedLogItemDetails;
    public Image collapseButton;

    public ScrollRect logItemsScrollRect;
    public ScrollRect clickedLogItemDetailsScrollRect;

    // Minimum dimensions for the console window
    public float logWindowMinWidth = 250f;
    public float logWindowMinHeight = 200f;

    // Maximum number of debug items to pool instead of Destroy
    public int maximumLogItemsToPool = 10;

    // Visuals for different log types
    public Sprite infoLog, warningLog, errorLog;
    
    public Color logItemNormalColor1, logItemNormalColor2, logItemSelectedColor;
    public Color collapseButtonNormalColor, collapseButtonSelectedColor;

    private int numberOfLogs = 0;
    private DebugLogItem lastClickedLogItem = null;

    private bool isCollapseOn = false;
    // If the last log item is completely visible (scrollbar is at the bottom),
    // scrollbar will remain at the bottom when new debug entries are received
    private bool snapToBottom = true;

    private List<DebugLogItem> pooledLogItems;
    // The debug items to show when collapse is enabled
    private Dictionary<string, DebugLogItem> collapsedLogItems;
    // Incoming order of debug entries (used while uncollapsing)
    private List<DebugLogItem> uncollapsedLogItems;
    
    private Vector2 windowDragDeltaPosition;

    void OnEnable()
    {
        // Only one instance of debug console is allowed
        if( instance == null )
        {
            instance = this;
            pooledLogItems = new List<DebugLogItem>();
            collapsedLogItems = new Dictionary<string, DebugLogItem>();
            uncollapsedLogItems = new List<DebugLogItem>();

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

        Debug.LogAssertion( "assert" );
        Debug.LogError( "error" );
        Debug.LogException( new System.IO.EndOfStreamException() );
        Debug.LogWarning( "warning" );
        Debug.Log( "log" );
    }

    void OnDisable()
    {
        // Stop receiving debug entries
        Application.logMessageReceived -= ReceivedLog;
    }

    // A log item is clicked
    public static void OnLogClicked( DebugLogItem logItem )
    {
        // If the clicked log item is not already selected
        if( instance.lastClickedLogItem != logItem )
        {
            // If another log item was selected previously, change its color properly
            if( instance.lastClickedLogItem != null )
            {
                int lastClickedLogItemIndex = instance.lastClickedLogItem.transformComponent.GetSiblingIndex();
                if( lastClickedLogItemIndex % 2 == 0 )
                    instance.lastClickedLogItem.imageComponent.color = instance.logItemNormalColor1;
                else
                    instance.lastClickedLogItem.imageComponent.color = instance.logItemNormalColor2;
            }

            // Highlight the selected log item
            logItem.imageComponent.color = instance.logItemSelectedColor;
            // Show full stacktrace of the debug entry
            instance.clickedLogItemDetails.text = logItem.ToString();

            instance.lastClickedLogItem = logItem;
        }

        // Move scrollbar of Log Item Details scroll rect to the top
        instance.clickedLogItemDetailsScrollRect.verticalNormalizedPosition = 1f;
    }

    // A debug entry is received
    void ReceivedLog( string logString, string stackTrace, LogType logType )
    {
        DebugLogItem newLogItem;
        if( isCollapseOn && collapsedLogItems.TryGetValue( string.Concat( logString, "\n", stackTrace ), out newLogItem ) )
        {
            // If collapse is enabled and a collapsed log item for this entry already exists,
            // increment that log item's count property and add this log entry to
            // uncollapsedLogItems list so that this entry's order in the non-collapsed list
            // will be known
            newLogItem.IncrementCount();
            uncollapsedLogItems.Add( newLogItem );
        }
        else
        {
            // If either collapse is disabled or a collapsed log item for this entry does not exist,
            // create a new log item (or fetch it from pool, if it is not empty) and set its properties
            Sprite logSpriteRepresentation;
            if( logType == LogType.Log )
                logSpriteRepresentation = infoLog;
            else if( logType == LogType.Warning )
                logSpriteRepresentation = warningLog;
            else
                logSpriteRepresentation = errorLog;

            newLogItem = CreateOrUnpoolLogItem( logString, stackTrace, logSpriteRepresentation );

            numberOfLogs++;

            if( isCollapseOn )
            {
                // If collapse is enabled, add this log item to collapsedLogItems so that
                // the next time the same debug entry is received, the count property of this
                // log item will be increased instead of creating a new log item
                collapsedLogItems.Add( newLogItem.ToString(), newLogItem );
                // Store the order the debug entries are received
                uncollapsedLogItems.Add( newLogItem );
                // Show the count of this collapsed log item
                newLogItem.ShowCount();
            }
            else
            {
                // Collapse is disabled, hide the count of this log item (just in case)
                newLogItem.HideCount();
            }
        }
    }

    // If snapToBottom is enabled, force the scrollbar to the bottom
    void LateUpdate()
    {
        if( snapToBottom )
        {
            logItemsScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    // Value of snapToBottom is changed (user scrolled the list manually)
    public void OnSnapToBottomChanged( bool snapToBottom )
    {
        this.snapToBottom = snapToBottom;
    }

    // Clear button is clicked
    public void ClearButtonPressed()
    {
        numberOfLogs = 0;
        lastClickedLogItem = null;
        collapsedLogItems.Clear();
        uncollapsedLogItems.Clear();

        // Clear all the log items (either pool them or destroy them)
        for( int i = 0; i < logItemsContainer.childCount; i++ )
        {
            if( DestroyOrPoolLogItem( logItemsContainer.GetChild( i ) ) )
            {
                // log item is pooled and as a result, child indices for the
                // following log items are decreased by 1, so compensate it
                i--;
            }
        }

        // Clear the Selected Log Item Details text
        clickedLogItemDetails.text = "";
    }

    // Collapse button is clicked
    public void CollapseButtonPressed()
    {
        // Swap the value of collapse mode
        isCollapseOn = !isCollapseOn;

        if( isCollapseOn )
        {
            collapseButton.color = collapseButtonSelectedColor;

            numberOfLogs = 0;
            for( int i = 0; i < logItemsContainer.childCount; i++ )
            {
                // Foreach log item, check if a collapsed version of this log item exists
                DebugLogItem thisLogItem = logItemsContainer.GetChild( i ).GetComponent<DebugLogItem>();
                DebugLogItem collapsedLogItem;
                string key = thisLogItem.ToString();
                if( collapsedLogItems.TryGetValue( key, out collapsedLogItem ) )
                {
                    // If a collapsed version of this log item exists
                    if( thisLogItem != collapsedLogItem )
                    {
                        // If this log item is not the collapsed log item,
                        // increment the count of the collapsed log item and 
                        // destroy this log item
                        collapsedLogItem.IncrementCount();

                        if( DestroyOrPoolLogItem( thisLogItem ) )
                        {
                            // log item is pooled and as a result, child indices for the
                            // following log items are decreased by 1, so compensate it
                            i--;
                        }
                    }
                    else
                    {
                        // This log item is the collapsed log item,
                        // simply adjust its color and show its counts property on screen
                        ColorLogItem( collapsedLogItem );
                        collapsedLogItem.ShowCount();
                        numberOfLogs++;
                    }

                    // Store the order the debug entries are received in a list 
                    // (to use while uncollapsing)
                    uncollapsedLogItems.Add( collapsedLogItem );
                }
                else
                {
                    // If a collapsed version of this log item does not exist,
                    // make this log item the collapsed one
                    collapsedLogItems.Add( key, thisLogItem );
                    uncollapsedLogItems.Add( thisLogItem );
                    thisLogItem.ShowCount();
                    ColorLogItem( thisLogItem );
                    numberOfLogs++;
                }
            }
        }
        else
        {
            collapseButton.color = collapseButtonNormalColor;

            // Don't destroy the collapsed log items while uncollapsing,
            // instead use them for their first uncollapsed version
            HashSet<DebugLogItem> usedLogItems = new HashSet<DebugLogItem>();
            numberOfLogs = 0;
            for( int i = 0; i < uncollapsedLogItems.Count; i++ )
            {
                // Foreach received debug entry, if the collapsed log item 
                // for this debug entry is not yet used, use it for the non-collapsed
                // log item, otherwise create a new log item (or fetch one from pool,
                // if it is not empty)
                DebugLogItem logItemToUncollapse = uncollapsedLogItems[i];
                DebugLogItem newLogItem;
                if( !usedLogItems.Contains( logItemToUncollapse ) )
                {
                    newLogItem = logItemToUncollapse;
                    usedLogItems.Add( logItemToUncollapse );
                }
                else
                {
                    newLogItem = CreateOrUnpoolLogItem( logItemToUncollapse );
                }

                // Set this log item's order and reset its count property (just in case)
                newLogItem.transformComponent.SetSiblingIndex( numberOfLogs );
                newLogItem.ResetCount();
                newLogItem.HideCount();
                ColorLogItem( newLogItem );

                numberOfLogs++;
            }
            
            uncollapsedLogItems.Clear();
        }
    }

    // Debug window is about to be moved on screen,
    // cache the offset between pointer and the window position
    public void OnWindowDragStarted( BaseEventData dat )
    {
        PointerEventData eventData = (PointerEventData) dat;
        
        windowDragDeltaPosition = (Vector2) logWindowTR.position - eventData.position;
    }

    // Debug window is being dragged,
    // set the new position of the window
    public void OnWindowDrag( BaseEventData dat )
    {
        PointerEventData eventData = (PointerEventData) dat;

        logWindowTR.position = eventData.position + windowDragDeltaPosition;
    }

    // Debug window is being resized,
    // Set the sizeDelta property of the window accordingly while
    // preventing window dimensions from going below the minimum dimensions
    public void OnWindowResize( BaseEventData dat )
    {
        PointerEventData eventData = (PointerEventData) dat;

        Vector2 newSize = eventData.position - (Vector2) logWindowTR.position;
        newSize.y = -newSize.y;
        if( newSize.x < logWindowMinWidth ) newSize.x = logWindowMinWidth;
        if( newSize.y < logWindowMinHeight ) newSize.y = logWindowMinHeight;
        logWindowTR.sizeDelta = newSize;
    }

    // Color a log item using its order
    private void ColorLogItem( DebugLogItem logItem )
    {
        if( numberOfLogs % 2 == 0 )
            logItem.imageComponent.color = logItemNormalColor1;
        else
            logItem.imageComponent.color = logItemNormalColor2;
    }

    // Clone a log item for a debug entry
    private DebugLogItem CreateOrUnpoolLogItem( DebugLogItem logToClone )
    {
        return CreateOrUnpoolLogItem( logToClone.GetLogString(), logToClone.GetStackTrace(), logToClone.GetLogSpriteRepresentation() );
    }

    // Create a log item for a new debug entry
    private DebugLogItem CreateOrUnpoolLogItem( string logString, string stackTrace, Sprite logSpriteRepresentation )
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
            newLogItem = Instantiate<GameObject>( logItemPrefab ).GetComponent<DebugLogItem>();
        }

        newLogItem.SetContent( logString, stackTrace, logSpriteRepresentation );

        ColorLogItem( newLogItem );

        newLogItem.transform.SetParent( logItemsContainer, false );

        return newLogItem;
    }

    // Destroy a log item and return whether it is pooled or not
    private bool DestroyOrPoolLogItem( Transform logItemTR )
    {
        // If pool has reached maximum capacity, destroy the log item,
        // pool it otherwise
        if( pooledLogItems.Count >= maximumLogItemsToPool )
        {
            Destroy( logItemTR.gameObject );
            return false;
        }
        else
        {
            DebugLogItem logItem = logItemTR.GetComponent<DebugLogItem>();
            logItem.transformComponent.SetParent( logItemsPool, false );
            logItem.gameObject.SetActive( false );
            pooledLogItems.Add( logItem );
            
            return true;
        }
    }

    // Same as above, without the need for GetComponent
    private bool DestroyOrPoolLogItem( DebugLogItem logItem )
    {
        if( pooledLogItems.Count >= maximumLogItemsToPool )
        {
            Destroy( logItem.gameObject );
            return false;
        }
        else
        {
            logItem.transformComponent.SetParent( logItemsPool, false );
            logItem.gameObject.SetActive( false );
            pooledLogItems.Add( logItem );

            return true;
        }
    }
}