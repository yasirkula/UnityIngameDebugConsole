# In-game Debug Console for Unity 3D

![screenshot](Images/1.png)

**Forum Thread:** http://forum.unity3d.com/threads/in-game-debug-console-with-ugui-free.411323/

## ABOUT

This asset is a means to see debug messages (logs, warnings, errors, exceptions) runtime in a build (also assertions in editor). It also has a built-in console that allows you to enter commands and execute pre-defined functions in-game.

User interface is created with **uGUI** and costs **1 SetPass call** (and 6 to 10 batches). It is possible to drag, resize and hide the console window during the game. Once the console is hidden, a small popup will take its place (which is also draggable).

![popup](Images/2.png)

Console window is optimized using a customized recycled list view that calls Instantiate and Destroy functions sparingly. 

## HOW TO

Simply import **IngameDebugConsole.unitypackage** to your project and place **DebugLogCanvas** prefab to your scene. You may want to tweak the following settings:

- **Singleton**: if enabled, console window will persist between scenes (recommended). If, however, you don't want the console on each scene, then deselect this option and manually drag & drop the DebugLogCanvas prefab to the scenes you want
- **Launch In Popup Mode**:: if enabled, the console will be initialized as a popup
- **Clear Command After Execution**: if enabled, the command input field at the bottom of the console window will automatically be cleared after entering a command. If you want to spam a command, or make small tweaks to the previous command without having to write the whole command again, then deselect this option
- **Receive Logcat Logs In Android**: if enabled, on Android platform, logcat entries of the application will also be logged to the console with the prefix "*LOGCAT:*". This may come in handy especially if you want to access the native logs of your Android plugins (like *Admob*)
- **Logcat Arguments**: on Android, if Logcat logs are enabled, native logs will be filtered using these arguments. If left blank, all native logs of the application will be logged to the console. If you want to, for example, see Admob's logs only, you can enter **-s Ads** here

![dragdrop](Images/3.png)

You can move the console window around via the gizmo located at the top-center of the window. To scale the window, you can use the gizmo at the bottom right corner. To hide the console window completely, you can drag&drop it onto the little popup at the edge of the screen. You can also drag the popup to reposition it, or click the popup to show the console window again.

## COMMAND CONSOLE

### Executing Commands

You can enter commands using the input field at the bottom of the console. Initially, only the "*help*" command is available, which lists all the valid commands registered to the console.

A command is basically a function that can be called from the console via the command input field. This function can be **static** or an **instance function** (non static), in which case, a living instance is required to call the function. The return type of the function can be anything (including *void*). If the function returns an object, it will be printed to the console. The function can also take any number of parameters; the only restriction applies to the types of these parameters. Supported parameter types are:

**int, float, bool, string, Vector2, Vector3, Vector4**

To call a registered command, simply write down the command and then provide the necessary parameters. For example: 

`cube [0 2.5 0]`

To see the syntax of a command, see the help log:

`- cube: CubeCreator.CreateCubeAt(Vector3)`

Here, command is *cube* and the only necessary parameter is a *Vector3*. This command calls the *CreateCubeAt* function in the *CubeCreator* script (this demo script is not shipped with the asset).

The console uses a simple algorithm to parse the command input and has some strict restrictions:

- Put exactly one space character in-between parameters
- Don't put an f character after a float parameter
- Wrap strings with quotation marks ( " )
- Wrap vectors with square brackets ( [] ) or normal brackets ( () )
- Don't put space character after vector opening ( [ ) and/or before vector closing ( ] )

However, there is some flexibility in the syntax, as well:

- You can provide an empty vector to represent Vector_.zero: []
- You can enter a float as input to an int parameter. The fractional part will automatically be discarded
- You can enter 1 instead of true, or 0 instead of false

### Registering Custom Commands

If the parameters of a function meets the criteria mentioned above, you can register it into the console in two different ways:

- **Static Functions**

Use `DebugLogConsole.AddCommandStatic( string command, string methodName, System.Type ownerType )`. Here, **methodName** is the name of the method in string format, and **ownerType** is the type of the owner class. It may seem strange to provide the method name in string and/or provide the type of the class; however, after hours of research, I have found it the best way to register any function with any number of parameters and parameter types into the system without knowing the signature of the method.

```csharp
using UnityEngine;

public class CubeCreator : MonoBehaviour
{
	void Start()
	{
		DebugLogConsole.AddCommandStatic( "cube", "CreateCubeAt", typeof(
		CubeCreator ) );
	}
	
	public static void CreateCubeAt( Vector3 pos )
	{
		GameObject obj = GameObject.CreatePrimitive( PrimitiveType.Cube );
		obj.transform.position = pos;
	}
}
```

- **Instance Functions**

Use `DebugLogConsole.AddCommandInstance( string command, string methodName, object instance )`:

```csharp
using UnityEngine;

public class CubeCreator : MonoBehaviour
{
	void Start()
	{
		DebugLogConsole.AddCommandInstance( "cube", "CreateCubeAt", this );
	}
	
	void CreateCubeAt( Vector3 pos )
	{
		GameObject obj = GameObject.CreatePrimitive( PrimitiveType.Cube );
		obj.transform.position = pos;
	}
}
```

The only difference is that, you have to provide an actual instance of the class that owns the function, instead of the type of the class.

To remove a command, use `DebugLogConsole.RemoveCommand( string command )`.

## NOTES

- If **2D Rect Mask** component does not exist in your version of Unity (*pre 5.2*), replace it with **Mask** component (search for *Viewport* objects). Then change the color alpha value of the attached **Image** components to 1. Unfortunately, DebugLogCanvas will now cost more draw calls

## LIMITATIONS

- This asset uses **2D Rect Mask** and thus, the *DebugLogCanvas* can not be rendered in World Space but only in **Screen Space**