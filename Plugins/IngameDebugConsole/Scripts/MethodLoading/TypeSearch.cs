using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace IngameDebugConsole
{
	public static class TypeSearch
	{
		public static IEnumerable<IConsoleMethod> GetConsoleMethods(params Type[] types)
		{
			Dictionary<int, List<IConsoleMethod>> methods = new();
			int maxOrder = 0;

			for (int t = 0; t < types.Length; t++)
			{
				Type type = types[t];
				MethodInfo[] typeMethods = GetMethods(type);

				for (int m = 0; m < typeMethods.Length; m++)
				{
					MethodInfo method = typeMethods[m];

					ConsoleCustomTypeParserAttribute parserAttribute = method.GetCustomAttribute<ConsoleCustomTypeParserAttribute>(false);
					IEnumerable<ConsoleMethodAttribute> consoleMethods = method.GetCustomAttributes<ConsoleMethodAttribute>(false);

					if (consoleMethods.Any() && parserAttribute != null)
					{
						const string errorFormat = "Method {0}.{1} cannot be both a Console Parser and a Console Command.";
						Debug.LogError(string.Format(errorFormat, type.FullName, method.Name));
						continue;
					}

					if (parserAttribute != null)
					{
						yield return new ParserConsoleMethod(method, parserAttribute);
					}

					foreach (ConsoleMethodAttribute command in consoleMethods)
					{
						IConsoleMethod commandMethod = new CommandConsoleMethod(method, command);
						maxOrder = Add(methods, commandMethod, maxOrder);
					}
				}
			}

			for (int i = 0; i < maxOrder; i++)
			{
				if (!methods.TryGetValue(i, out List<IConsoleMethod> list))
					continue;

				for (int m = 0; m < list.Count; m++)
				{
					yield return list[m];
				}
			}
		}

		private static int Add(Dictionary<int, List<IConsoleMethod>> methods, IConsoleMethod method, int currentMaxOrder)
		{
			int order = method.Order;
			if (!methods.TryGetValue(order, out List<IConsoleMethod> list))
			{
				list = new List<IConsoleMethod>();
				methods[order] = list;
			}

			list.Add(method);

			return Math.Max(order, currentMaxOrder);
		}

		private static MethodInfo[] GetMethods(Type type)
		{
			return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
		}
	}
}