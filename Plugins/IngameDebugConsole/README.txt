= In-game Debug Console (v1.5.9) =

Online documentation available at: https://github.com/yasirkula/UnityIngameDebugConsole
E-mail: yasirkula@gmail.com

### ABOUT
This asset helps you see debug messages (logs, warnings, errors, exceptions) runtime in a build (also assertions in editor) and execute commands using its built-in console.


### HOW TO
You can simply place the IngameDebugConsole prefab to your scene. Hovering the cursor over its properties in the Inspector will reveal explanatory tooltips.


### NEW INPUT SYSTEM SUPPORT
This plugin supports Unity's new Input System but it requires some manual modifications (if both the legacy and the new input systems are active at the same time, no changes are needed):

- the plugin mustn't be installed as a package, i.e. it must reside inside the Assets folder and not the Packages folder (it can reside inside a subfolder of Assets like Assets/Plugins)
- if Unity 2019.2.5 or earlier is used, add ENABLE_INPUT_SYSTEM compiler directive to "Player Settings/Scripting Define Symbols" (these symbols are platform specific, so if you change the active platform later, you'll have to add the compiler directive again)
- add "Unity.InputSystem" assembly to "IngameDebugConsole.Runtime" Assembly Definition File's "Assembly Definition References" list
- open IngameDebugConsole prefab, select EventSystem child object and replace StandaloneInputModule component with InputSystemUIInputModule component (or, if your scene(s) already have EventSystem objects, you can delete IngameDebugConsole prefab's EventSystem child object)