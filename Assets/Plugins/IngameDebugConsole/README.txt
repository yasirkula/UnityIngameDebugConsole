= In-game Debug Console =

Online documentation available at: https://github.com/yasirkula/UnityIngameDebugConsole
E-mail: yasirkula@gmail.com

1. ABOUT
This asset helps you see debug messages (logs, warnings, errors, exceptions) runtime in a build (also assertions in editor) and execute commands using its built-in console.

2. HOW TO
You can simply place the IngameDebugConsole prefab to your scene. You may want to tweak the following settings:

- Singleton: if enabled, console window will persist between scenes (recommended). If, however, you don't want the console on each scene, then deselect this option and manually drag & drop the DebugLogCanvas prefab to the scenes you want
- Enable Popup: if disabled, no popup will be shown when the console window is hidden
- Start In Popup Mode: if enabled, console will be initialized as a popup
- Toggle With Key: if enabled, pressing the Toggle Key will show/hide (i.e. toggle) the console window at runtime
- Enable Searchbar: if enabled, the console window will have a searchbar
- Top Searchbar Min Width: if searchbar is enabled, width of the canvas determines whether the searchbar will be located inside the menu bar or underneath the menu bar. This way, the menu bar doesn't get too crowded on narrow screens. This value determines the minimum width of the canvas for the searchbar to appear inside the menu bar
- Clear Command After Execution: if enabled, the command input field at the bottom of the console window will automatically be cleared after entering a command. If you want to spam a command, or make small tweaks to the previous command without having to write the whole command again, then deselect this option
- Command History Size: console keeps track of the previously entered commands, this value determines how many will be remembered (you can scroll through the history via up and down arrow keys while the command input field is focused)
- Receive Logcat Logs In Android: if enabled, on Android platform, logcat entries of the application will also be logged to the console with the prefix "LOGCAT:". This may come in handy especially if you want to access the native logs of your Android plugins (like Admob)
- Logcat Arguments: on Android, if Logcat logs are enabled, native logs will be filtered using these arguments. If left blank, all native logs of the application will be logged to the console. If you want to, for example, see Admob's logs only, you can enter -s Ads here
- Avoid Screen Cutout: if enabled, on Android and iOS devices with notch screens, the console window will be repositioned so that the cutout(s) don't obscure it
- Max Log Length: if a log is longer than this limit, it will be truncated. This helps avoid reaching Unity's 65000 vertex limit for UI canvases