using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace IngameDebugConsole
{
    public static class InputHelper
    {
        private static readonly Dictionary<KeyCode, Key> KeyTranslationMap = new Dictionary<KeyCode, Key> {
            {KeyCode.None, Key.None},
            {KeyCode.A, Key.A},
            {KeyCode.B, Key.B},
            {KeyCode.C, Key.C},
            {KeyCode.D, Key.D},
            {KeyCode.E, Key.E},
            {KeyCode.F, Key.F},
            {KeyCode.G, Key.G},
            {KeyCode.H, Key.H},
            {KeyCode.I, Key.I},
            {KeyCode.J, Key.J},
            {KeyCode.K, Key.K},
            {KeyCode.L, Key.L},
            {KeyCode.M, Key.M},
            {KeyCode.N, Key.N},
            {KeyCode.O, Key.O},
            {KeyCode.P, Key.P},
            {KeyCode.Q, Key.Q},
            {KeyCode.R, Key.R},
            {KeyCode.S, Key.S},
            {KeyCode.T, Key.T},
            {KeyCode.U, Key.U},
            {KeyCode.V, Key.V},
            {KeyCode.W, Key.W},
            {KeyCode.X, Key.X},
            {KeyCode.Y, Key.Y},
            {KeyCode.Z, Key.Z},
            {KeyCode.F1, Key.F1},
            {KeyCode.F2, Key.F2},
            {KeyCode.F3, Key.F3},
            {KeyCode.F4, Key.F4},
            {KeyCode.F5, Key.F5},
            {KeyCode.F6, Key.F6},
            {KeyCode.F7, Key.F7},
            {KeyCode.F8, Key.F8},
            {KeyCode.F9, Key.F9},
            {KeyCode.F10, Key.F10},
            {KeyCode.F11, Key.F11},
            {KeyCode.F12, Key.F12},
            {KeyCode.End, Key.End},
            {KeyCode.Backspace, Key.Backspace},
            {KeyCode.Delete, Key.Delete},
            {KeyCode.Tab, Key.Tab},
            {KeyCode.Return, Key.Enter},
            {KeyCode.Pause, Key.Pause},
            {KeyCode.Escape, Key.Escape},
            {KeyCode.Space, Key.Space},
            {KeyCode.Keypad0, Key.Numpad0},
            {KeyCode.Keypad1, Key.Numpad1},
            {KeyCode.Keypad2, Key.Numpad2},
            {KeyCode.Keypad3, Key.Numpad3},
            {KeyCode.Keypad4, Key.Numpad4},
            {KeyCode.Keypad5, Key.Numpad5},
            {KeyCode.Keypad6, Key.Numpad6},
            {KeyCode.Keypad7, Key.Numpad7},
            {KeyCode.Keypad8, Key.Numpad8},
            {KeyCode.Keypad9, Key.Numpad9},
            {KeyCode.KeypadPeriod, Key.NumpadPeriod},
            {KeyCode.KeypadDivide, Key.NumpadDivide},
            {KeyCode.KeypadMultiply, Key.NumpadMultiply},
            {KeyCode.KeypadMinus, Key.NumpadMinus},
            {KeyCode.KeypadPlus, Key.NumpadPlus},
            {KeyCode.KeypadEnter, Key.NumpadEnter},
            {KeyCode.KeypadEquals, Key.NumpadEquals},
            {KeyCode.UpArrow, Key.UpArrow},
            {KeyCode.DownArrow, Key.DownArrow},
            {KeyCode.RightArrow, Key.RightArrow},
            {KeyCode.LeftArrow, Key.LeftArrow},
            {KeyCode.Insert, Key.Insert},
            {KeyCode.Home, Key.Home},
            {KeyCode.PageUp, Key.PageUp},
            {KeyCode.PageDown, Key.PageDown}
        };
        
        public static bool GetKeyDown(KeyCode key)
            => KeyTranslationMap.ContainsKey(key) && Keyboard.current[KeyTranslationMap[key]].wasPressedThisFrame;
    }
}
#else
namespace IngameDebugConsole
{
    public static class InputHelper
    {
        public static bool GetKeyDown(KeyCode key) 
            => Input.GetKeyDown(key);
    }
}
#endif