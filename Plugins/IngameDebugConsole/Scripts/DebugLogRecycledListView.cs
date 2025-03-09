using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Handles the log items in an optimized way such that existing log items are
// recycled within the list instead of creating a new log item at each chance
namespace IngameDebugConsole
{
	public class DebugLogRecycledListView : MonoBehaviour
	{
#pragma warning disable 0649
		// Cached components
		[SerializeField]
		private RectTransform transformComponent;
		[SerializeField]
		private RectTransform viewportTransform;

		[SerializeField]
		private Color logItemNormalColor1;
		[SerializeField]
		private Color logItemNormalColor2;
		[SerializeField]
		private Color logItemSelectedColor;
#pragma warning restore 0649

		internal DebugLogManager manager;
		private ScrollRect scrollView;

		private float logItemHeight;

		private DynamicCircularBuffer<DebugLogEntry> entriesToShow = null;
		private DynamicCircularBuffer<DebugLogEntryTimestamp> timestampsOfEntriesToShow = null;

		private DebugLogEntry selectedLogEntry;
		private int indexOfSelectedLogEntry = int.MaxValue;
		private float heightOfSelectedLogEntry;
		private float DeltaHeightOfSelectedLogEntry { get { return heightOfSelectedLogEntry - logItemHeight; } }

		/// These properties are used by <see cref="OnBeforeFilterLogs"/> and <see cref="OnAfterFilterLogs"/>.
		private int collapsedOrderOfSelectedLogEntry;
		private float scrollDistanceToSelectedLogEntry;

		// Log items used to visualize the visible debug entries
		private readonly DynamicCircularBuffer<DebugLogItem> visibleLogItems = new DynamicCircularBuffer<DebugLogItem>( 32 );

		private bool isCollapseOn = false;

		// Current indices of debug entries shown on screen
		private int currentTopIndex = -1, currentBottomIndex = -1;

		private System.Predicate<DebugLogItem> shouldRemoveLogItemPredicate;
		private System.Action<DebugLogItem> poolLogItemAction;

		public float ItemHeight { get { return logItemHeight; } }
		public float SelectedItemHeight { get { return heightOfSelectedLogEntry; } }

		private void Awake()
		{
			scrollView = viewportTransform.GetComponentInParent<ScrollRect>();
			scrollView.onValueChanged.AddListener( ( pos ) =>
			{
				if( manager.IsLogWindowVisible )
					UpdateItemsInTheList( false );
			} );
		}

		public void Initialize( DebugLogManager manager, DynamicCircularBuffer<DebugLogEntry> entriesToShow, DynamicCircularBuffer<DebugLogEntryTimestamp> timestampsOfEntriesToShow, float logItemHeight )
		{
			this.manager = manager;
			this.entriesToShow = entriesToShow;
			this.timestampsOfEntriesToShow = timestampsOfEntriesToShow;
			this.logItemHeight = logItemHeight;

			shouldRemoveLogItemPredicate = ShouldRemoveLogItem;
			poolLogItemAction = manager.PoolLogItem;
		}

		public void SetCollapseMode( bool collapse )
		{
			isCollapseOn = collapse;
		}

		// A log item is clicked, highlight it
		public void OnLogItemClicked( DebugLogItem item )
		{
			OnLogItemClickedInternal( item.Index, item );
		}

		// Force expand the log item at specified index
		public void SelectAndFocusOnLogItemAtIndex( int itemIndex )
		{
			if( indexOfSelectedLogEntry != itemIndex ) // Make sure that we aren't deselecting the target log item
				OnLogItemClickedInternal( itemIndex );

			float viewportHeight = viewportTransform.rect.height;
			float transformComponentCenterYAtTop = viewportHeight * 0.5f;
			float transformComponentCenterYAtBottom = transformComponent.sizeDelta.y - viewportHeight * 0.5f;
			float transformComponentTargetCenterY = itemIndex * logItemHeight + viewportHeight * 0.5f;
			if( transformComponentCenterYAtTop == transformComponentCenterYAtBottom )
				scrollView.verticalNormalizedPosition = 0.5f;
			else
				scrollView.verticalNormalizedPosition = Mathf.Clamp01( Mathf.InverseLerp( transformComponentCenterYAtBottom, transformComponentCenterYAtTop, transformComponentTargetCenterY ) );

			manager.SnapToBottom = false;
		}

		private void OnLogItemClickedInternal( int itemIndex, DebugLogItem referenceItem = null )
		{
			int indexOfPreviouslySelectedLogEntry = indexOfSelectedLogEntry;
			DeselectSelectedLogItem();

			if( indexOfPreviouslySelectedLogEntry != itemIndex )
			{
				selectedLogEntry = entriesToShow[itemIndex];
				indexOfSelectedLogEntry = itemIndex;
				CalculateSelectedLogEntryHeight( referenceItem );

				manager.SnapToBottom = false;
			}

			CalculateContentHeight();
			UpdateItemsInTheList( true );

			manager.ValidateScrollPosition();
		}

