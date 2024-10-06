using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace IngameDebugConsole
{
	public static class TypeSearch
	{
		public static IEnumerable<IConsoleMethodInfo> GetConsoleMethods(params Type[] types)
		{
			Dictionary<int, List<IConsoleMethodInfo>> methods = new();
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
						yield return new ParserConsoleMethodInfo(method, parserAttribute);
					}

					foreach (ConsoleMethodAttribute command in consoleMethods)
					{
						IConsoleMethodInfo commandMethod = new CommandConsoleMethodInfo(method, command);
						maxOrder = Add(methods, commandMethod, maxOrder);
					}
				}
			}

			for (int i = 1; i <= maxOrder; i++)
			{
				if (!methods.TryGetValue(i, out List<IConsoleMethodInfo> list))
					continue;

				for (int m = 0; m < list.Count; m++)
				{
					yield return list[m];
				}
			}
		}

		private static int Add(Dictionary<int, List<IConsoleMethodInfo>> methods, IConsoleMethodInfo method, int currentMaxOrder)
		{
			int order = method.Order;
			if (!methods.TryGetValue(order, out List<IConsoleMethodInfo> list))
			{
				list = new List<IConsoleMethodInfo>();
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