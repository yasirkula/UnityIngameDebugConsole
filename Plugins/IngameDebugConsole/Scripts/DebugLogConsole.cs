#if UNITY_EDITOR || UNITY_STANDALONE
// Unity's Text component doesn't render <b> tag correctly on mobile devices
#define USE_BOLD_COMMAND_SIGNATURES
#endif

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Object = UnityEngine.Object;
#if UNITY_EDITOR && UNITY_2021_1_OR_NEWER
using SystemInfo = UnityEngine.Device.SystemInfo; // To support Device Simulator on Unity 2021.1+
#endif

// Manages the console commands, parses console input and handles execution of commands
// Supported method parameter types: int, float, bool, string, Vector2, Vector3, Vector4

// Helper class to store important information about a command
namespace IngameDebugConsole
{
	public class ConsoleMethodInfo
	{
		public readonly MethodInfo method;
		public readonly Type[] parameterTypes;
		public readonly object instance;

		public readonly string command;
		public readonly string signature;
		public readonly string[] parameters;

		public ConsoleMethodInfo( MethodInfo method, Type[] parameterTypes, object instance, string command, string signature, string[] parameters )
		{
			this.method = method;
			this.parameterTypes = parameterTypes;
			this.instance = instance;
			this.command = command;
			this.signature = signature;
			this.parameters = parameters;
		}

		public bool IsValid()
		{
			if( !method.IsStatic && ( instance == null || instance.Equals( null ) ) )
				return false;

			return true;
		}
	}

	public static class DebugLogConsole
	{
		public delegate bool ParseFunction( string input, out object output );

		public delegate void CommandExecutedDelegate( string command, object[] parameters );
		public static event CommandExecutedDelegate OnCommandExecuted;

		// All the commands
		private static readonly List<ConsoleMethodInfo> methods = new List<ConsoleMethodInfo>();
		private static readonly List<ConsoleMethodInfo> matchingMethods = new List<ConsoleMethodInfo>( 4 );

		// All the parse functions
		private static readonly Dictionary<Type, ParseFunction> parseFunctions = new Dictionary<Type, ParseFunction>()
		{
			{ typeof( string ), ParseString },
			{ typeof( bool ), ParseBool },
			{ typeof( int ), ParseInt },
			{ typeof( uint ), ParseUInt },
			{ typeof( long ), ParseLong },
			{ typeof( ulong ), ParseULong },
			{ typeof( byte ), ParseByte },
			{ typeof( sbyte ), ParseSByte },
			{ typeof( short ), ParseShort },
			{ typeof( ushort ), ParseUShort },
			{ typeof( char ), ParseChar },
			{ typeof( float ), ParseFloat },
			{ typeof( double ), ParseDouble },
			{ typeof( decimal ), ParseDecimal },
			{ typeof( Vector2 ), ParseVector2 },
			{ typeof( Vector3 ), ParseVector3 },
			{ typeof( Vector4 ), ParseVector4 },
			{ typeof( Quaternion ), ParseQuaternion },
			{ typeof( Color ), ParseColor },
			{ typeof( Color32 ), ParseColor32 },
			{ typeof( Rect ), ParseRect },
			{ typeof( RectOffset ), ParseRectOffset },
			{ typeof( Bounds ), ParseBounds },
			{ typeof( GameObject ), ParseGameObject },
#if UNITY_2017_2_OR_NEWER
			{ typeof( Vector2Int ), ParseVector2Int },
			{ typeof( Vector3Int ), ParseVector3Int },
			{ typeof( RectInt ), ParseRectInt },
			{ typeof( BoundsInt ), ParseBoundsInt },
#endif
		};

		// All the readable names of accepted types
		private static readonly Dictionary<Type, string> typeReadableNames = new Dictionary<Type, string>()
		{
			{ typeof( string ), "String" },
			{ typeof( bool ), "Boolean" },
			{ typeof( int ), "Integer" },
			{ typeof( uint ), "Unsigned Integer" },
			{ typeof( long ), "Long" },
			{ typeof( ulong ), "Unsigned Long" },
			{ typeof( byte ), "Byte" },
			{ typeof( sbyte ), "Short Byte" },
			{ typeof( short ), "Short" },
			{ typeof( ushort ), "Unsigned Short" },
			{ typeof( char ), "Char" },
			{ typeof( float ), "Float" },
			{ typeof( double ), "Double" },
			{ typeof( decimal ), "Decimal" }
		};

		// Split arguments of an entered command
		private static readonly List<string> commandArguments = new List<string>( 8 );

		// Command parameter delimeter groups
		private static readonly string[] inputDelimiters = new string[] { "\"\"", "''", "{}", "()", "[]" };

		// CompareInfo used for case-insensitive command name comparison
		internal static readonly CompareInfo caseInsensitiveComparer = new CultureInfo( "en-US" ).CompareInfo;

		static DebugLogConsole()
		{
#if !IDG_DISABLE_HELP_COMMAND
			AddCommand( "help", "Prints all commands", LogAllCommands );
			AddCommand<string>( "help", "Prints all matching commands", LogAllCommandsWithName );
#endif
#if IDG_ENABLE_HELPER_COMMANDS || IDG_ENABLE_SYSINFO_COMMAND
			AddCommand( "sysinfo", "Prints system information", LogSystemInfo );
#endif

#if UNITY_EDITOR || !NETFX_CORE
			// Find all [ConsoleMethod] functions
			// Don't search built-in assemblies for console methods since they can't have any
			string[] ignoredAssemblies = new string[]
			{
				"Unity",
				"System",
				"Mono.",
				"mscorlib",
				"netstandard",
				"TextMeshPro",
				"Microsoft.GeneratedCode",
				"I18N",
				"Boo.",
				"UnityScript.",
				"ICSharpCode.",
				"ExCSS.Unity",
#if UNITY_EDITOR
				"Assembly-CSharp-Editor",
				"Assembly-UnityScript-Editor",
				"nunit.",
				"SyntaxTree.",
				"AssetStoreTools",
#endif
			};
#endif

#if UNITY_EDITOR || !NETFX_CORE
			foreach( Assembly assembly in AppDomain.CurrentDomain.GetAssemblies() )
#else
			foreach( Assembly assembly in new Assembly[] { typeof( DebugLogConsole ).Assembly } ) // On UWP, at least search this plugin's Assembly for console methods
#endif
			{
#if( NET_4_6 || NET_STANDARD_2_0 ) && ( UNITY_EDITOR || !NETFX_CORE )
				if( assembly.IsDynamic )
					continue;
#endif


#if UNITY_EDITOR || !NETFX_CORE
				string assemblyName = assembly.GetName().Name;
				bool ignoreAssembly = false;
				for( int i = 0; i < ignoredAssemblies.Length; i++ )
				{
					if( caseInsensitiveComparer.IsPrefix( assemblyName, ignoredAssemblies[i], CompareOptions.IgnoreCase ) )
					{
						ignoreAssembly = true;
						break;
					}
				}

				if( ignoreAssembly )
					continue;
#endif

				SearchAssemblyForConsoleMethods( assembly );
			}
		}

