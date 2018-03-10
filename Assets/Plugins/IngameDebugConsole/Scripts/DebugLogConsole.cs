using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

// In-game Debug Console / DebugLogConsole
// Author: Suleyman Yasir Kula
// 
// Manages the console commands, parses console input and handles execution of commands
// Supported method parameter types: int, float, bool, string, Vector2, Vector3, Vector4

// Helper class to store important information about a command
namespace IngameDebugConsole
{
	public class ConsoleMethodInfo
	{
		public readonly MethodInfo method;
		public readonly System.Type[] parameterTypes;
		public readonly object instance;

		public readonly string signature;

		public ConsoleMethodInfo( MethodInfo method, System.Type[] parameterTypes, object instance, string signature )
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
		// All the commands
		private static Dictionary<string, ConsoleMethodInfo> methods;

		static DebugLogConsole()
		{
			methods = new Dictionary<string, ConsoleMethodInfo>();

			// help is the default command that lists all the available commands in the console
			AddCommandStatic( "help", "LogAllCommands", typeof( DebugLogConsole ) );
		}

		// Logs the list of available commands
		public static void LogAllCommands()
		{
			System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder( "Available commands:" );
			
			foreach( var entry in methods )
			{
				if( entry.Value.IsValid() )
					stringBuilder.Append( "\n- " ).Append( entry.Value.signature );
			}

			Debug.Log( stringBuilder.Append( "\n" ).ToString() );
		}

		// Add a command related with an instance method (i.e. non static method)
		public static void AddCommandInstance( string command, string methodName, object instance )
		{
			if( instance == null )
			{
				Debug.LogError( "Instance can't be null!" );
				return;
			}

			AddCommand( command, methodName, instance.GetType(), instance );
		}

		// Add a command related with a static method (i.e. no instance is required to call the method)
		public static void AddCommandStatic( string command, string methodName, System.Type ownerType )
		{
			AddCommand( command, methodName, ownerType );
		}

		// Remove a command from the console
		public static void RemoveCommand( string command )
		{
			if( !string.IsNullOrEmpty( command ) )
				methods.Remove( command );
		}

		// Create a new command and set its properties
		private static void AddCommand( string command, string methodName, System.Type ownerType, object instance = null )
		{
			// Get the method from the class
			MethodInfo method = ownerType.GetMethod( methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static );
			if( method == null )
			{
				Debug.LogError( methodName + " does not exist in " + ownerType );
				return;
			}

			// Fetch the parameters of the class
			ParameterInfo[] parameters = method.GetParameters();
			if( parameters == null || parameters.Length == 0 )
			{
				// Method takes no parameters
				
				string methodSignature = method.ToString();
				methodSignature = methodSignature.Replace( "Void ", "" );
				methodSignature = command + ": " + ownerType.ToString() + "." + methodSignature;

				// Associate the method with the entered command
				methods[command] = new ConsoleMethodInfo( method, new System.Type[] { }, instance, methodSignature );
			}
			else
			{
				// Method takes parameter(s), check their types 
				// to see if this method is valid (can be called by a command)

				bool isMethodValid = true;

				// Store the parameter types in an array
				System.Type[] parameterTypes = new System.Type[parameters.Length];
				for( int k = 0; k < parameters.Length; k++ )
				{
					System.Type parameterType = parameters[k].ParameterType;
					if( parameterType == typeof( int ) || parameterType == typeof( float ) || parameterType == typeof( bool ) || parameterType == typeof( string ) ||
						parameterType == typeof( Vector2 ) || parameterType == typeof( Vector3 ) || parameterType == typeof( Vector4 ) )
					{
						parameterTypes[k] = parameterType;
					}
					else
					{
						isMethodValid = false;
						break;
					}
				}

				// If method is valid, associate it with the entered command
				if( isMethodValid )
				{
					string methodSignature = method.ToString();
					methodSignature = methodSignature.Replace( "Int32", "Integer" );
					methodSignature = methodSignature.Replace( "Single", "Float" );
					methodSignature = methodSignature.Replace( "System.", "" );
					methodSignature = methodSignature.Replace( "Void ", "" );
					methodSignature = command + ": " + ownerType.ToString() + "." + methodSignature;

					methods[command] = new ConsoleMethodInfo( method, parameterTypes, instance, methodSignature );
				}
			}
		}

