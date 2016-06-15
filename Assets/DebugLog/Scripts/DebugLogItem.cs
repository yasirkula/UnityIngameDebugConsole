using UnityEngine;
using UnityEngine.UI;

// In-game Debug Console / DebugLogItem
// Author: Suleyman Yasir Kula
// 
// A UI element to show information about a debug entry

public class DebugLogItem : MonoBehaviour
{
    // Cached components
    public Transform transformComponent;
    public Image imageComponent;

    public Text logText;
    public Image logTypeImage;

    // Objects related to count of a log item (collapsed mode)
    public GameObject logCountParent;
    public Text logCountText;

    private string stackTrace;
    private int collapsedCount;

    public void SetContent( string logString, string stackTrace, Sprite logType )
    {
        logText.text = logString;
        this.stackTrace = stackTrace;

        logTypeImage.sprite = logType;

        collapsedCount = 1;
        logCountText.text = "" + collapsedCount;
    }

    // Show count of this log item (collapsed mode)
    public void ShowCount()
    {
        logCountParent.SetActive( true );
    }

    // Hide count of this log item (non-collapsed mode)
    public void HideCount()
    {
        logCountParent.SetActive( false );
    }

    public void ResetCount()
    {
        collapsedCount = 1;
        logCountText.text = "" + collapsedCount;
    }

    public void IncrementCount()
    {
        collapsedCount++;
        logCountText.text = "" + collapsedCount;
    }

    // This log item is clicked, show its stack trace
    public void Clicked()
    {
        DebugLogManager.OnLogClicked( this );
    }

    // ++ ACCESSOR METHODS ++
    public string GetLogString()
    {
        return logText.text;
    }

    public string GetStackTrace()
    {
        return stackTrace;
    }

    public Sprite GetLogSpriteRepresentation()
    {
        return logTypeImage.sprite;
    }
    // -- ACCESSOR METHODS --

    // Return a string containing complete information about this debug entry
    public override string ToString()
    {
        return string.Concat( logText.text, "\n", stackTrace );
    }
}