		// Deselect the currently selected log item
		public void DeselectSelectedLogItem()
		{
			selectedLogEntry = null;
			indexOfSelectedLogEntry = int.MaxValue;
			heightOfSelectedLogEntry = 0f;
		}

		/// <summary>
		/// Cache the currently selected log item's properties so that its position can be restored after <see cref="OnAfterFilterLogs"/> is called.
		/// </summary>
		public void OnBeforeFilterLogs()
		{
			collapsedOrderOfSelectedLogEntry = 0;
			scrollDistanceToSelectedLogEntry = 0f;

			if( selectedLogEntry != null )
			{
				if( !isCollapseOn )
				{
					for( int i = 0; i < indexOfSelectedLogEntry; i++ )
					{
						if( entriesToShow[i] == selectedLogEntry )
							collapsedOrderOfSelectedLogEntry++;
					}
				}

				scrollDistanceToSelectedLogEntry = indexOfSelectedLogEntry * ItemHeight - transformComponent.anchoredPosition.y;
			}
		}

		/// <summary>
		/// See <see cref="OnBeforeFilterLogs"/>.
		/// </summary>
		public void OnAfterFilterLogs()
		{
			// Refresh selected log entry's index
			int newIndexOfSelectedLogEntry = -1;
			if( selectedLogEntry != null )
			{
				for( int i = 0; i < entriesToShow.Count; i++ )
				{
					if( entriesToShow[i] == selectedLogEntry && collapsedOrderOfSelectedLogEntry-- == 0 )
					{
						newIndexOfSelectedLogEntry = i;
						break;
					}
				}
			}

			if( newIndexOfSelectedLogEntry < 0 )
				DeselectSelectedLogItem();
			else
			{
				indexOfSelectedLogEntry = newIndexOfSelectedLogEntry;
				transformComponent.anchoredPosition = new Vector2( 0f, newIndexOfSelectedLogEntry * ItemHeight - scrollDistanceToSelectedLogEntry );
			}
		}

		// Number of debug entries may have changed, update the list
		public void OnLogEntriesUpdated( bool updateAllVisibleItemContents )
		{
			CalculateContentHeight();
			UpdateItemsInTheList( updateAllVisibleItemContents );
		}

		// A single collapsed log entry at specified index is updated, refresh its item if visible
		public void OnCollapsedLogEntryAtIndexUpdated( int index )
		{
			if( index >= currentTopIndex && index <= currentBottomIndex )
			{
				DebugLogItem logItem = GetLogItemAtIndex( index );
				logItem.ShowCount();

				if( timestampsOfEntriesToShow != null )
					logItem.UpdateTimestamp( timestampsOfEntriesToShow[index] );
			}
		}

		public void RefreshCollapsedLogEntryCounts()
		{
			for( int i = 0; i < visibleLogItems.Count; i++ )
				visibleLogItems[i].ShowCount();
		}

		public void OnLogEntriesRemoved( int removedLogCount )
		{
			if( selectedLogEntry != null )
			{
				bool isSelectedLogEntryRemoved = isCollapseOn ? ( selectedLogEntry.count == 0 ) : ( indexOfSelectedLogEntry < removedLogCount );
				if( isSelectedLogEntryRemoved )
					DeselectSelectedLogItem();
				else
					indexOfSelectedLogEntry = isCollapseOn ? FindIndexOfLogEntryInReverseDirection( selectedLogEntry, indexOfSelectedLogEntry ) : ( indexOfSelectedLogEntry - removedLogCount );
			}

			if( !manager.IsLogWindowVisible && manager.SnapToBottom )
			{
				// When log window becomes visible, it refreshes all logs. So unless snap to bottom is disabled, we don't need to
				// keep track of either the scroll position or the visible log items' positions.
				visibleLogItems.TrimStart( visibleLogItems.Count, poolLogItemAction );
			}
			else if( !isCollapseOn )
				visibleLogItems.TrimStart( Mathf.Clamp( removedLogCount - currentTopIndex, 0, visibleLogItems.Count ), poolLogItemAction );
			else
			{
				visibleLogItems.RemoveAll( shouldRemoveLogItemPredicate );
				if( visibleLogItems.Count > 0 )
					removedLogCount = currentTopIndex - FindIndexOfLogEntryInReverseDirection( visibleLogItems[0].Entry, visibleLogItems[0].Index );
			}

			if( visibleLogItems.Count == 0 )
			{
				currentTopIndex = -1;

				if( !manager.SnapToBottom )
					transformComponent.anchoredPosition = Vector2.zero;
			}
			else
			{
				currentTopIndex = Mathf.Max( 0, currentTopIndex - removedLogCount );
				currentBottomIndex = currentTopIndex + visibleLogItems.Count - 1;

				float firstVisibleLogItemInitialYPos = visibleLogItems[0].Transform.anchoredPosition.y;
				for( int i = 0; i < visibleLogItems.Count; i++ )
				{
					DebugLogItem logItem = visibleLogItems[i];
					logItem.Index = currentTopIndex + i;

					// If log window is visible, we need to manually refresh the visible items' visual properties. Otherwise, all log items will be refreshed when log window is opened
					if( manager.IsLogWindowVisible )
					{
						RepositionLogItem( logItem );
						ColorLogItem( logItem );

						// Update collapsed count of the log items in collapsed mode
						if( isCollapseOn )
							logItem.ShowCount();
					}
				}

				// Shift the ScrollRect
				if( !manager.SnapToBottom )
					transformComponent.anchoredPosition = new Vector2( 0f, Mathf.Max( 0f, transformComponent.anchoredPosition.y - ( visibleLogItems[0].Transform.anchoredPosition.y - firstVisibleLogItemInitialYPos ) ) );
			}
		}