		// Parse the command and try to execute it
		public static void ExecuteCommand( string command )
		{
			if( command == null || command.Length == 0 )
				return;

			// Don't split string and vector inputs into pieces (yet)
			char[] commandChars = command.ToCharArray();
			bool inQuote = false;
			bool inVector = false;
			for( int i = 0; i < command.Length; i++ )
			{
				if( commandChars[i] == '"' )
				{
					inQuote = !inQuote;
				}
				else if( commandChars[i] == '[' || commandChars[i] == '(' )
				{
					if( !inQuote )
						inVector = true;
				}
				else if( commandChars[i] == ']' || commandChars[i] == ')' )
				{
					if( !inQuote )
						inVector = false;
				}
				else if( commandChars[i] == ' ' )
				{
					if( !inQuote && !inVector )
						commandChars[i] = '\n';
				}
			}

			if( inQuote || inVector )
			{
				Debug.LogError( "Either string ( \" ) or vector ( [ ) is not closed" );
				return;
			}

			// Split the command into pieces
			string[] commandArguments = new string( commandChars ).Split( '\n' );

			// Let me know if it happens?
			if( commandArguments.Length == 0 )
			{
				Debug.LogError( "???" );
				return;
			}

			// Check if command exists
			ConsoleMethodInfo methodInfo;
			if( methods.TryGetValue( commandArguments[0], out methodInfo ) && methodInfo.IsValid() )
			{
				// Check if number of parameter match
				if( methodInfo.parameterTypes.Length != commandArguments.Length - 1 )
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

					System.Type parameterType = methodInfo.parameterTypes[i];
					if( parameterType == typeof( bool ) )
					{
						argument = argument.ToLowerInvariant();
						if( argument == "true" || argument == "1" )
						{
							parameters[i] = true;
						}
						else if( argument == "false" || argument == "0" )
						{
							parameters[i] = false;
						}
						else
						{
							Debug.LogError( "Can't convert " + argument + " to boolean" );
							return;
						}
					}
					else if( parameterType == typeof( float ) )
					{
						float value;
						if( float.TryParse( argument, out value ) )
						{
							parameters[i] = value;
						}
						else
						{
							Debug.LogError( "Can't convert " + argument + " to float" );
							return;
						}
					}
					else if( parameterType == typeof( int ) )
					{
						float value;
						if( float.TryParse( argument, out value ) )
						{
							parameters[i] = (int) value;
						}
						else
						{
							Debug.LogError( "Can't convert " + argument + " to float" );
							return;
						}
					}
					else if( parameterType == typeof( Vector2 ) || parameterType == typeof( Vector3 ) || parameterType == typeof( Vector4 ) )
					{
						if( argument.Length < 2 )
						{
							Debug.LogError( "Vector is not valid: " + argument + ". Don't forget to wrap it with square brackets ( [] )" );
							return;
						}

						// Split vector into pieces and parse each piece separately
						string[] vectorArgs = argument.Substring( 1, argument.Length - 2 ).Split( ' ' );
						float[] vectorVals = new float[vectorArgs.Length];

						if( vectorArgs.Length == 1 && vectorArgs[0].Length == 0 )
						{
							vectorArgs = new string[0];
							vectorVals = new float[0];
						}

						if( vectorArgs.Length <= 4 )
						{
							for( int j = 0; j < vectorArgs.Length; j++ )
							{
								float value;
								if( float.TryParse( vectorArgs[j], out value ) )
								{
									vectorVals[j] = value;
								}
								else
								{
									Debug.LogError( "Can't convert " + vectorArgs[j] + " to float" );
									return;
								}
							}
						}
						else
						{
							Debug.LogError( "There is no vector struct of size " + vectorArgs.Length );
							return;
						}

						// Create a vector object that matches the type of the parameter
						parameters[i] = CreateVectorFromInput( vectorVals, parameterType );
					}
					else if( parameterType == typeof( string ) )
					{
						if( argument.Length < 2 )
						{
							Debug.LogError( "String is not valid: " + argument + ". Don't forget to wrap it with quotes ( \" )" );
							return;
						}

						parameters[i] = argument.Substring( 1, argument.Length - 2 );
					}
					else
					{
						Debug.LogError( "Unexpected type: " + parameterType );
						return;
					}
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
			else
			{
				Debug.LogWarning( "Can't find command: " + commandArguments[0] );
			}
		}

		// Create a vector of specified type (fill the blank slots with 0 or ignore unnecessary slots)
		private static object CreateVectorFromInput( float[] values, System.Type vectorType )
		{
			int i;
			if( vectorType == typeof( Vector4 ) )
			{
				Vector4 result = new Vector4();

				for( i = 0; i < values.Length && i < 4; i++ )
				{
					result[i] = values[i];
				}

				for( ; i < 4; i++ )
				{
					result[i] = 0;
				}

				return result;
			}
			else if( vectorType == typeof( Vector3 ) )
			{
				Vector3 result = new Vector3();

				for( i = 0; i < values.Length && i < 3; i++ )
				{
					result[i] = values[i];
				}

				for( ; i < 3; i++ )
				{
					result[i] = 0;
				}

				return result;
			}
			else
			{
				Vector2 result = new Vector2();

				for( i = 0; i < values.Length && i < 2; i++ )
				{
					result[i] = values[i];
				}

				for( ; i < 2; i++ )
				{
					result[i] = 0;
				}

				return result;
			}
		}
	}
}