		public static void SearchAssemblyForConsoleMethods( Assembly assembly )
		{
			try
			{
				List<ConsoleAttribute> methods = new List<ConsoleAttribute>();
				foreach( Type type in assembly.GetExportedTypes() )
				{
					foreach( MethodInfo method in type.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly ) )
					{
						foreach( ConsoleAttribute consoleAttribute in method.GetCustomAttributes( typeof(ConsoleAttribute), false ) )
						{
							consoleAttribute.SetMethod(method);
							methods.Add(consoleAttribute);
						}
					}
				}

				methods.Sort((a, b) => a.Order.CompareTo(b.Order));
				for (int i = 0; i < methods.Count; i++)
				{
					methods[i].Load();
				}
			}
			catch( NotSupportedException ) { }
			catch( System.IO.FileNotFoundException ) { }
			catch( ReflectionTypeLoadException ) { }
			catch( Exception e )
			{
				Debug.LogError( "Couldn't search assembly for [ConsoleMethod] attributes: " + assembly.GetName().Name + "\n" + e.ToString() );
			}
		}

		public static List<ConsoleMethodInfo> GetAllCommands()
		{
			return methods;
		}

		// Logs the list of available commands
		public static void LogAllCommands()
		{
			int length = 25;
			for( int i = 0; i < methods.Count; i++ )
			{
				if( methods[i].IsValid() )
					length += methods[i].signature.Length + 7;
			}

			StringBuilder stringBuilder = new StringBuilder( length );
			stringBuilder.Append( "Available commands:" );

			for( int i = 0; i < methods.Count; i++ )
			{
				if( methods[i].IsValid() )
					stringBuilder.Append( "\n    - " ).Append( methods[i].signature );
			}

			Debug.Log( stringBuilder.ToString() );

			// After typing help, the log that lists all the commands should automatically be expanded for better UX
			if( DebugLogManager.Instance )
				DebugLogManager.Instance.AdjustLatestPendingLog( true, true );
		}

		// Logs the list of available commands that are either equal to commandName or contain commandName as substring
		public static void LogAllCommandsWithName( string commandName )
		{
			matchingMethods.Clear();

			// First, try to find commands that exactly match the commandName. If there are no such commands, try to find
			// commands that contain commandName as substring
			FindCommands( commandName, false, matchingMethods );
			if( matchingMethods.Count == 0 )
				FindCommands( commandName, true, matchingMethods );

			if( matchingMethods.Count == 0 )
				Debug.LogWarning( string.Concat( "ERROR: can't find command '", commandName, "'" ) );
			else
			{
				int commandsLength = 25;
				for( int i = 0; i < matchingMethods.Count; i++ )
					commandsLength += matchingMethods[i].signature.Length + 7;

				StringBuilder stringBuilder = new StringBuilder( commandsLength );
				stringBuilder.Append( "Matching commands:" );

				for( int i = 0; i < matchingMethods.Count; i++ )
					stringBuilder.Append( "\n    - " ).Append( matchingMethods[i].signature );

				Debug.Log( stringBuilder.ToString() );

				if( DebugLogManager.Instance )
					DebugLogManager.Instance.AdjustLatestPendingLog( true, true );
			}
		}

