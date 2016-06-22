using UnityEngine;
using System.Collections.Generic;

public class DebugLogRecycledListView : MonoBehaviour
{
	// Cached components
	public RectTransform transformComponent;
	public RectTransform viewportTransform;

	public DebugLogManager debugManager;
	
	public Color logItemNormalColor1, logItemNormalColor2, logItemSelectedColor;

	private float logItemHeight, _1OverLogItemHeight;
	private float viewportHeight;

	// Unique debug entries
	private List<DebugLogEntry> collapsedLogEntries = null;

	// Indices of debug entries to show in collapsedLogEntries
	private List<int> indicesOfEntriesToShow = null;

	private int indexOfSelectedLogEntry = -1;

	// Log items used to visualize the debug entries at specified indices
	private Dictionary<int, DebugLogItem> logItemsAtIndices = new Dictionary<int, DebugLogItem>();
	
	// Should update all the visible log items
	private bool updateListItemsContents = false;

	private bool isCollapseOn = false;

	// Current indices of debug entries shown on screen
	private int currentTopIndex = -1, currentBottomIndex = -1;
	
	void Awake()
	{
		viewportHeight = viewportTransform.rect.height;
	}

	public void SetCollapsedEntriesList( List<DebugLogEntry> entries )
	{
		collapsedLogEntries = entries;
	}

	public void SetEntryIndicesList( List<int> entryIndices )
	{
		indicesOfEntriesToShow = entryIndices;

		DeselectSelectedLogItem();

		// Update all the visible log items
		updateListItemsContents = true;
		OnLogEntriesUpdated();
	}

	public void SetLogItemHeight( float height )
	{
		logItemHeight = height;
		_1OverLogItemHeight = 1f / logItemHeight;
	}

	public void SetCollapseMode( bool collapse )
	{
		isCollapseOn = collapse;
	}

	// A log item is clicked, highlight it
	public void OnLogItemClicked( int index )
	{
		DeselectSelectedLogItem();

		indexOfSelectedLogEntry = index;
		
		if( index >= currentTopIndex && index <= currentBottomIndex )
		{
			ColorLogItem( logItemsAtIndices[index], index );
		}
	}

	// Deselect the currently selected log item
	private void DeselectSelectedLogItem()
	{
		int indexOfPreviouslySelectedLogEntry = indexOfSelectedLogEntry;
		indexOfSelectedLogEntry = -1;

		if( indexOfPreviouslySelectedLogEntry != -1 &&
			indexOfPreviouslySelectedLogEntry >= currentTopIndex && indexOfPreviouslySelectedLogEntry <= currentBottomIndex )
		{
			ColorLogItem( logItemsAtIndices[indexOfPreviouslySelectedLogEntry], indexOfPreviouslySelectedLogEntry );
		}
	}

	// Number of debug entries may be changed, update the list
	public void OnLogEntriesUpdated()
	{
		float newHeight = Mathf.Max( 1f, indicesOfEntriesToShow.Count * logItemHeight );
		transformComponent.sizeDelta = new Vector2( 0f, newHeight );

		UpdateItemsInTheList();
	}

	// Log window is resized, update the list
	public void OnViewportDimensionsChanged()
	{
		viewportHeight = viewportTransform.rect.height;

		UpdateItemsInTheList();
	}
	
