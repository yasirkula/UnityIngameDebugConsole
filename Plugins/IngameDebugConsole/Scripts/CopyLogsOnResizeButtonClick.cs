using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IngameDebugConsole
{
    public class CopyLogsOnResizeButtonClick : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private int maxLogCount = int.MaxValue;
        [SerializeField]
        private float maxElapsedTime = float.PositiveInfinity;

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging && eventData.eligibleForClick && DebugLogManager.Instance.copyAllLogsOnResizeButtonClick)
            {
                GUIUtility.systemCopyBuffer = DebugLogManager.Instance.GetAllLogs(maxLogCount, maxElapsedTime);
                StartCoroutine(ScaleAnimationCoroutine());
            }
        }

        private IEnumerator ScaleAnimationCoroutine()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 3f)
            {
                transform.localScale = Vector3.one * (1f + Mathf.PingPong(t, 0.5f));
                yield return null;
            }

            transform.localScale = Vector3.one;
        }
    }
}
