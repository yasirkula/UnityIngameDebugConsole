using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

namespace IngameDebugConsole
{
    public static class InputHelper
    {
        public static bool GetKeyDown(KeyCode key)
        {
            bool ok = Enum.TryParse(key.ToString(), out Key newKey);
            return ok && Keyboard.current[newKey].wasPressedThisFrame;
        }
    }
}
#elif ENABLE_LEGACY_INPUT_MANAGER
namespace IngameDebugConsole
{
    public static class InputHelper
    {
        public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
    }
}
#endif