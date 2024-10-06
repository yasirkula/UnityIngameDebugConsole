using System.Reflection;

namespace IngameDebugConsole
{
	public readonly struct CommandConsoleMethodInfo : IConsoleMethodInfo
	{
		public MethodInfo Method { get; }
		public int Order => 1;
		public readonly ConsoleMethodAttribute attribute;

		public CommandConsoleMethodInfo(MethodInfo method, ConsoleMethodAttribute attribute)
		{
			this.Method = method;
			this.attribute = attribute;
		}

		public void Load()
		{
			DebugLogConsole.AddCommand(attribute.Command, attribute.Description, Method, null, attribute.ParameterNames);
		}
	}
}