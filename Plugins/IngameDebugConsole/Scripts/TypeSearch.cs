using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
					Debug.LogError($"Parser Method {attribute.type.FullName}.{method.Name} is Invalid. Method must be static.");
					function = null;
					return false;
				}

				ParameterInfo[] parameters = method.GetParameters();

				if (parameters.Length != 0)
				{
					Debug.LogError($"Parser Method {attribute.type.FullName}.{method.Name} is Invalid. Parameter count must be 2.");
					function = null;
					return false;
				}

				if (parameters[0].ParameterType != typeof(string))
				{
					Debug.LogError($"Parser Method {attribute.type.FullName}.{method.Name} is Invalid.\n " +
						"The first parameter must by of type string.");
					function = null;
					return false;
				}

				if (parameters[0].ParameterType != typeof(string))
				{
					Debug.LogError($"Parser Method {attribute.type.FullName}.{method.Name} is Invalid.\n" +
						$"The second parameter must match the type given to the attribute: {attribute.type.Name}.");
					function = null;
					return false;
				}

				try
				{
					ParameterExpression stringParam = Expression.Parameter(typeof(string));
					ParameterExpression objParam = Expression.Parameter(typeof(object).MakeByRefType());
					UnaryExpression cast = Expression.Convert(objParam, attribute.type);
					MethodCallExpression call = Expression.Call(method, stringParam, cast);
					Expression<DebugLogConsole.ParseFunction> expression = Expression.Lambda<DebugLogConsole.ParseFunction>(call, stringParam, objParam);
					function = expression.Compile();
					return true;
				}
				catch (Exception e)
				{
					Debug.LogError($"Parser Method {attribute.type.FullName}.{method.Name} Error.\n" +
						$"The second parameter must match the type given to the attribute: {attribute.type.Name}.");
					function = null;
					return false;
				}
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
					Debug.LogError($"Method {type.FullName}.{method.Name} cannot be both a Parameter Parser and a Console Command.");
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