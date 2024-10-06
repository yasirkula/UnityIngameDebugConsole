using System.Reflection;

namespace IngameDebugConsole
{
	public interface IConsoleMethod
	{
		MethodInfo Method { get; }
		int Order { get; }

		void Load();
	}
}