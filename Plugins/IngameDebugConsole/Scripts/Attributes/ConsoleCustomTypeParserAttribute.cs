using System;

namespace IngameDebugConsole
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public class ConsoleCustomTypeParserAttribute : ConsoleAttribute
	{
		public readonly Type type;
		public readonly string readableName;

		public override int Order { get { return 0; } }

		public ConsoleCustomTypeParserAttribute(Type type, string readableName = null)
		{
			this.type = type;
			this.readableName = readableName;
		}

		public override void Load()
		{
			DebugLogConsole.AddCustomParameterType(type, (DebugLogConsole.ParseFunction)Delegate.CreateDelegate(typeof(DebugLogConsole.ParseFunction), Method), readableName);
		}
	}
}