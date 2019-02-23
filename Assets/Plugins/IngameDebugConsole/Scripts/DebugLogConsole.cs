using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System;

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

		public readonly string signature;

		public ConsoleMethodInfo( MethodInfo method, Type[] parameterTypes, object instance, string signature )
		{
			this.method = method;
			this.parameterTypes = parameterTypes;
			this.instance = instance;
			this.signature = signature;
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

		// All the commands
		private static Dictionary<string, ConsoleMethodInfo> methods = new Dictionary<string, ConsoleMethodInfo>();

		// All the parse functions
		private static Dictionary<Type, ParseFunction> parseFunctions;

		// All the readable names of accepted types
		private static Dictionary<Type, string> typeReadableNames;

		// Split arguments of an entered command
		private static List<string> commandArguments = new List<string>( 8 );

		// Command parameter delimeter groups
		private static readonly string[] inputDelimiters = new string[] { "\"\"", "{}", "()", "[]" };

		static DebugLogConsole()
		{
			parseFunctions = new Dictionary<Type, ParseFunction>() {
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
				{ typeof( GameObject ), ParseGameObject } };

			typeReadableNames = new Dictionary<Type, string>() {
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
				{ typeof( decimal ), "Decimal" },
				{ typeof( Vector2 ), "Vector2" },
				{ typeof( Vector3 ), "Vector3" },
				{ typeof( Vector4 ), "Vector4" },
				{ typeof( GameObject ), "GameObject" } };

#if UNITY_EDITOR || !NETFX_CORE
			// Load commands in most common Unity assemblies
			HashSet<Assembly> assemblies = new HashSet<Assembly> { Assembly.GetAssembly( typeof( DebugLogConsole ) ) };
			try
			{
				assemblies.Add( Assembly.Load( "Assembly-CSharp" ) );
			}
			catch { }

			foreach( var assembly in assemblies )
			{
				foreach( var type in assembly.GetExportedTypes() )
				{
					foreach( var method in type.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly ) )
					{
						foreach( var attribute in method.GetCustomAttributes( typeof( ConsoleMethodAttribute ), false ) )
						{
							ConsoleMethodAttribute consoleMethod = attribute as ConsoleMethodAttribute;
							if( consoleMethod != null )
								AddCommand( consoleMethod.Command, consoleMethod.Description, method );
						}
					}
				}
			}
#endif

			AddCommandStatic( "help", "Prints all commands", "LogAllCommands", typeof( DebugLogConsole ) );
			AddCommandStatic( "sysinfo", "Prints system information", "LogSystemInfo", typeof( DebugLogConsole ) );
		}

		// Logs the list of available commands
		public static void LogAllCommands()
		{
			int length = 25;
			foreach( var entry in methods )
			{
				if( entry.Value.IsValid() )
					length += 3 + entry.Value.signature.Length;
			}

			StringBuilder stringBuilder = new StringBuilder( length );
			stringBuilder.Append( "Available commands:" );

			foreach( var entry in methods )
			{
				if( entry.Value.IsValid() )
					stringBuilder.Append( "\n- " ).Append( entry.Value.signature );
			}

			Debug.Log( stringBuilder.Append( "\n" ).ToString() );
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
			stringBuilder.Append( "Max Cubemap Size: " ).Append( SystemInfo.maxCubemapSize ).Append( "\n" );
			stringBuilder.Append( "Accelerometer: " ).Append( SystemInfo.supportsAccelerometer ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Gyro: " ).Append( SystemInfo.supportsGyroscope ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Location Service: " ).Append( SystemInfo.supportsLocationService ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Image Effects: " ).Append( SystemInfo.supportsImageEffects ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Compute Shaders: " ).Append( SystemInfo.supportsComputeShaders ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Shadows: " ).Append( SystemInfo.supportsShadows ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "RenderToCubemap: " ).Append( SystemInfo.supportsRenderToCubemap ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Instancing: " ).Append( SystemInfo.supportsInstancing ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Motion Vectors: " ).Append( SystemInfo.supportsMotionVectors ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "3D Textures: " ).Append( SystemInfo.supports3DTextures ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "3D Render Textures: " ).Append( SystemInfo.supports3DRenderTextures ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "2D Array Textures: " ).Append( SystemInfo.supports2DArrayTextures ? "supported\n" : "not supported\n" );
			stringBuilder.Append( "Cubemap Array Textures: " ).Append( SystemInfo.supportsCubemapArrayTextures ? "supported" : "not supported" );

			Debug.Log( stringBuilder.Append( "\n" ).ToString() );
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

		// Add a command related with an instance method (i.e. non static method)
		public static void AddCommandInstance( string command, string description, string methodName, object instance )
		{
			if( instance == null )
			{
				Debug.LogError( "Instance can't be null!" );
				return;
			}

			AddCommand( command, description, methodName, instance.GetType(), instance );
		}

		// Add a command related with a static method (i.e. no instance is required to call the method)
		public static void AddCommandStatic( string command, string description, string methodName, Type ownerType )
		{
			AddCommand( command, description, methodName, ownerType );
		}

		// Remove a command from the console
		public static void RemoveCommand( string command )
		{
			if( !string.IsNullOrEmpty( command ) )
				methods.Remove( command );
		}

		// Returns the first command that starts with the entered argument
		public static string GetAutoCompleteCommand( string commandStart )
		{
			foreach( var entry in methods )
			{
				if( entry.Key.StartsWith( commandStart ) )
					return entry.Key;
			}

			return null;
		}

		// Create a new command and set its properties
		private static void AddCommand( string command, string description, string methodName, Type ownerType, object instance = null )
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

			// Get the method from the class
			MethodInfo method = ownerType.GetMethod( methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static );
			if( method == null )
			{
				Debug.LogError( methodName + " does not exist in " + ownerType );
				return;
			}

			AddCommand( command, description, method, instance );
		}

		private static void AddCommand( string command, string description, MethodInfo method, object instance = null )
		{
			// Fetch the parameters of the class
			ParameterInfo[] parameters = method.GetParameters();
			if( parameters == null )
				parameters = new ParameterInfo[0];

			bool isMethodValid = true;

			// Store the parameter types in an array
			Type[] parameterTypes = new Type[parameters.Length];
			for( int k = 0; k < parameters.Length; k++ )
			{
				Type parameterType = parameters[k].ParameterType;
				if( parseFunctions.ContainsKey( parameterType ) )
					parameterTypes[k] = parameterType;
				else
				{
					isMethodValid = false;
					break;
				}
			}

			// If method is valid, associate it with the entered command
			if( isMethodValid )
			{
				StringBuilder methodSignature = new StringBuilder( 256 );
				methodSignature.Append( command ).Append( ": " );

				if( !string.IsNullOrEmpty( description ) )
					methodSignature.Append( description ).Append( " -> " );

				methodSignature.Append( method.DeclaringType.ToString() ).Append( "." ).Append( method.Name ).Append( "(" );
				for( int i = 0; i < parameterTypes.Length; i++ )
				{
					Type type = parameterTypes[i];
					string typeName;
					if( !typeReadableNames.TryGetValue( type, out typeName ) )
						typeName = type.Name;

					methodSignature.Append( typeName );

					if( i < parameterTypes.Length - 1 )
						methodSignature.Append( ", " );
				}

				methodSignature.Append( ")" );

				Type returnType = method.ReturnType;
				if( returnType != typeof( void ) )
				{
					string returnTypeName;
					if( !typeReadableNames.TryGetValue( returnType, out returnTypeName ) )
						returnTypeName = returnType.Name;

					methodSignature.Append( " : " ).Append( returnTypeName );
				}

				methods[command] = new ConsoleMethodInfo( method, parameterTypes, instance, methodSignature.ToString() );
			}
		}

		// Parse the command and try to execute it
		public static void ExecuteCommand( string command )
		{
			if( command == null )
				return;

			command = command.Trim();

			if( command.Length == 0 )
				return;

			// Parse the arguments
			commandArguments.Clear();

			int endIndex = IndexOfChar( command, ' ', 0 );
			commandArguments.Add( command.Substring( 0, endIndex ) );

			for( int i = endIndex + 1; i < command.Length; i++ )
			{
				if( command[i] == ' ' )
					continue;

				int delimiterIndex = IndexOfDelimiter( command[i] );
				if( delimiterIndex >= 0 )
				{
					endIndex = IndexOfChar( command, inputDelimiters[delimiterIndex][1], i + 1 );
					commandArguments.Add( command.Substring( i + 1, endIndex - i - 1 ) );
				}
				else
				{
					endIndex = IndexOfChar( command, ' ', i + 1 );
					commandArguments.Add( command.Substring( i, endIndex - i ) );
				}

				i = endIndex;
			}

			// Check if command exists
			ConsoleMethodInfo methodInfo;
			if( !methods.TryGetValue( commandArguments[0], out methodInfo ) )
				Debug.LogWarning( "Can't find command: " + commandArguments[0] );
			else if( !methodInfo.IsValid() )
				Debug.LogWarning( "Method no longer valid (instance dead): " + commandArguments[0] );
			else
			{
				// Check if number of parameter match
				if( methodInfo.parameterTypes.Length != commandArguments.Count - 1 )
				{
					Debug.LogWarning( "Parameter count mismatch: " + methodInfo.parameterTypes.Length + " parameters are needed" );
					return;
				}

				Debug.Log( "Executing command: " + commandArguments[0] );

				// Parse the parameters into objects
				object[] parameters = new object[methodInfo.parameterTypes.Length];
				for( int i = 0; i < methodInfo.parameterTypes.Length; i++ )
				{
					string argument = commandArguments[i + 1];

					Type parameterType = methodInfo.parameterTypes[i];
					ParseFunction parseFunction;
					if( !parseFunctions.TryGetValue( parameterType, out parseFunction ) )
					{
						Debug.LogError( "Unsupported parameter type: " + parameterType.Name );
						return;
					}

					object val;
					if( !parseFunction( argument, out val ) )
					{
						Debug.LogError( "Couldn't parse " + argument + " to " + parameterType.Name );
						return;
					}

					parameters[i] = val;
				}

				// Execute the method associated with the command
				object result = methodInfo.method.Invoke( methodInfo.instance, parameters );
				if( methodInfo.method.ReturnType != typeof( void ) )
				{
					// Print the returned value to the console
					if( result == null || result.Equals( null ) )
						Debug.Log( "Value returned: null" );
					else
						Debug.Log( "Value returned: " + result.ToString() );
				}
			}
		}

		// Find the index of the delimiter group that 'c' belongs to
		private static int IndexOfDelimiter( char c )
		{
			for( int i = 0; i < inputDelimiters.Length; i++ )
			{
				if( c == inputDelimiters[i][0] )
					return i;
			}

			return -1;
		}

		// Find the index of char in the string, or return the length of string instead of -1
		private static int IndexOfChar( string command, char c, int startIndex )
		{
			int result = command.IndexOf( c, startIndex );
			if( result < 0 )
				result = command.Length;

			return result;
		}

		private static bool ParseString( string input, out object output )
		{
			output = input;
			return input.Length > 0;
		}

		private static bool ParseBool( string input, out object output )
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

		private static bool ParseInt( string input, out object output )
		{
			bool result;
			int value;
			result = int.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseUInt( string input, out object output )
		{
			bool result;
			uint value;
			result = uint.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseLong( string input, out object output )
		{
			bool result;
			long value;
			result = long.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseULong( string input, out object output )
		{
			bool result;
			ulong value;
			result = ulong.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseByte( string input, out object output )
		{
			bool result;
			byte value;
			result = byte.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseSByte( string input, out object output )
		{
			bool result;
			sbyte value;
			result = sbyte.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseShort( string input, out object output )
		{
			bool result;
			short value;
			result = short.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseUShort( string input, out object output )
		{
			bool result;
			ushort value;
			result = ushort.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseChar( string input, out object output )
		{
			bool result;
			char value;
			result = char.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseFloat( string input, out object output )
		{
			bool result;
			float value;
			result = float.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseDouble( string input, out object output )
		{
			bool result;
			double value;
			result = double.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseDecimal( string input, out object output )
		{
			bool result;
			decimal value;
			result = decimal.TryParse( input, out value );

			output = value;
			return result;
		}

		private static bool ParseVector2( string input, out object output )
		{
			return CreateVectorFromInput( input, typeof( Vector2 ), out output );
		}

		private static bool ParseVector3( string input, out object output )
		{
			return CreateVectorFromInput( input, typeof( Vector3 ), out output );
		}

		private static bool ParseVector4( string input, out object output )
		{
			return CreateVectorFromInput( input, typeof( Vector4 ), out output );
		}

		private static bool ParseGameObject( string input, out object output )
		{
			output = GameObject.Find( input );
			return true;
		}

		// Create a vector of specified type (fill the blank slots with 0 or ignore unnecessary slots)
		private static bool CreateVectorFromInput( string input, Type vectorType, out object output )
		{
			List<string> tokens = new List<string>( input.Replace( ',', ' ' ).Trim().Split( ' ' ) );

			int i;
			for( i = tokens.Count - 1; i >= 0; i-- )
			{
				tokens[i] = tokens[i].Trim();
				if( tokens[i].Length == 0 )
					tokens.RemoveAt( i );
			}

			float[] tokenValues = new float[tokens.Count];
			for( i = 0; i < tokens.Count; i++ )
			{
				float val;
				if( !float.TryParse( tokens[i], out val ) )
				{
					if( vectorType == typeof( Vector3 ) )
						output = new Vector3();
					else if( vectorType == typeof( Vector2 ) )
						output = new Vector2();
					else
						output = new Vector4();

					return false;
				}

				tokenValues[i] = val;
			}

			if( vectorType == typeof( Vector3 ) )
			{
				Vector3 result = new Vector3();

				for( i = 0; i < tokenValues.Length && i < 3; i++ )
					result[i] = tokenValues[i];

				for( ; i < 3; i++ )
					result[i] = 0;

				output = result;
			}
			else if( vectorType == typeof( Vector2 ) )
			{
				Vector2 result = new Vector2();

				for( i = 0; i < tokenValues.Length && i < 2; i++ )
					result[i] = tokenValues[i];

				for( ; i < 2; i++ )
					result[i] = 0;

				output = result;
			}
			else
			{
				Vector4 result = new Vector4();

				for( i = 0; i < tokenValues.Length && i < 4; i++ )
					result[i] = tokenValues[i];

				for( ; i < 4; i++ )
					result[i] = 0;

				output = result;
			}

			return true;
		}
	}
}