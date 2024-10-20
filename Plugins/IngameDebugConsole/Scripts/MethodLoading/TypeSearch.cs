using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace IngameDebugConsole
{
	public static class TypeSearch
	{
		public static IEnumerable<ConsoleAttribute> GetConsoleMethods(params Type[] types)
		{
			Dictionary<int, List<ConsoleAttribute>> methods = new();
			int maxOrder = 0;

			for (int t = 0; t < types.Length; t++)
			{
				Type type = types[t];
				MethodInfo[] typeMethods = GetMethods(type);

				for (int m = 0; m < typeMethods.Length; m++)
				{
					MethodInfo method = typeMethods[m];

					IEnumerable<ConsoleAttribute> attributes = method.GetCustomAttributes<ConsoleAttribute>(true);

					foreach (ConsoleAttribute attribute in attributes)
					{
						attribute.SetMethod(method);
						if (attribute.Order < 1)
						{
							yield return attribute;
						}
						else
						{
							maxOrder = Add(methods, attribute, maxOrder);
						}
					}
				}
			}

			for (int i = 1; i <= maxOrder; i++)
			{
				if (!methods.TryGetValue(i, out List<ConsoleAttribute> list))
					continue;

				for (int m = 0; m < list.Count; m++)
				{
					yield return list[m];
				}
			}
		}

		private static int Add(Dictionary<int, List<ConsoleAttribute>> methods, ConsoleAttribute attribute, int currentMaxOrder)
		{
			int order = attribute.Order;
			if (!methods.TryGetValue(order, out List<ConsoleAttribute> list))
			{
				list = new List<ConsoleAttribute>();
				methods[order] = list;
			}

			list.Add(attribute);

			return Math.Max(order, currentMaxOrder);
		}

		private static MethodInfo[] GetMethods(Type type)
		{
			return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
		}
	}
}