using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using Pointer = UnityEngine.InputSystem.Pointer;
#endif

namespace IngameDebugConsole
{
    public class CommandInputField : TMP_InputField
    {
        // ===============================================================================================================================================================================
        // NOTE: To assign values to serialized variables, unfortunately we need to set the Inspector to Debug Mode because the inspector is overridden by TMP_InputField's custom editor.
        // ===============================================================================================================================================================================

        [SerializeField]
        private RectTransform commandSuggestionsContainer;
        [SerializeField]
        private TextMeshProUGUI commandSuggestionPrefab;

        [SerializeField]
        private string commandSuggestionHighlightStart = "<color=orange>";
        [SerializeField]
        private string commandSuggestionHighlightEnd = "</color>";

        private DebugLogManager manager;

        /// <summary>Command suggestions that match the currently entered command.</summary>
        private List<ConsoleMethodInfo> matchingCommandSuggestions;
        private List<TextMeshProUGUI> commandSuggestionInstances;
        private int visibleCommandSuggestionInstances = 0;

        private List<int> commandCaretIndexIncrements;

        private string previousCommand;
        private string previousCommandName;
        private int previousParameterCount = -1;
        private int previousCaretPosition = -1;
        private int previousCaretArgumentIndex = -1;

        /// <summary>Value of the command input field when autocomplete was first requested.</summary>
        private string autoCompleteBase;
        /// <summary><c>true</c> immediately after the user presses Tab to initiate auto-complete.</summary>
        private bool hasAutoCompletedNow;

        /// <summary>History of the previously entered commands.</summary>
        private CircularBuffer<string> commandHistory;
        private int commandHistoryIndex = -1;
        private string commandBeforeNavigatingHistory;

        private readonly Event poppedEvent = new Event();