		// Logs system information
		public static void LogSystemInfo()
		{
			StringBuilder stringBuilder = new StringBuilder( 1024 );
			stringBuilder.Append( "Rig: " ).AppendSysInfoIfPresent( SystemInfo.deviceModel ).AppendSysInfoIfPresent( SystemInfo.processorType )
				.AppendSysInfoIfPresent( SystemInfo.systemMemorySize, "MB RAM" ).Append( SystemInfo.processorCount ).Append( " cores\n" );
			stringBuilder.Append( "OS: " ).Append( SystemInfo.operatingSystem ).Append( "\n" );
			stringBuilder.Append( "GPU: " ).Append( SystemInfo.graphicsDeviceName ).Append( " " ).Append( SystemInfo.graphicsMemorySize )
				.Append( "MB " ).Append( SystemInfo.graphicsDeviceVersion )
				.Append( SystemInfo.graphicsMultiThreaded ? " multi-threaded\n" : "\n" );
			stringBuilder.Append( "Data Path: " ).Append( Application.dataPath ).Append( "\n" );
			stringBuilder.Append( "Persistent Data Path: " ).Append( Application.persistentDataPath ).Append( "\n" );
			stringBuilder.Append( "StreamingAssets Path: " ).Append( Application.streamingAssetsPath ).Append( "\n" );
			stringBuilder.Append( "Temporary Cache Path: " ).Append( Application.temporaryCachePath ).Append( "\n" );
			stringBuilder.Append( "Device ID: " ).Append( SystemInfo.deviceUniqueIdentifier ).Append( "\n" );
			stringBuilder.Append( "Max Texture Size: " ).Append( SystemInfo.maxTextureSize ).Append( "\n" );
#if UNITY_5_6_OR_NEWER
			stringBuilder.Append( "Max Cubemap Size: " ).Append( SystemInfo.maxCubemapSize ).Append( "\n" );
#endif
			stringBuilder.Append( "Accelerometer: " ).Append( SystemInfo.supportsAccelerometer ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Gyro: " ).Append( SystemInfo.supportsGyroscope ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Location Service: " ).Append( SystemInfo.supportsLocationService ? "supported\n" : "not supported\n" );
#if !UNITY_2019_1_OR_NEWER
			stringBuilder.Append( "Image Effects: " ).Append( SystemInfo.supportsImageEffects ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "RenderToCubemap: " ).Append( SystemInfo.supportsRenderToCubemap ? "supported\n" : "not supported\n" );
#endif
			stringBuilder.Append( "Compute Shaders: " ).Append( SystemInfo.supportsComputeShaders ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Shadows: " ).Append( SystemInfo.supportsShadows ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Instancing: " ).Append( SystemInfo.supportsInstancing ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Motion Vectors: " ).Append( SystemInfo.supportsMotionVectors ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "3D Textures: " ).Append( SystemInfo.supports3DTextures ? "supported\n" : "not supported\n" );
#if UNITY_5_6_OR_NEWER
			stringBuilder.Append( "3D Render Textures: " ).Append( SystemInfo.supports3DRenderTextures ? "supported\n" : "not supported\n" );
#endif
			stringBuilder.Append( "2D Array Textures: " ).Append( SystemInfo.supports2DArrayTextures ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Cubemap Array Textures: " ).Append( SystemInfo.supportsCubemapArrayTextures ? "supported" : "not supported" );

			Debug.Log( stringBuilder.ToString() );

			// After typing sysinfo, the log that lists system information should automatically be expanded for better UX
			if( DebugLogManager.Instance )
				DebugLogManager.Instance.AdjustLatestPendingLog( true, true );
		}

		private static StringBuilder AppendSysInfoIfPresent( this StringBuilder sb, string info, string postfix = null )
		{
			if( info != SystemInfo.unsupportedIdentifier )
			{
				sb.Append( info );

				if( postfix != null )
					sb.Append( postfix );

				sb.Append( " " );
			}

			return sb;
		}

		private static StringBuilder AppendSysInfoIfPresent( this StringBuilder sb, int info, string postfix = null )
		{
			if( info > 0 )
			{
				sb.Append( info );

				if( postfix != null )
					sb.Append( postfix );

				sb.Append( " " );
			}

			return sb;
		}

		// Add a custom Type to the list of recognized command parameter Types
		public static void AddCustomParameterType( Type type, ParseFunction parseFunction, string typeReadableName = null )
		{
			if( type == null )
			{
				Debug.LogError( "Parameter type can't be null!" );
				return;
			}
			else if( parseFunction == null )
			{
				Debug.LogError( "Parameter parseFunction can't be null!" );
				return;
			}

			parseFunctions[type] = parseFunction;

			if( !string.IsNullOrEmpty( typeReadableName ) )
				typeReadableNames[type] = typeReadableName;
		}

		// Remove a custom Type from the list of recognized command parameter Types
		public static void RemoveCustomParameterType( Type type )
		{
			parseFunctions.Remove( type );
			typeReadableNames.Remove( type );
		}

		// Add a command related with an instance method (i.e. non static method)
		public static void AddCommandInstance( string command, string description, string methodName, object instance, params string[] parameterNames )
		{
			if( instance == null )
			{
				Debug.LogError( "Instance can't be null!" );
				return;
			}

			AddCommand( command, description, methodName, instance.GetType(), instance, parameterNames );
		}

		// Add a command related with a static method (i.e. no instance is required to call the method)
		public static void AddCommandStatic( string command, string description, string methodName, Type ownerType, params string[] parameterNames )
		{
			AddCommand( command, description, methodName, ownerType, null, parameterNames );
		}

		// Add a command that can be related to either a static or an instance method
		public static void AddCommand( string command, string description, Action method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1>( string command, string description, Action<T1> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1>( string command, string description, Func<T1> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1, T2>( string command, string description, Action<T1, T2> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1, T2>( string command, string description, Func<T1, T2> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1, T2, T3>( string command, string description, Action<T1, T2, T3> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1, T2, T3>( string command, string description, Func<T1, T2, T3> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1, T2, T3, T4>( string command, string description, Action<T1, T2, T3, T4> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1, T2, T3, T4>( string command, string description, Func<T1, T2, T3, T4> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand<T1, T2, T3, T4, T5>( string command, string description, Func<T1, T2, T3, T4, T5> method ) { AddCommand( command, description, method.Method, method.Target, null ); }
		public static void AddCommand( string command, string description, Delegate method ) { AddCommand( command, description, method.Method, method.Target, null ); }

		// Add a command with custom parameter names
		public static void AddCommand<T1>( string command, string description, Action<T1> method, string parameterName ) { AddCommand( command, description, method.Method, method.Target, new string[1] { parameterName } ); }
		public static void AddCommand<T1, T2>( string command, string description, Action<T1, T2> method, string parameterName1, string parameterName2 ) { AddCommand( command, description, method.Method, method.Target, new string[2] { parameterName1, parameterName2 } ); }
		public static void AddCommand<T1, T2>( string command, string description, Func<T1, T2> method, string parameterName ) { AddCommand( command, description, method.Method, method.Target, new string[1] { parameterName } ); }
		public static void AddCommand<T1, T2, T3>( string command, string description, Action<T1, T2, T3> method, string parameterName1, string parameterName2, string parameterName3 ) { AddCommand( command, description, method.Method, method.Target, new string[3] { parameterName1, parameterName2, parameterName3 } ); }
		public static void AddCommand<T1, T2, T3>( string command, string description, Func<T1, T2, T3> method, string parameterName1, string parameterName2 ) { AddCommand( command, description, method.Method, method.Target, new string[2] { parameterName1, parameterName2 } ); }
		public static void AddCommand<T1, T2, T3, T4>( string command, string description, Action<T1, T2, T3, T4> method, string parameterName1, string parameterName2, string parameterName3, string parameterName4 ) { AddCommand( command, description, method.Method, method.Target, new string[4] { parameterName1, parameterName2, parameterName3, parameterName4 } ); }
		public static void AddCommand<T1, T2, T3, T4>( string command, string description, Func<T1, T2, T3, T4> method, string parameterName1, string parameterName2, string parameterName3 ) { AddCommand( command, description, method.Method, method.Target, new string[3] { parameterName1, parameterName2, parameterName3 } ); }
		public static void AddCommand<T1, T2, T3, T4, T5>( string command, string description, Func<T1, T2, T3, T4, T5> method, string parameterName1, string parameterName2, string parameterName3, string parameterName4 ) { AddCommand( command, description, method.Method, method.Target, new string[4] { parameterName1, parameterName2, parameterName3, parameterName4 } ); }
		public static void AddCommand( string command, string description, Delegate method, params string[] parameterNames ) { AddCommand( command, description, method.Method, method.Target, parameterNames ); }

		// Create a new command and set its properties
		private static void AddCommand( string command, string description, string methodName, Type ownerType, object instance, string[] parameterNames )
		{
			// Get the method from the class
			MethodInfo method = ownerType.GetMethod( methodName, BindingFlags.Public | BindingFlags.NonPublic | ( instance != null ? BindingFlags.Instance : BindingFlags.Static ) );
			if( method == null )
			{
				Debug.LogError( methodName + " does not exist in " + ownerType );
				return;
			}

			AddCommand( command, description, method, instance, parameterNames );
		}

		internal static void AddCommand( string command, string description, MethodInfo method, object instance, string[] parameterNames )
		{
			if( string.IsNullOrEmpty( command ) )
			{
				Debug.LogError( "Command name can't be empty!" );
				return;
			}

			command = command.Trim();
			if( command.IndexOf( ' ' ) >= 0 )
			{
				Debug.LogError( "Command name can't contain whitespace: " + command );
				return;
			}

			// Fetch the parameters of the class
			ParameterInfo[] parameters = method.GetParameters();
			if( parameters == null )
				parameters = new ParameterInfo[0];

			// Store the parameter types in an array
			Type[] parameterTypes = new Type[parameters.Length];
			for( int i = 0; i < parameters.Length; i++ )
			{
				if( parameters[i].ParameterType.IsByRef )
				{
					Debug.LogError( "Command can't have 'out' or 'ref' parameters" );
					return;
				}

				Type parameterType = parameters[i].ParameterType;
				if( parseFunctions.ContainsKey( parameterType ) || typeof( Component ).IsAssignableFrom( parameterType ) || parameterType.IsEnum || IsSupportedArrayType( parameterType ) )
					parameterTypes[i] = parameterType;
				else
				{
					Debug.LogError( string.Concat( "Parameter ", parameters[i].Name, "'s Type ", parameterType, " isn't supported" ) );
					return;
				}
			}

			int commandIndex = FindCommandIndex( command );
			if( commandIndex < 0 )
				commandIndex = ~commandIndex;
			else
			{
				int commandFirstIndex = commandIndex;
				int commandLastIndex = commandIndex;

				while( commandFirstIndex > 0 && caseInsensitiveComparer.Compare( methods[commandFirstIndex - 1].command, command, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
					commandFirstIndex--;
				while( commandLastIndex < methods.Count - 1 && caseInsensitiveComparer.Compare( methods[commandLastIndex + 1].command, command, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
					commandLastIndex++;

				commandIndex = commandFirstIndex;
				for( int i = commandFirstIndex; i <= commandLastIndex; i++ )
				{
					int parameterCountDiff = methods[i].parameterTypes.Length - parameterTypes.Length;
					if( parameterCountDiff <= 0 )
					{
						// We are sorting the commands in 2 steps:
						// 1: Sorting by their 'command' names which is handled by FindCommandIndex
						// 2: Sorting by their parameter counts which is handled here (parameterCountDiff <= 0)
						commandIndex = i + 1;

						// Check if this command has been registered before and if it is, overwrite that command
						if( parameterCountDiff == 0 )
						{
							int j = 0;
							while( j < parameterTypes.Length && parameterTypes[j] == methods[i].parameterTypes[j] )
								j++;

							if( j >= parameterTypes.Length )
							{
								commandIndex = i;
								commandLastIndex--;
								methods.RemoveAt( i-- );

								continue;
							}
						}
					}
				}
			}

			// Create the command
			StringBuilder methodSignature = new StringBuilder( 256 );
			string[] parameterSignatures = new string[parameterTypes.Length];

#if USE_BOLD_COMMAND_SIGNATURES
			methodSignature.Append( "<b>" );
#endif
			methodSignature.Append( command );

			if( parameterTypes.Length > 0 )
			{
				methodSignature.Append( " " );

				for( int i = 0; i < parameterTypes.Length; i++ )
				{
					int parameterSignatureStartIndex = methodSignature.Length;

					methodSignature.Append( "[" ).Append( GetTypeReadableName( parameterTypes[i] ) ).Append( " " ).Append( ( parameterNames != null && i < parameterNames.Length && !string.IsNullOrEmpty( parameterNames[i] ) ) ? parameterNames[i] : parameters[i].Name ).Append( "]" );

					if( i < parameterTypes.Length - 1 )
						methodSignature.Append( " " );

					parameterSignatures[i] = methodSignature.ToString( parameterSignatureStartIndex, methodSignature.Length - parameterSignatureStartIndex );
				}
			}

#if USE_BOLD_COMMAND_SIGNATURES
			methodSignature.Append( "</b>" );
#endif

			if( !string.IsNullOrEmpty( description ) )
				methodSignature.Append( ": " ).Append( description );

			methods.Insert( commandIndex, new ConsoleMethodInfo( method, parameterTypes, instance, command, methodSignature.ToString(), parameterSignatures ) );
		}

		// Remove all commands with the matching command name from the console
		public static void RemoveCommand( string command )
		{
			if( !string.IsNullOrEmpty( command ) )
			{
				for( int i = methods.Count - 1; i >= 0; i-- )
				{
					if( caseInsensitiveComparer.Compare( methods[i].command, command, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
						methods.RemoveAt( i );
				}
			}
		}

		// Remove all commands with the matching method from the console
		public static void RemoveCommand( Action method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1>( Action<T1> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1>( Func<T1> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1, T2>( Action<T1, T2> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1, T2>( Func<T1, T2> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1, T2, T3>( Action<T1, T2, T3> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1, T2, T3>( Func<T1, T2, T3> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1, T2, T3, T4>( Action<T1, T2, T3, T4> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1, T2, T3, T4>( Func<T1, T2, T3, T4> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand<T1, T2, T3, T4, T5>( Func<T1, T2, T3, T4, T5> method ) { RemoveCommand( method.Method ); }
		public static void RemoveCommand( Delegate method ) { RemoveCommand( method.Method ); }

		public static void RemoveCommand( MethodInfo method )
		{
			if( method != null )
			{
				for( int i = methods.Count - 1; i >= 0; i-- )
				{
					if( methods[i].method == method )
						methods.RemoveAt( i );
				}
			}
		}

		// Returns the first command that starts with the entered argument
		public static string GetAutoCompleteCommand( string commandStart, string previousSuggestion )
		{
			int commandIndex = FindCommandIndex( !string.IsNullOrEmpty( previousSuggestion ) ? previousSuggestion : commandStart );
			if( commandIndex < 0 )
			{
				commandIndex = ~commandIndex;
				return ( commandIndex < methods.Count && caseInsensitiveComparer.IsPrefix( methods[commandIndex].command, commandStart, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) ) ? methods[commandIndex].command : null;
			}

			// Find the next command that starts with commandStart and is different from previousSuggestion
			for( int i = commandIndex + 1; i < methods.Count; i++ )
			{
				if( caseInsensitiveComparer.Compare( methods[i].command, previousSuggestion, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
					continue;
				else if( caseInsensitiveComparer.IsPrefix( methods[i].command, commandStart, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) )
					return methods[i].command;
				else
					break;
			}

			// Couldn't find a command that follows previousSuggestion and satisfies commandStart, loop back to the beginning of the autocomplete suggestions
			string result = null;
			for( int i = commandIndex - 1; i >= 0 && caseInsensitiveComparer.IsPrefix( methods[i].command, commandStart, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ); i-- )
				result = methods[i].command;

			return result;
		}

		// Parse the command and try to execute it
		public static void ExecuteCommand( string command )
		{
			if( command == null )
				return;

			command = command.Trim();

			if( command.Length == 0 )
				return;

			// Split the command's arguments
			commandArguments.Clear();
			FetchArgumentsFromCommand( command, commandArguments );

			// Find all matching commands
			matchingMethods.Clear();
			bool parameterCountMismatch = false;
			int commandIndex = FindCommandIndex( commandArguments[0] );
			if( commandIndex >= 0 )
			{
				string _command = commandArguments[0];

				int commandLastIndex = commandIndex;
				while( commandIndex > 0 && caseInsensitiveComparer.Compare( methods[commandIndex - 1].command, _command, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
					commandIndex--;
				while( commandLastIndex < methods.Count - 1 && caseInsensitiveComparer.Compare( methods[commandLastIndex + 1].command, _command, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
					commandLastIndex++;

				while( commandIndex <= commandLastIndex )
				{
					if( !methods[commandIndex].IsValid() )
					{
						methods.RemoveAt( commandIndex );
						commandLastIndex--;
					}
					else
					{
						// Check if number of parameters match
						if( methods[commandIndex].parameterTypes.Length == commandArguments.Count - 1 )
							matchingMethods.Add( methods[commandIndex] );
						else
							parameterCountMismatch = true;

						commandIndex++;
					}
				}
			}

			if( matchingMethods.Count == 0 )
			{
				string _command = commandArguments[0];
				FindCommands( _command, !parameterCountMismatch, matchingMethods );

				if( matchingMethods.Count == 0 )
					Debug.LogWarning( string.Concat( "ERROR: can't find command '", _command, "'" ) );
				else
				{
					int commandsLength = _command.Length + 75;
					for( int i = 0; i < matchingMethods.Count; i++ )
						commandsLength += matchingMethods[i].signature.Length + 7;

					StringBuilder stringBuilder = new StringBuilder( commandsLength );
					if( parameterCountMismatch )
						stringBuilder.Append( "ERROR: '" ).Append( _command ).Append( "' doesn't take " ).Append( commandArguments.Count - 1 ).Append( " parameter(s). Available command(s):" );
					else
						stringBuilder.Append( "ERROR: can't find command '" ).Append( _command ).Append( "'. Did you mean:" );

					for( int i = 0; i < matchingMethods.Count; i++ )
						stringBuilder.Append( "\n    - " ).Append( matchingMethods[i].signature );

					Debug.LogWarning( stringBuilder.ToString() );

					// The log that lists method signature(s) for this command should automatically be expanded for better UX
					if( DebugLogManager.Instance )
						DebugLogManager.Instance.AdjustLatestPendingLog( true, true );
				}

				return;
			}

			ConsoleMethodInfo methodToExecute = null;
			object[] parameters = new object[commandArguments.Count - 1];
			string errorMessage = null;
			for( int i = 0; i < matchingMethods.Count && methodToExecute == null; i++ )
			{
				ConsoleMethodInfo methodInfo = matchingMethods[i];

				// Parse the parameters into objects
				bool success = true;
				for( int j = 0; j < methodInfo.parameterTypes.Length && success; j++ )
				{
					try
					{
						string argument = commandArguments[j + 1];
						Type parameterType = methodInfo.parameterTypes[j];

						object val;
						if( ParseArgument( argument, parameterType, out val ) )
							parameters[j] = val;
						else
						{
							success = false;
							errorMessage = string.Concat( "ERROR: couldn't parse ", argument, " to ", GetTypeReadableName( parameterType ) );
						}
					}
					catch( Exception e )
					{
						success = false;
						errorMessage = "ERROR: " + e.ToString();
					}
				}

				if( success )
					methodToExecute = methodInfo;
			}

			if( methodToExecute == null )
				Debug.LogWarning( !string.IsNullOrEmpty( errorMessage ) ? errorMessage : "ERROR: something went wrong" );
			else
			{
				// Execute the method associated with the command
				object result = methodToExecute.method.Invoke( methodToExecute.instance, parameters );
				if( methodToExecute.method.ReturnType != typeof( void ) )
				{
					// Print the returned value to the console
					if( result == null || result.Equals( null ) )
						Debug.Log( "Returned: null" );
					else
						Debug.Log( "Returned: " + result.ToString() );
				}

				if( OnCommandExecuted != null )
					OnCommandExecuted( methodToExecute.command, parameters );
			}
		}

		public static void FetchArgumentsFromCommand( string command, List<string> commandArguments )
		{
			for( int i = 0; i < command.Length; i++ )
			{
				if( char.IsWhiteSpace( command[i] ) )
					continue;

				int delimiterIndex = IndexOfDelimiterGroup( command[i] );
				if( delimiterIndex >= 0 )
				{
					int endIndex = IndexOfDelimiterGroupEnd( command, delimiterIndex, i + 1 );
					commandArguments.Add( command.Substring( i + 1, endIndex - i - 1 ) );
					i = ( endIndex < command.Length - 1 && command[endIndex + 1] == ',' ) ? endIndex + 1 : endIndex;
				}
				else
				{
					int endIndex = IndexOfChar( command, ' ', i + 1 );
					commandArguments.Add( command.Substring( i, command[endIndex - 1] == ',' ? endIndex - 1 - i : endIndex - i ) );
					i = endIndex;
				}
			}
		}

		public static void FindCommands( string commandName, bool allowSubstringMatching, List<ConsoleMethodInfo> matchingCommands )
		{
			if( allowSubstringMatching )
			{
				for( int i = 0; i < methods.Count; i++ )
				{
					if( methods[i].IsValid() && caseInsensitiveComparer.IndexOf( methods[i].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) >= 0 )
						matchingCommands.Add( methods[i] );
				}
			}
			else
			{
				for( int i = 0; i < methods.Count; i++ )
				{
					if( methods[i].IsValid() && caseInsensitiveComparer.Compare( methods[i].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
						matchingCommands.Add( methods[i] );
				}
			}
		}

		// Finds all commands that have a matching signature with command
		// - caretIndexIncrements: indices inside "string command" that separate two arguments in the command. This is used to
		//   figure out which argument the caret is standing on
		// - commandName: command's name (first argument)
		internal static void GetCommandSuggestions( string command, List<ConsoleMethodInfo> matchingCommands, List<int> caretIndexIncrements, ref string commandName, out int numberOfParameters )
		{
			bool commandNameCalculated = false;
			bool commandNameFullyTyped = false;
			numberOfParameters = -1;
			for( int i = 0; i < command.Length; i++ )
			{
				if( char.IsWhiteSpace( command[i] ) )
					continue;

				int delimiterIndex = IndexOfDelimiterGroup( command[i] );
				if( delimiterIndex >= 0 )
				{
					int endIndex = IndexOfDelimiterGroupEnd( command, delimiterIndex, i + 1 );
					if( !commandNameCalculated )
					{
						commandNameCalculated = true;
						commandNameFullyTyped = command.Length > endIndex;

						int commandNameLength = endIndex - i - 1;
						if( commandName == null || commandNameLength == 0 || commandName.Length != commandNameLength || caseInsensitiveComparer.IndexOf( command, commandName, i + 1, commandNameLength, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) != i + 1 )
							commandName = command.Substring( i + 1, commandNameLength );
					}

					i = ( endIndex < command.Length - 1 && command[endIndex + 1] == ',' ) ? endIndex + 1 : endIndex;
					caretIndexIncrements.Add( i + 1 );
				}
				else
				{
					int endIndex = IndexOfChar( command, ' ', i + 1 );
					if( !commandNameCalculated )
					{
						commandNameCalculated = true;
						commandNameFullyTyped = command.Length > endIndex;

						int commandNameLength = command[endIndex - 1] == ',' ? endIndex - 1 - i : endIndex - i;
						if( commandName == null || commandNameLength == 0 || commandName.Length != commandNameLength || caseInsensitiveComparer.IndexOf( command, commandName, i, commandNameLength, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) != i )
							commandName = command.Substring( i, commandNameLength );
					}

					i = endIndex;
					caretIndexIncrements.Add( i );
				}

				numberOfParameters++;
			}

			if( !commandNameCalculated )
				commandName = string.Empty;

			if( !string.IsNullOrEmpty( commandName ) )
			{
				int commandIndex = FindCommandIndex( commandName );
				if( commandIndex < 0 )
					commandIndex = ~commandIndex;

				int commandLastIndex = commandIndex;
				if( !commandNameFullyTyped )
				{
					// Match all commands that start with commandName
					if( commandIndex < methods.Count && caseInsensitiveComparer.IsPrefix( methods[commandIndex].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) )
					{
						while( commandIndex > 0 && caseInsensitiveComparer.IsPrefix( methods[commandIndex - 1].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) )
							commandIndex--;
						while( commandLastIndex < methods.Count - 1 && caseInsensitiveComparer.IsPrefix( methods[commandLastIndex + 1].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) )
							commandLastIndex++;
					}
					else
						commandLastIndex = -1;
				}
				else
				{
					// Match only the commands that are equal to commandName
					if( commandIndex < methods.Count && caseInsensitiveComparer.Compare( methods[commandIndex].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
					{
						while( commandIndex > 0 && caseInsensitiveComparer.Compare( methods[commandIndex - 1].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
							commandIndex--;
						while( commandLastIndex < methods.Count - 1 && caseInsensitiveComparer.Compare( methods[commandLastIndex + 1].command, commandName, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace ) == 0 )
							commandLastIndex++;
					}
					else
						commandLastIndex = -1;
				}

				for( ; commandIndex <= commandLastIndex; commandIndex++ )
				{
					if( methods[commandIndex].parameterTypes.Length >= numberOfParameters )
						matchingCommands.Add( methods[commandIndex] );
				}
			}
		}

		// Find the index of the delimiter group that 'c' belongs to
		private static int IndexOfDelimiterGroup( char c )
		{
			for( int i = 0; i < inputDelimiters.Length; i++ )
			{
				if( c == inputDelimiters[i][0] )
					return i;
			}

			return -1;
		}

		private static int IndexOfDelimiterGroupEnd( string command, int delimiterIndex, int startIndex )
		{
			char startChar = inputDelimiters[delimiterIndex][0];
			char endChar = inputDelimiters[delimiterIndex][1];

			// Check delimiter's depth for array support (e.g. [[1 2] [3 4]] for Vector2 array)
			int depth = 1;

			for( int i = startIndex; i < command.Length; i++ )
			{
				char c = command[i];
				if( c == endChar && --depth <= 0 )
					return i;
				else if( c == startChar )
					depth++;
			}

			return command.Length;
		}

		// Find the index of char in the string, or return the length of string instead of -1
		private static int IndexOfChar( string command, char c, int startIndex )
		{
			int result = command.IndexOf( c, startIndex );
			if( result < 0 )
				result = command.Length;

			return result;
		}

		// Find command's index in the list of registered commands using binary search
		private static int FindCommandIndex( string command )
		{
			int min = 0;
			int max = methods.Count - 1;
			while( min <= max )
			{
				int mid = ( min + max ) / 2;
				int comparison = caseInsensitiveComparer.Compare( command, methods[mid].command, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace );
				if( comparison == 0 )
					return mid;
				else if( comparison < 0 )
					max = mid - 1;
				else
					min = mid + 1;
			}

			return ~min;
		}

		public static bool IsSupportedArrayType( Type type )
		{
			if( type.IsArray )
			{
				if( type.GetArrayRank() != 1 )
					return false;

				type = type.GetElementType();
			}
			else if( type.IsGenericType )
			{
				if( type.GetGenericTypeDefinition() != typeof( List<> ) )
					return false;

				type = type.GetGenericArguments()[0];
			}
			else
				return false;

			return parseFunctions.ContainsKey( type ) || typeof( Component ).IsAssignableFrom( type ) || type.IsEnum;
		}

		public static string GetTypeReadableName( Type type )
		{
			string result;
			if( typeReadableNames.TryGetValue( type, out result ) )
				return result;

			if( IsSupportedArrayType( type ) )
			{
				Type elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
				if( typeReadableNames.TryGetValue( elementType, out result ) )
					return result + "[]";
				else
					return elementType.Name + "[]";
			}

			return type.Name;
		}

		public static bool ParseArgument( string input, Type argumentType, out object output )
		{
			ParseFunction parseFunction;
			if( parseFunctions.TryGetValue( argumentType, out parseFunction ) )
				return parseFunction( input, out output );
			else if( typeof( Component ).IsAssignableFrom( argumentType ) )
				return ParseComponent( input, argumentType, out output );
			else if( argumentType.IsEnum )
				return ParseEnum( input, argumentType, out output );
			else if( IsSupportedArrayType( argumentType ) )
				return ParseArray( input, argumentType, out output );
			else
			{
				output = null;
				return false;
			}
		}

		public static bool ParseString( string input, out object output )
		{
			output = input;
			return true;
		}

		public static bool ParseBool( string input, out object output )
		{
			if( input == "1" || input.ToLowerInvariant() == "true" )
			{
				output = true;
				return true;
			}

			if( input == "0" || input.ToLowerInvariant() == "false" )
			{
				output = false;
				return true;
			}

			output = false;
			return false;
		}

		public static bool ParseInt( string input, out object output )
		{
			int value;
			bool result = int.TryParse( input, out value );

			output = value;
			return result;
		}

		public static bool ParseUInt( string input, out object output )
		{
			uint value;
			bool result = uint.TryParse( input, out value );

			output = value;
			return result;
		}

		public static bool ParseLong( string input, out object output )
		{
			long value;
			bool result = long.TryParse( !input.EndsWith( "L", StringComparison.OrdinalIgnoreCase ) ? input : input.Substring( 0, input.Length - 1 ), out value );

			output = value;
			return result;
		}

		public static bool ParseULong( string input, out object output )
		{
			ulong value;
			bool result = ulong.TryParse( !input.EndsWith( "L", StringComparison.OrdinalIgnoreCase ) ? input : input.Substring( 0, input.Length - 1 ), out value );

			output = value;
			return result;
		}

		public static bool ParseByte( string input, out object output )
		{
			byte value;
			bool result = byte.TryParse( input, out value );

			output = value;
			return result;
		}

		public static bool ParseSByte( string input, out object output )
		{
			sbyte value;
			bool result = sbyte.TryParse( input, out value );

			output = value;
			return result;
		}

		public static bool ParseShort( string input, out object output )
		{
			short value;
			bool result = short.TryParse( input, out value );

			output = value;
			return result;
		}

		public static bool ParseUShort( string input, out object output )
		{
			ushort value;
			bool result = ushort.TryParse( input, out value );

			output = value;
			return result;
		}

		public static bool ParseChar( string input, out object output )
		{
			char value;
			bool result = char.TryParse( input, out value );

			output = value;
			return result;
		}

		public static bool ParseFloat( string input, out object output )
		{
			float value;
			bool result = float.TryParse( !input.EndsWith( "f", StringComparison.OrdinalIgnoreCase ) ? input : input.Substring( 0, input.Length - 1 ), NumberStyles.Float, CultureInfo.InvariantCulture, out value );

			output = value;
			return result;
		}

		public static bool ParseDouble( string input, out object output )
		{
			double value;
			bool result = double.TryParse( !input.EndsWith( "f", StringComparison.OrdinalIgnoreCase ) ? input : input.Substring( 0, input.Length - 1 ), NumberStyles.Float, CultureInfo.InvariantCulture, out value );

			output = value;
			return result;
		}

		public static bool ParseDecimal( string input, out object output )
		{
			decimal value;
			bool result = decimal.TryParse( !input.EndsWith( "f", StringComparison.OrdinalIgnoreCase ) ? input : input.Substring( 0, input.Length - 1 ), NumberStyles.Float, CultureInfo.InvariantCulture, out value );

			output = value;
			return result;
		}

		public static bool ParseVector2( string input, out object output )
		{
			return ParseVector( input, typeof( Vector2 ), out output );
		}

		public static bool ParseVector3( string input, out object output )
		{
			return ParseVector( input, typeof( Vector3 ), out output );
		}

		public static bool ParseVector4( string input, out object output )
		{
			return ParseVector( input, typeof( Vector4 ), out output );
		}

		public static bool ParseQuaternion( string input, out object output )
		{
			return ParseVector( input, typeof( Quaternion ), out output );
		}

		public static bool ParseColor( string input, out object output )
		{
			return ParseVector( input, typeof( Color ), out output );
		}

		public static bool ParseColor32( string input, out object output )
		{
			return ParseVector( input, typeof( Color32 ), out output );
		}

		public static bool ParseRect( string input, out object output )
		{
			return ParseVector( input, typeof( Rect ), out output );
		}

		public static bool ParseRectOffset( string input, out object output )
		{
			return ParseVector( input, typeof( RectOffset ), out output );
		}

		public static bool ParseBounds( string input, out object output )
		{
			return ParseVector( input, typeof( Bounds ), out output );
		}

#if UNITY_2017_2_OR_NEWER
		public static bool ParseVector2Int( string input, out object output )
		{
			return ParseVector( input, typeof( Vector2Int ), out output );
		}

		public static bool ParseVector3Int( string input, out object output )
		{
			return ParseVector( input, typeof( Vector3Int ), out output );
		}

		public static bool ParseRectInt( string input, out object output )
		{
			return ParseVector( input, typeof( RectInt ), out output );
		}

		public static bool ParseBoundsInt( string input, out object output )
		{
			return ParseVector( input, typeof( BoundsInt ), out output );
		}
#endif

		public static bool ParseGameObject( string input, out object output )
		{
			output = input == "null" ? null : GameObject.Find( input );
			return true;
		}

		public static bool ParseComponent( string input, Type componentType, out object output )
		{
			GameObject gameObject = input == "null" ? null : GameObject.Find( input );
			output = gameObject ? gameObject.GetComponent( componentType ) : null;
			return true;
		}

		public static bool ParseEnum( string input, Type enumType, out object output )
		{
			const int NONE = 0, OR = 1, AND = 2;

			int outputInt = 0;
			int operation = NONE; // 0: nothing, 1: OR with outputInt, 2: AND with outputInt
			for( int i = 0; i < input.Length; i++ )
			{
				string enumStr;
				int orIndex = input.IndexOf( '|', i );
				int andIndex = input.IndexOf( '&', i );
				if( orIndex < 0 )
					enumStr = input.Substring( i, ( andIndex < 0 ? input.Length : andIndex ) - i ).Trim();
				else
					enumStr = input.Substring( i, ( andIndex < 0 ? orIndex : Mathf.Min( andIndex, orIndex ) ) - i ).Trim();

				int value;
				if( !int.TryParse( enumStr, out value ) )
				{
					try
					{
						// Case-insensitive enum parsing
						value = Convert.ToInt32( Enum.Parse( enumType, enumStr, true ) );
					}
					catch
					{
						output = null;
						return false;
					}
				}

				if( operation == NONE )
					outputInt = value;
				else if( operation == OR )
					outputInt |= value;
				else
					outputInt &= value;

				if( orIndex >= 0 )
				{
					if( andIndex > orIndex )
					{
						operation = AND;
						i = andIndex;
					}
					else
					{
						operation = OR;
						i = orIndex;
					}
				}
				else if( andIndex >= 0 )
				{
					operation = AND;
					i = andIndex;
				}
				else
					i = input.Length;
			}

			output = Enum.ToObject( enumType, outputInt );
			return true;
		}

		public static bool ParseArray( string input, Type arrayType, out object output )
		{
			List<string> valuesToParse = new List<string>( 2 );
			FetchArgumentsFromCommand( input, valuesToParse );

			IList result = (IList) Activator.CreateInstance( arrayType, new object[1] { valuesToParse.Count } );
			output = result;

			if( arrayType.IsArray )
			{
				Type elementType = arrayType.GetElementType();
				for( int i = 0; i < valuesToParse.Count; i++ )
				{
					object obj;
					if( !ParseArgument( valuesToParse[i], elementType, out obj ) )
						return false;

					result[i] = obj;
				}
			}
			else
			{
				Type elementType = arrayType.GetGenericArguments()[0];
				for( int i = 0; i < valuesToParse.Count; i++ )
				{
					object obj;
					if( !ParseArgument( valuesToParse[i], elementType, out obj ) )
						return false;

					result.Add( obj );
				}
			}

			return true;
		}

		// Create a vector of specified type (fill the blank slots with 0 or ignore unnecessary slots)
		private static bool ParseVector( string input, Type vectorType, out object output )
		{
			List<string> tokens = new List<string>( input.Replace( ',', ' ' ).Trim().Split( ' ' ) );
			for( int i = tokens.Count - 1; i >= 0; i-- )
			{
				tokens[i] = tokens[i].Trim();
				if( tokens[i].Length == 0 )
					tokens.RemoveAt( i );
			}

			float[] tokenValues = new float[tokens.Count];
			for( int i = 0; i < tokens.Count; i++ )
			{
				object val;
				if( !ParseFloat( tokens[i], out val ) )
				{
					if( vectorType == typeof( Vector3 ) )
						output = Vector3.zero;
					else if( vectorType == typeof( Vector2 ) )
						output = Vector2.zero;
					else
						output = Vector4.zero;

					return false;
				}

				tokenValues[i] = (float) val;
			}

			if( vectorType == typeof( Vector3 ) )
			{
				Vector3 result = Vector3.zero;

				for( int i = 0; i < tokenValues.Length && i < 3; i++ )
					result[i] = tokenValues[i];

				output = result;
			}
			else if( vectorType == typeof( Vector2 ) )
			{
				Vector2 result = Vector2.zero;

				for( int i = 0; i < tokenValues.Length && i < 2; i++ )
					result[i] = tokenValues[i];

				output = result;
			}
			else if( vectorType == typeof( Vector4 ) )
			{
				Vector4 result = Vector4.zero;

				for( int i = 0; i < tokenValues.Length && i < 4; i++ )
					result[i] = tokenValues[i];

				output = result;
			}
			else if( vectorType == typeof( Quaternion ) )
			{
				Quaternion result = Quaternion.identity;

				for( int i = 0; i < tokenValues.Length && i < 4; i++ )
					result[i] = tokenValues[i];

				output = result;
			}
			else if( vectorType == typeof( Color ) )
			{
				Color result = Color.black;

				for( int i = 0; i < tokenValues.Length && i < 4; i++ )
					result[i] = tokenValues[i];

				output = result;
			}
			else if( vectorType == typeof( Color32 ) )
			{
				Color32 result = new Color32( 0, 0, 0, 255 );

				if( tokenValues.Length > 0 )
					result.r = (byte) Mathf.RoundToInt( tokenValues[0] );
				if( tokenValues.Length > 1 )
					result.g = (byte) Mathf.RoundToInt( tokenValues[1] );
				if( tokenValues.Length > 2 )
					result.b = (byte) Mathf.RoundToInt( tokenValues[2] );
				if( tokenValues.Length > 3 )
					result.a = (byte) Mathf.RoundToInt( tokenValues[3] );

				output = result;
			}
			else if( vectorType == typeof( Rect ) )
			{
				Rect result = Rect.zero;

				if( tokenValues.Length > 0 )
					result.x = tokenValues[0];
				if( tokenValues.Length > 1 )
					result.y = tokenValues[1];
				if( tokenValues.Length > 2 )
					result.width = tokenValues[2];
				if( tokenValues.Length > 3 )
					result.height = tokenValues[3];

				output = result;
			}
			else if( vectorType == typeof( RectOffset ) )
			{
				RectOffset result = new RectOffset();

				if( tokenValues.Length > 0 )
					result.left = Mathf.RoundToInt( tokenValues[0] );
				if( tokenValues.Length > 1 )
					result.right = Mathf.RoundToInt( tokenValues[1] );
				if( tokenValues.Length > 2 )
					result.top = Mathf.RoundToInt( tokenValues[2] );
				if( tokenValues.Length > 3 )
					result.bottom = Mathf.RoundToInt( tokenValues[3] );

				output = result;
			}
			else if( vectorType == typeof( Bounds ) )
			{
				Vector3 center = Vector3.zero;
				for( int i = 0; i < tokenValues.Length && i < 3; i++ )
					center[i] = tokenValues[i];

				Vector3 size = Vector3.zero;
				for( int i = 3; i < tokenValues.Length && i < 6; i++ )
					size[i - 3] = tokenValues[i];

				output = new Bounds( center, size );
			}
#if UNITY_2017_2_OR_NEWER
			else if( vectorType == typeof( Vector3Int ) )
			{
				Vector3Int result = Vector3Int.zero;

				for( int i = 0; i < tokenValues.Length && i < 3; i++ )
					result[i] = Mathf.RoundToInt( tokenValues[i] );

				output = result;
			}
			else if( vectorType == typeof( Vector2Int ) )
			{
				Vector2Int result = Vector2Int.zero;

				for( int i = 0; i < tokenValues.Length && i < 2; i++ )
					result[i] = Mathf.RoundToInt( tokenValues[i] );

				output = result;
			}
			else if( vectorType == typeof( RectInt ) )
			{
				RectInt result = new RectInt();

				if( tokenValues.Length > 0 )
					result.x = Mathf.RoundToInt( tokenValues[0] );
				if( tokenValues.Length > 1 )
					result.y = Mathf.RoundToInt( tokenValues[1] );
				if( tokenValues.Length > 2 )
					result.width = Mathf.RoundToInt( tokenValues[2] );
				if( tokenValues.Length > 3 )
					result.height = Mathf.RoundToInt( tokenValues[3] );

				output = result;
			}
			else if( vectorType == typeof( BoundsInt ) )
			{
				Vector3Int center = Vector3Int.zero;
				for( int i = 0; i < tokenValues.Length && i < 3; i++ )
					center[i] = Mathf.RoundToInt( tokenValues[i] );

				Vector3Int size = Vector3Int.zero;
				for( int i = 3; i < tokenValues.Length && i < 6; i++ )
					size[i - 3] = Mathf.RoundToInt( tokenValues[i] );

				output = new BoundsInt( center, size );
			}
#endif
			else
			{
				output = null;
				return false;
			}

			return true;
		}
	}
}