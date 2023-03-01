using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IngameDebugConsole
{
    public class SuggestionScrollResizer : MonoBehaviour
    {
        [SerializeField] private RectTransform myRect = null;
        [SerializeField] private RectTransform content = null;
        [SerializeField] private Vector2 offset = new Vector2();

        [Space]
        [SerializeField] private Vector2 maxSize = new Vector2();

        private Vector2 targetSize;

        private void Update()
        {
            if (myRect && content)
            {
                targetSize = content.sizeDelta + offset;

                targetSize.x = Mathf.Clamp(targetSize.x, 0, maxSize.x);
                targetSize.y = Mathf.Clamp(targetSize.y, 0, maxSize.y);

                myRect.sizeDelta = targetSize;
            }
        }
    }
}