		private bool ShouldRemoveLogItem( DebugLogItem logItem )
		{
			if( logItem.Entry.count == 0 )
			{
				poolLogItemAction( logItem );
				return true;
			}

			return false;
		}

		private int FindIndexOfLogEntryInReverseDirection( DebugLogEntry logEntry, int startIndex )
		{
			for( int i = Mathf.Min( startIndex, entriesToShow.Count - 1 ); i >= 0; i-- )
			{
				if( entriesToShow[i] == logEntry )
					return i;
			}

			return -1;
		}

		// Log window's width has changed, update the expanded (currently selected) log's height
		public void OnViewportWidthChanged()
		{
			if( indexOfSelectedLogEntry >= entriesToShow.Count )
				return;

			CalculateSelectedLogEntryHeight();
			CalculateContentHeight();
			UpdateItemsInTheList( true );

			manager.ValidateScrollPosition();
		}

		// Log window's height has changed, update the list
		public void OnViewportHeightChanged()
		{
			UpdateItemsInTheList( false );
		}

		private void CalculateContentHeight()
		{
			float newHeight = Mathf.Max( 1f, entriesToShow.Count * logItemHeight );
			if( selectedLogEntry != null )
				newHeight += DeltaHeightOfSelectedLogEntry;

			transformComponent.sizeDelta = new Vector2( 0f, newHeight );
		}

		private void CalculateSelectedLogEntryHeight( DebugLogItem referenceItem = null )
		{
			if( !referenceItem )
			{
				if( visibleLogItems.Count == 0 )
				{
					UpdateItemsInTheList( false ); // Try to generate some DebugLogItems, we need one DebugLogItem to calculate the text height
					if( visibleLogItems.Count == 0 ) // No DebugLogItems are generated, weird
						return;
				}

				referenceItem = visibleLogItems[0];
			}

			heightOfSelectedLogEntry = referenceItem.CalculateExpandedHeight( selectedLogEntry, ( timestampsOfEntriesToShow != null ) ? timestampsOfEntriesToShow[indexOfSelectedLogEntry] : (DebugLogEntryTimestamp?) null );
		}

