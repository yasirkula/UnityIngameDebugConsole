using System.Reflection;

namespace IngameDebugConsole
{
	public readonly struct ParserConsoleMethod : IConsoleMethod
	{
		public MethodInfo Method { get; }
		public int Order => 0;
		public readonly ConsoleCustomTypeParserAttribute attribute;

		public ParserConsoleMethod(MethodInfo method, ConsoleCustomTypeParserAttribute parserAttribute)
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