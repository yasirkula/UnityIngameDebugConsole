using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Listens to scroll events on the scroll rect that debug items are stored
// and decides whether snap to bottom should be true or not
// 
// Procedure: if, after a user input (drag or scroll), scrollbar is at the bottom, then 
// snap to bottom shall be true, otherwise it shall be false
namespace IngameDebugConsole
{
	public class DebugsOnScrollListener : MonoBehaviour, IScrollHandler, IBeginDragHandler, IEndDragHandler
	{
		public ScrollRect debugsScrollRect;
		public DebugLogManager debugLogManager;

		public void OnScroll( PointerEventData data )
		{
			debugLogManager.SnapToBottom = IsScrollbarAtBottom();
		}

		public void OnBeginDrag( PointerEventData data )
		{
			debugLogManager.SnapToBottom = false;
		}

		public void OnEndDrag( PointerEventData data )
		{
			debugLogManager.SnapToBottom = IsScrollbarAtBottom();
		}

		public void OnScrollbarDragStart( BaseEventData data )
		{
			debugLogManager.SnapToBottom = false;
		}

		public void OnScrollbarDragEnd( BaseEventData data )
		{
			debugLogManager.SnapToBottom = IsScrollbarAtBottom();
		}

		private bool IsScrollbarAtBottom()
		{
			return debugsScrollRect.verticalNormalizedPosition <= 1E-6f;
		}
	}
}