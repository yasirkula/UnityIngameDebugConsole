using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace IngameDebugConsole
{
	public readonly struct TypeSearch
	{
		public readonly struct Parser
		{
			public readonly MethodInfo method;
			public readonly ConsoleParserAttribute attribute;

			public Parser(MethodInfo method, ConsoleParserAttribute parserAttribute)
			{
				this.method = method;
				attribute = parserAttribute;
			}

			public readonly bool TryBuildFunction(out DebugLogConsole.ParseFunction function)
			{
				if (!method.IsStatic)
				{
					LogParserMethodError("Method must be static.");
					function = null;
					return false;
				}

				if (method.ReturnType != typeof(bool))
				{
					LogParserMethodError("Return type must be bool.");
					function = null;
					return false;
				}

				ParameterInfo[] parameters = method.GetParameters();

				if (parameters.Length != 2)
				{
					LogParserMethodError("Parameter count must be 2.");
					function = null;
					return false;
				}

				if (parameters[0].ParameterType != typeof(string))
				{
					LogParserMethodError("The first parameter must be of type string.");
					function = null;
					return false;
				}

				if (!parameters[1].IsOut)
				{
					LogParserMethodError("The second parameter must be a out parameter.");
					function = null;
					return false;
				}

				Type param2 = parameters[1].ParameterType;
				if (param2 != typeof(object).MakeByRefType())
				{
					LogParserMethodError("The second parameter must be of type object.");
					function = null;
					return false;
				}

				try
				{
					function = (DebugLogConsole.ParseFunction)Delegate.CreateDelegate(typeof(DebugLogConsole.ParseFunction), method);
					return true;
				}
				catch (Exception e)
				{
					LogParserMethodError(e.Message);
					function = null;
					return false;
				}
			}

			private void LogParserMethodError(string message)
			{
				const string format = "Parser Method {0}.{1} is Invalid.\n{2}\nex: public static bool {1}(string input, out {3}|object result)";
				string error = string.Format(format, method.DeclaringType.FullName, method.Name, message, attribute.type.Name);
				Debug.LogError(error);
			}
		}
		public readonly struct Command
		{
			public readonly MethodInfo method;
			public readonly ConsoleMethodAttribute attribute;

			public Command(MethodInfo method, ConsoleMethodAttribute attribute)
			{
				this.method = method;
				this.attribute = attribute;
			}
		}

		public readonly Type type;
		public readonly IReadOnlyList<Parser> parsers;
		public readonly IReadOnlyList<Command> commands;

		public TypeSearch(Type type)
		{
			this.type = type;
			MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);

			List<Parser> parsers = new List<Parser>();
			List<Command> commands = new List<Command>();

			for (int i = 0; i < methods.Length; i++)
			{
				MethodInfo method = methods[i];

				ConsoleParserAttribute parserAttribute = method.GetCustomAttribute<ConsoleParserAttribute>(false);
				IEnumerable<ConsoleMethodAttribute> consoleMethods = method.GetCustomAttributes<ConsoleMethodAttribute>(false);

				if (consoleMethods.Any() && parserAttribute != null)
				{
					const string errorFormat = "Method {0}.{1} cannot be both a Console Parser and a Console Command.";
					Debug.LogError(string.Format(errorFormat, type.FullName, method.Name));
					continue;
				}

				if (parserAttribute != null)
				{
					parsers.Add(new Parser(method, parserAttribute));
				}

				foreach (ConsoleMethodAttribute command in consoleMethods)
				{
					commands.Add(new Command(method, command));
				}
			}

			this.parsers = parsers;
			this.commands = commands;
		}
	}
}