	// Calculate the indices of log entries to show
	// and handle log items accordingly
	public void UpdateItemsInTheList()
	{
		// If there is at least one log entry to show
		if( indicesOfEntriesToShow.Count > 0 )
		{
			Vector3 localPos = transformComponent.localPosition;

			// Use an extra log item at each side, in case of scrolling
			int newTopIndex = (int) ( localPos.y * _1OverLogItemHeight ) - 1;
			int newBottomIndex = (int) ( ( localPos.y + viewportHeight ) * _1OverLogItemHeight ) + 1;

			if( newTopIndex < 0 )
				newTopIndex = 0;

			if( newBottomIndex > indicesOfEntriesToShow.Count - 1 )
				newBottomIndex = indicesOfEntriesToShow.Count - 1;

			if( currentTopIndex == -1 )
			{
				// There are no log items visible on screen,
				// just create the new log items
				updateListItemsContents = true;

				currentTopIndex = newTopIndex;
				currentBottomIndex = newBottomIndex;

				CreateLogItemsBetweenIndices( newTopIndex, newBottomIndex );
			}
			else
			{
				// There are some log items visible on screen

				if( newBottomIndex < currentTopIndex || newTopIndex > currentBottomIndex )
				{
					// If user scrolled a lot such that, none of the log items are now within
					// the bounds of the scroll view, pool all the previous log items and create
					// new log items for the new list of visible debug entries
					updateListItemsContents = true;

					DestroyLogItemsBetweenIndices( currentTopIndex, currentBottomIndex );
					CreateLogItemsBetweenIndices( newTopIndex, newBottomIndex );
				}
				else
				{
					// User did not scroll a lot such that, there are still some log items within
					// the bounds of the scroll view. Don't destroy them but update their content,
					// if necessary
					if( newTopIndex > currentTopIndex )
					{
						DestroyLogItemsBetweenIndices( currentTopIndex, newTopIndex - 1 );
					}

					if( newBottomIndex < currentBottomIndex )
					{
						DestroyLogItemsBetweenIndices( newBottomIndex + 1, currentBottomIndex );
					}

					if( newTopIndex < currentTopIndex )
					{
						CreateLogItemsBetweenIndices( newTopIndex, currentTopIndex - 1 );

						// If it is not necessary to update all the log items,
						// then just update the newly created log items. Otherwise,
						// wait for the major update
						if( !updateListItemsContents )
						{
							UpdateLogItemContentsBetweenIndices( newTopIndex, currentTopIndex - 1 );
						}
					}
					
					if( newBottomIndex > currentBottomIndex )
					{
						CreateLogItemsBetweenIndices( currentBottomIndex + 1, newBottomIndex );

						// If it is not necessary to update all the log items,
						// then just update the newly created log items. Otherwise,
						// wait for the major update
						if( !updateListItemsContents )
						{
							UpdateLogItemContentsBetweenIndices( currentBottomIndex + 1, newBottomIndex );
						}
					}
				}

				currentTopIndex = newTopIndex;
				currentBottomIndex = newBottomIndex;
			}
			
			if( updateListItemsContents )
			{
				// Update all the log items
				UpdateLogItemContentsBetweenIndices( currentTopIndex, currentBottomIndex );
			}
		}
		else if( currentTopIndex != -1 )
		{
			// There is no log entry to show but some log items are still visible,
			// pool them
			DestroyLogItemsBetweenIndices( currentTopIndex, currentBottomIndex );

			currentTopIndex = -1;
		}
	}

	private void CreateLogItemsBetweenIndices( int topIndex, int bottomIndex )
	{
		for( int i = topIndex; i <= bottomIndex; i++ )
		{
			CreateLogItemAtIndex( i );
		}
	}

	// Create (or unpool) a log item
	private void CreateLogItemAtIndex( int index )
	{
		DebugLogItem logItem = debugManager.UnpoolLogItem();

		// Reposition the log item
		logItem.transformComponent.localPosition = new Vector3( 1f, -index * logItemHeight, 0f );

		// Color the log item
		ColorLogItem( logItem, index );

		// To access this log item easily in the future, add it to the dictionary
		logItemsAtIndices[index] = logItem;
	}

	private void DestroyLogItemsBetweenIndices( int topIndex, int bottomIndex )
	{
		for( int i = topIndex; i <= bottomIndex; i++ )
		{
			debugManager.PoolLogItem( logItemsAtIndices[i] );
		}
	}

	private void UpdateLogItemContentsBetweenIndices( int topIndex, int bottomIndex )
	{
		DebugLogItem logItem;
		for( int i = topIndex; i <= bottomIndex; i++ )
		{
			logItem = logItemsAtIndices[i];
			logItem.SetContent( collapsedLogEntries[indicesOfEntriesToShow[i]], i );

			if( isCollapseOn )
				logItem.ShowCount();
			else
				logItem.HideCount();
		}
	}

	// Color a log item using its index
	private void ColorLogItem( DebugLogItem logItem, int index )
	{
		if( index == indexOfSelectedLogEntry )
			logItem.imageComponent.color = logItemSelectedColor;
		else if( index % 2 == 0 )
			logItem.imageComponent.color = logItemNormalColor1;
		else
			logItem.imageComponent.color = logItemNormalColor2;
	}
}