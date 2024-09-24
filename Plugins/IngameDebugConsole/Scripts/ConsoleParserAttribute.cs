using System;

namespace IngameDebugConsole
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public class ConsoleParserAttribute : Attribute
	{
		public readonly Type type;
		public readonly string readableName;

		public ConsoleParserAttribute(Type type, string readableName)
		{
			this.type = type;
			this.readableName = readableName;
		}
	}
}