        private delegate object FieldInfoGetDelegate(object obj);
        private delegate void FieldInfoSetDelegate(object obj, object value);
        private readonly FieldInfoGetDelegate m_IsCompositionActiveGetter = typeof(TMP_InputField).GetField("m_IsCompositionActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue;
        private readonly FieldInfoSetDelegate m_IsTextComponentUpdateRequiredSetter = typeof(TMP_InputField).GetField("m_IsTextComponentUpdateRequired", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue;
        private readonly object boxedTrueValue = true;

        public void Initialize(DebugLogManager manager)
        {
            this.manager = manager;

            commandSuggestionInstances = new List<TextMeshProUGUI>(8);
            matchingCommandSuggestions = new List<ConsoleMethodInfo>(8);
            commandCaretIndexIncrements = new List<int>(8);
            commandHistory = new CircularBuffer<string>(manager.commandHistorySize);

            commandSuggestionsContainer.gameObject.SetActive(false);

            onValidateInput += OnValidateCommand;
            onValueChanged.AddListener(OnEditCommand);
            onEndEdit.AddListener(OnEndEditCommand);
            onSubmit.AddListener(OnSubmitCommand);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            if (!manager.IsLogWindowVisible)
                return;

            if (manager.showCommandSuggestions && isFocused && caretPosition != previousCaretPosition)
                RefreshCommandSuggestions(text);

            if (isFocused && commandHistory.Count > 0)
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                if (Keyboard.current != null && Keyboard.current[Key.UpArrow].wasPressedThisFrame)
#else
                if (Input.GetKeyDown(KeyCode.UpArrow))
#endif
                {
                    if (commandHistoryIndex == -1)
                    {
                        commandHistoryIndex = commandHistory.Count - 1;
                        commandBeforeNavigatingHistory = text;
                    }
                    else if (--commandHistoryIndex < 0)
                        commandHistoryIndex = 0;

                    text = commandHistory[commandHistoryIndex];
                    caretPosition = text.Length;
                }
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                else if (Keyboard.current != null && Keyboard.current[Key.DownArrow].wasPressedThisFrame && commandHistoryIndex != -1)
#else
                else if (Input.GetKeyDown(KeyCode.DownArrow) && commandHistoryIndex != -1)
#endif
                {
                    if (++commandHistoryIndex < commandHistory.Count)
                        text = commandHistory[commandHistoryIndex];
                    else
                    {
                        commandHistoryIndex = -1;
                        text = commandBeforeNavigatingHistory ?? string.Empty;
                    }
                }
            }
        }

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
                        if (poppedEvent.character == 0 && poppedEvent.modifiers == EventModifiers.None && caretPositionInternal == m_CaretPosition && (bool)m_IsCompositionActiveGetter(this))
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

        // Command field input is changed, check if command is submitted
        private char OnValidateCommand(string command, int charIndex, char addedChar)
        {
            if (addedChar == '\t') // Autocomplete attempt
            {
                if (!string.IsNullOrEmpty(command))
                {
                    if (string.IsNullOrEmpty(autoCompleteBase))
                        autoCompleteBase = command;

                    string autoCompletedCommand = DebugLogConsole.GetAutoCompleteCommand(autoCompleteBase, command);
                    if (!string.IsNullOrEmpty(autoCompletedCommand) && autoCompletedCommand != command)
                    {
                        hasAutoCompletedNow = true;
                        text = autoCompletedCommand;
                        stringPosition = autoCompletedCommand.Length;
                    }
                }

                return '\0';
            }
            else if (addedChar == '\n') // Command is submitted
            {
                OnSubmitCommand(command);
                return '\0';
            }

            return addedChar;
        }

        // Command input field's text has changed
        private void OnEditCommand(string command)
        {
            RefreshCommandSuggestions(command);

            if (!hasAutoCompletedNow)
                autoCompleteBase = null;
            else // This change was caused by autocomplete
                hasAutoCompletedNow = false;
        }

        // Command input field has lost focus
        private void OnEndEditCommand(string command)
        {
            if (!commandSuggestionsContainer.gameObject.activeSelf)
                return;

            // Check if any command suggestion is clicked
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (visibleCommandSuggestionInstances > 0 && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
#else
            if (visibleCommandSuggestionInstances > 0 && Input.GetMouseButtonDown(0))
#endif
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                Vector2 pointerPosition = Pointer.current.position.ReadValue();
#else
                Vector2 pointerPosition = Input.mousePosition;
#endif

                Canvas canvas = textComponent.canvas;
                Camera canvasCamera = (canvas.renderMode == RenderMode.ScreenSpaceOverlay || (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)) ? null : (canvas.worldCamera != null) ? canvas.worldCamera : Camera.main;
                if (RectTransformUtility.RectangleContainsScreenPoint(commandSuggestionsContainer, pointerPosition, canvasCamera) && RectTransformUtility.ScreenPointToLocalPointInRectangle(commandSuggestionsContainer, pointerPosition, canvasCamera, out Vector2 localPoint))
                {
                    /// <see cref="commandSuggestionInstances"/> have their Pivot Y set to 1 so we need localPoint to have the same pivot value.
                    localPoint.y -= commandSuggestionsContainer.rect.height;

                    for (int i = 0; i < visibleCommandSuggestionInstances; i++)
                    {
                        if (localPoint.y >= commandSuggestionInstances[i].rectTransform.anchoredPosition.y - commandSuggestionInstances[i].rectTransform.sizeDelta.y * commandSuggestionInstances[i].rectTransform.pivot.y)
                        {
                            text = matchingCommandSuggestions[i].command + ((matchingCommandSuggestions[i].parameters.Length > 0) ? " " : null);
                            StartCoroutine(ActivateCommandInputFieldCoroutine());
                            return;
                        }
                    }
                }
            }

            commandSuggestionsContainer.gameObject.SetActive(false);
        }

        // Command input field has been submitted
        private void OnSubmitCommand(string command)
        {
            // Clear the command field
            if (manager.clearCommandAfterExecution)
                text = string.Empty;

            if (command.Length > 0)
            {
                if (commandHistory.Count == 0 || commandHistory[commandHistory.Count - 1] != command)
                    commandHistory.Add(command);

                commandHistoryIndex = -1;
                commandBeforeNavigatingHistory = null;

                // Execute the command
                DebugLogConsole.ExecuteCommand(command);

                // Snap to bottom and select the latest entry
                manager.SnapToBottom = true;
            }
        }

        // Show suggestions for the currently entered command
        private void RefreshCommandSuggestions(string command)
        {
            if (!manager.showCommandSuggestions)
                return;

            previousCaretPosition = caretPosition;

            // Don't recalculate the command suggestions if the input command hasn't changed (i.e. only caret's position has changed)
            bool commandChanged = command != previousCommand;
            bool commandNameOrParametersChanged = false;
            if (commandChanged)
            {
                previousCommand = command;

                matchingCommandSuggestions.Clear();
                commandCaretIndexIncrements.Clear();

                string prevCommandName = previousCommandName;
                DebugLogConsole.GetCommandSuggestions(command, matchingCommandSuggestions, commandCaretIndexIncrements, ref previousCommandName, out int numberOfParameters);
                if (prevCommandName != previousCommandName || numberOfParameters != previousParameterCount)
                {
                    previousParameterCount = numberOfParameters;
                    commandNameOrParametersChanged = true;
                }
            }

            int caretArgumentIndex = 0;
            int caretPos = caretPosition;
            for (int i = 0; i < commandCaretIndexIncrements.Count && caretPos > commandCaretIndexIncrements[i]; i++)
                caretArgumentIndex++;

            if (caretArgumentIndex != previousCaretArgumentIndex)
                previousCaretArgumentIndex = caretArgumentIndex;
            else if (!commandChanged || !commandNameOrParametersChanged)
            {
                // Command suggestions don't need to be updated if:
                // a) neither the entered command nor the argument that the caret is hovering has changed
                // b) entered command has changed but command's name hasn't changed, parameter count hasn't changed and the argument
                //    that the caret is hovering hasn't changed (i.e. user has continued typing a parameter's value)
                return;
            }

            if (matchingCommandSuggestions.Count == 0)
                OnEndEditCommand(command);
            else
            {
                if (!commandSuggestionsContainer.gameObject.activeSelf)
                    commandSuggestionsContainer.gameObject.SetActive(true);

                int suggestionInstancesCount = commandSuggestionInstances.Count;
                int suggestionsCount = matchingCommandSuggestions.Count;

                for (int i = 0; i < suggestionsCount; i++)
                {
                    if (i >= visibleCommandSuggestionInstances)
                    {
                        if (i >= suggestionInstancesCount)
                            commandSuggestionInstances.Add(Instantiate(commandSuggestionPrefab, commandSuggestionsContainer, false));
                        else
                            commandSuggestionInstances[i].gameObject.SetActive(true);

                        visibleCommandSuggestionInstances++;
                    }

                    ConsoleMethodInfo suggestedCommand = matchingCommandSuggestions[i];
                    StringBuilder sb = manager.sharedStringBuilder.Clear();
                    if (caretArgumentIndex > 0)
                        sb.Append(suggestedCommand.command);
                    else
                        sb.Append(commandSuggestionHighlightStart).Append(matchingCommandSuggestions[i].command).Append(commandSuggestionHighlightEnd);

                    if (suggestedCommand.parameters.Length > 0)
                    {
                        sb.Append(" ");

                        // If the command name wasn't highlighted, a parameter must always be highlighted
                        int caretParameterIndex = caretArgumentIndex - 1;
                        if (caretParameterIndex >= suggestedCommand.parameters.Length)
                            caretParameterIndex = suggestedCommand.parameters.Length - 1;

                        for (int j = 0; j < suggestedCommand.parameters.Length; j++)
                        {
                            if (caretParameterIndex != j)
                                sb.Append(suggestedCommand.parameters[j]);
                            else
                                sb.Append(commandSuggestionHighlightStart).Append(suggestedCommand.parameters[j]).Append(commandSuggestionHighlightEnd);
                        }
                    }

                    commandSuggestionInstances[i].text = sb.ToString();
                }

                for (int i = visibleCommandSuggestionInstances - 1; i >= suggestionsCount; i--)
                    commandSuggestionInstances[i].gameObject.SetActive(false);

                visibleCommandSuggestionInstances = suggestionsCount;
            }
        }

        public IEnumerator ActivateCommandInputFieldCoroutine()
        {
            yield return null;

            /// Don't select the text during this automated activation of <see cref="TMP_InputField"/> because it's distracting.
            bool onFocusSelectAll = this.onFocusSelectAll;
            this.onFocusSelectAll = false;

            ActivateInputField();

            /// Wait for <see cref="TMP_InputField.LateUpdate"/> because input field's activation is handled there.
            yield return null;

            MoveTextEnd(false);
            this.onFocusSelectAll = onFocusSelectAll;
        }
    }
}
