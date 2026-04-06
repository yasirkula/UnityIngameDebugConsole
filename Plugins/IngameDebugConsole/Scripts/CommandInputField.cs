using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IngameDebugConsole
{
    public class CommandInputField : TMP_InputField
    {
        private readonly Event poppedEvent = new Event();

        private int compositionLength => caretPositionInternal - m_CaretPosition;

        private delegate object FieldInfoGetDelegate(object obj);
        private readonly FieldInfoGetDelegate m_IsCompositionActiveGetter = typeof(TMP_InputField).GetField("m_IsCompositionActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue;

        private delegate void FieldInfoSetDelegate(object obj, object value);
        private readonly FieldInfoSetDelegate m_IsTextComponentUpdateRequiredSetter = typeof(TMP_InputField).GetField("m_IsTextComponentUpdateRequired", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue;
        private readonly object boxedTrueValue = (bool)true;

        /// <summary>
        /// Copy & paste of <see cref="TMP_InputField.OnUpdateSelected"/> except it doesn't remove focus when '\n' or '\t' characters are pressed
        /// (behaves the same as <see cref="TMP_InputField.LineType.MultiLineNewline"/> while in <see cref="TMP_InputField.LineType.SingleLine"/>).
        /// </summary>
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            if (!isFocused)
                return;

            bool consumedEvent = false;
            while (Event.PopEvent(poppedEvent))
            {
                switch (poppedEvent.rawType)
                {
                    case EventType.KeyDown:
                    {
                        consumedEvent = true;

                        // Special handling on OSX which produces more events which need to be suppressed.
                        if (poppedEvent.character == 0 && poppedEvent.modifiers == EventModifiers.None && compositionLength == 0 && (bool)m_IsCompositionActiveGetter(this))
                            break;

                        char ch = poppedEvent.keyCode switch
                        {
                            KeyCode.Return or KeyCode.KeypadEnter => '\n',
                            KeyCode.Tab => '\t',
                            _ => poppedEvent.character,
                        };

                        if (ch == '\t' || ch == '\n')
                            Append(ch);
                        else if (KeyPressed(poppedEvent) == EditState.Finish)
                        {
                            if (!wasCanceled)
                                SendOnSubmit();

                            DeactivateInputField();
                            break;
                        }

                        m_IsTextComponentUpdateRequiredSetter(this, boxedTrueValue);
                        UpdateLabel();

                        break;
                    }
                    case EventType.ValidateCommand:
                    case EventType.ExecuteCommand:
                    {
                        if (poppedEvent.commandName == "SelectAll")
                        {
                            SelectAll();
                            consumedEvent = true;
                        }

                        break;
                    }
                }
            }

            if (consumedEvent)
                UpdateLabel();

            eventData.Use();
        }
    }
}