		// Calculate the indices of log entries to show
		// and handle log items accordingly
		private void UpdateItemsInTheList( bool updateAllVisibleItemContents )
		{
			if( entriesToShow.Count > 0 )
			{
				float contentPosTop = transformComponent.anchoredPosition.y - 1f;
				float contentPosBottom = contentPosTop + viewportTransform.rect.height + 2f;
				float positionOfSelectedLogEntry = indexOfSelectedLogEntry * logItemHeight;

				if( positionOfSelectedLogEntry <= contentPosBottom )
				{
					if( positionOfSelectedLogEntry <= contentPosTop )
					{
						contentPosTop = Mathf.Max( contentPosTop - DeltaHeightOfSelectedLogEntry, positionOfSelectedLogEntry - 1f );
						contentPosBottom = Mathf.Max( contentPosBottom - DeltaHeightOfSelectedLogEntry, contentPosTop + 2f );
					}
					else
						contentPosBottom = Mathf.Max( contentPosBottom - DeltaHeightOfSelectedLogEntry, positionOfSelectedLogEntry + 1f );
				}

				int newBottomIndex = Mathf.Min( (int) ( contentPosBottom / logItemHeight ), entriesToShow.Count - 1 );
				int newTopIndex = Mathf.Clamp( (int) ( contentPosTop / logItemHeight ), 0, newBottomIndex );

				if( currentTopIndex == -1 )
				{
					// There are no log items visible on screen,
					// just create the new log items
					updateAllVisibleItemContents = true;
					for( int i = 0, count = newBottomIndex - newTopIndex + 1; i < count; i++ )
						visibleLogItems.Add( manager.PopLogItem() );
				}
				else
				{
					// There are some log items visible on screen

					if( newBottomIndex < currentTopIndex || newTopIndex > currentBottomIndex )
					{
						// If user scrolled a lot such that, none of the log items are now within
						// the bounds of the scroll view, pool all the previous log items and create
						// new log items for the new list of visible debug entries
						updateAllVisibleItemContents = true;

						visibleLogItems.TrimStart( visibleLogItems.Count, poolLogItemAction );
						for( int i = 0, count = newBottomIndex - newTopIndex + 1; i < count; i++ )
							visibleLogItems.Add( manager.PopLogItem() );
					}
					else
					{
						// User did not scroll a lot such that, there are still some log items within
						// the bounds of the scroll view. Don't destroy them but update their content,
						// if necessary
						if( newTopIndex > currentTopIndex )
							visibleLogItems.TrimStart( newTopIndex - currentTopIndex, poolLogItemAction );

						if( newBottomIndex < currentBottomIndex )
							visibleLogItems.TrimEnd( currentBottomIndex - newBottomIndex, poolLogItemAction );

						if( newTopIndex < currentTopIndex )
						{
							for( int i = 0, count = currentTopIndex - newTopIndex; i < count; i++ )
								visibleLogItems.AddFirst( manager.PopLogItem() );

							// If it is not necessary to update all the log items,
							// then just update the newly created log items. Otherwise,
							// wait for the major update
							if( !updateAllVisibleItemContents )
								UpdateLogItemContentsBetweenIndices( newTopIndex, currentTopIndex - 1, newTopIndex );
						}

						if( newBottomIndex > currentBottomIndex )
						{
							for( int i = 0, count = newBottomIndex - currentBottomIndex; i < count; i++ )
								visibleLogItems.Add( manager.PopLogItem() );

							// If it is not necessary to update all the log items,
							// then just update the newly created log items. Otherwise,
							// wait for the major update
							if( !updateAllVisibleItemContents )
								UpdateLogItemContentsBetweenIndices( currentBottomIndex + 1, newBottomIndex, newTopIndex );
						}
					}
				}

				currentTopIndex = newTopIndex;
				currentBottomIndex = newBottomIndex;

				if( updateAllVisibleItemContents )
				{
					// Update all the log items
					UpdateLogItemContentsBetweenIndices( currentTopIndex, currentBottomIndex, newTopIndex );
				}
			}
			else if( currentTopIndex != -1 )
			{
				// There is nothing to show but some log items are still visible; pool them
				visibleLogItems.TrimStart( visibleLogItems.Count, poolLogItemAction );
				currentTopIndex = -1;
			}
		}

		private DebugLogItem GetLogItemAtIndex( int index )
		{
			return visibleLogItems[index - currentTopIndex];
		}

		private void UpdateLogItemContentsBetweenIndices( int topIndex, int bottomIndex, int logItemOffset )
		{
			for( int i = topIndex; i <= bottomIndex; i++ )
			{
				DebugLogItem logItem = visibleLogItems[i - logItemOffset];
				logItem.SetContent( entriesToShow[i], ( timestampsOfEntriesToShow != null ) ? timestampsOfEntriesToShow[i] : (DebugLogEntryTimestamp?) null, i, i == indexOfSelectedLogEntry );

				RepositionLogItem( logItem );
				ColorLogItem( logItem );

				if( isCollapseOn )
					logItem.ShowCount();
				else
					logItem.HideCount();
			}
		}

		private void RepositionLogItem( DebugLogItem logItem )
		{
			int index = logItem.Index;
			Vector2 anchoredPosition = new Vector2( 1f, -index * logItemHeight );
			if( index > indexOfSelectedLogEntry )
				anchoredPosition.y -= DeltaHeightOfSelectedLogEntry;

			logItem.Transform.anchoredPosition = anchoredPosition;
		}

		private void ColorLogItem( DebugLogItem logItem )
		{
			int index = logItem.Index;
			if( index == indexOfSelectedLogEntry )
				logItem.Image.color = logItemSelectedColor;
			else if( index % 2 == 0 )
				logItem.Image.color = logItemNormalColor1;
			else
				logItem.Image.color = logItemNormalColor2;
		}
	}
}