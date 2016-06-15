# In-game Debug Console for Unity 3D

![screenshot](https://yasirkula.files.wordpress.com/2016/06/ingamedebugconsolepng.png)

### A. ABOUT

This package is a simple console window for **Unity 3D** that helps you see debug messages (logs, errors, exceptions, warnings and assertions) in-game to easily debug your game. User interface is created with **uGUI** and costs 1 SetPass call (and 6 to 9 batches). It is possible to drag and resize the console window during the game.

### B. HOW TO

Simply drag & drop **DebugLogCanvas** prefab to the first scene of the game. If you don't need the console on every scene, you can deselect the *Singleton* property of the canvas so that it will be destroyed when scene changes.

### C. NOTES

- To customize a Log item's appearance properly, drag & drop **DebugLogItem** prefab to **DebugLogCanvas-DebugLogWindow-Debugs-Viewport-LogsContainer**. Don't forget to Apply your changes to DebugLogItem after you are done.

- If **2D Rect Mask** component does not exist in your version of Unity (*pre 5.2* I believe), replace it with **Mask** component (search for *Viewport* objects). Then change the color alpha value of the attached **Image** components to 1. Unfortunately, DebugLogCanvas will now cost more draw calls.

### D. LIMITATIONS

- This asset uses **2D Rect Mask** and thus the DebugLogCanvas can not be rendered in World Space but **only in Screen Space**.

### E. TO-DO

- Log item recycling to boost the performance (by reducing the number of Instantiate and Destroy calls drastically)
- Filtering logs by their type
