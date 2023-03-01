using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace IngameDebugConsole
{
    public class SuggestionController : MonoBehaviour
    {
        [SerializeField] private Text suggestionText = null;

        public UnityEvent OnSuggestionHandler;

        public void SetText(string _text)
        {
            suggestionText.text = _text;
        }

        public void EventTriggerCallback()
        {
            OnSuggestionHandler?.Invoke();
        }
    }
}