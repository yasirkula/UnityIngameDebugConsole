# HOW TO

- Simply drag & drop DebugLogCanvas prefab to the first scene of the game. If you don't need the console on every scene, 
you can deselect the "Singleton" property of the canvas so that it will be destroyed when scene changes.

- To hide the log window in-game, drag & drop it onto the debug popup (that is visible while the window is being dragged).

# NOTES

- If 2D Rect Mask component does not exist in your version of Unity (pre 5.2 I believe), replace it with Mask component 
(search for "Viewport" objects). Then change the color alpha value of the attached Image components to 1. Unfortunately, 
DebugLogCanvas will now cost more draw calls.

# LIMITATIONS

- This asset uses 2D Rect Mask and thus the DebugLogCanvas can not be rendered in World Space but only in Screen Space.

# CREDITS

- Suleyman Yasir Kula (yasirkula@yahoo.com)