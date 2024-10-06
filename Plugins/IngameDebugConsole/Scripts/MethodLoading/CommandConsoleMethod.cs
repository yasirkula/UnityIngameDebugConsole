using System;
using System.Reflection;

namespace IngameDebugConsole
{
	public readonly struct CommandConsoleMethod : IConsoleMethod
	{
		public MethodInfo Method { get; }
		public int Order => 1;
		public readonly ConsoleMethodAttribute attribute;

		public CommandConsoleMethod(MethodInfo method, ConsoleMethodAttribute attribute)
		{
			this.Method = method;
			this.attribute = attribute;
		}

		public void Load()
		{
			Delegate func = Delegate.CreateDelegate(typeof(Delegate), Method);
			DebugLogConsole.AddCommand(attribute.Command, attribute.Description, func, attribute.ParameterNames);
		}
	}
}