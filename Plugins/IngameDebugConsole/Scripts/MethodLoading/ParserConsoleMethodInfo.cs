using System.Reflection;

namespace IngameDebugConsole
{
	public readonly struct ParserConsoleMethodInfo : IConsoleMethodInfo
	{
		public MethodInfo Method { get; }
		public int Order => 0;
		public readonly ConsoleCustomTypeParserAttribute attribute;

		public ParserConsoleMethodInfo(MethodInfo method, ConsoleCustomTypeParserAttribute parserAttribute)
		{
			this.Method = method;
			attribute = parserAttribute;
		}

		public void Load()
		{
			DebugLogConsole.AddCustomParameterType(Method, attribute.type, attribute.readableName);
		}
	}
}