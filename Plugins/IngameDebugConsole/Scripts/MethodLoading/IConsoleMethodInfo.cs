using System.Reflection;

namespace IngameDebugConsole
{
	public interface IConsoleMethodInfo
	{
		MethodInfo Method { get; }
		int Order { get; }

		void Load();
	}
}