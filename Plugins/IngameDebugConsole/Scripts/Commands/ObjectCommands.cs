using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Type = System.Type;

namespace IngameDebugConsole.Commands
{
	public class ObjectCommands
	{
		public static Dictionary<string, string> TypeAliases
			= new Dictionary<string, string>()
			{
				{ "rin.rh", "RuntimeInspectorNamespace.RuntimeHierarchy" },
				{ "rin.ri", "RuntimeInspectorNamespace.RuntimeInspector" }
			};

		[ConsoleMethod( "obj.toggletype",
			"Finds the first GameObject containing a component with a type"
			+ "name containing the argument, or type alias matching the argument, "
			+ "toggles its activeSelf property" ),
			UnityEngine.Scripting.Preserve]
		public static void ToggleType( string keyword )
		{
			string loweredKeyword = keyword.ToLower();
			string typeName;
			Type searchType;
			bool exactTypeName = false;
			if ( TypeAliases.ContainsKey( loweredKeyword ) )
			{ typeName = TypeAliases[loweredKeyword]; exactTypeName = true; }
			else typeName = loweredKeyword;

			IEnumerable<Type> allFoundTypes = System.AppDomain.CurrentDomain
				.GetAssemblies()
				.Reverse()
				.SelectMany( a => a.GetTypes() );
			if ( exactTypeName )
				searchType = allFoundTypes.FirstOrDefault( t => t.FullName == typeName );
			else searchType = allFoundTypes.FirstOrDefault( t => t.Name.ToLower().Contains( typeName ) );

			if ( searchType == null ) return;
			Object foundObj = Object.FindObjectOfType( searchType, true );
			if ( foundObj == null) return;
			Component foundComp = foundObj as Component;
			if ( foundObj == null ) return;
			foundComp.gameObject.SetActive( !foundComp.gameObject.activeSelf );
		}

		[ConsoleMethod( "obj.typealiases",
			"Returns a list of alias-to-type-name mappings in "
			+ "IngameDebugConsole.Commands.ObjectCommands.TypeAliases and used by "
			+ "the obj.toggletype command" ),
			UnityEngine.Scripting.Preserve]
		public static string ListTypeAliases()
		{
			return string.Join( "\n",
				TypeAliases.Select( p => "[" + p.Key + "] = " + p.Value ) );
		}
	}
}
