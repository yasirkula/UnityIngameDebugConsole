using System;

namespace IngameDebugConsole
{
	[AttributeUsage( AttributeTargets.Method, Inherited = false, AllowMultiple = true )]
	public class ConsoleMethodAttribute : ConsoleAttribute
	{
		private string m_command;
		private string m_description;
		private string[] m_parameterNames;

		public string Command { get { return m_command; } }
		public string Description { get { return m_description; } }
		public string[] ParameterNames { get { return m_parameterNames; } }

		public override int Order { get { return 1; } }

		public ConsoleMethodAttribute( string command, string description, params string[] parameterNames )
		{
			m_command = command;
			m_description = description;
			m_parameterNames = parameterNames;
		}

		public override void Load()
		{
			DebugLogConsole.AddCommand(Command, Description, Method, null, ParameterNames);
		}
	}
}