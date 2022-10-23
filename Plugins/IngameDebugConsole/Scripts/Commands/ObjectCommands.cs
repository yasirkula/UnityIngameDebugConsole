using System.Collections.Generic;
using UnityEngine;
using Type = System.Type;
using System.Reflection;
using System;

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

			List<Type> allFoundTypes = new List<Type>();
			foreach ( Assembly a in AppDomain.CurrentDomain.GetAssemblies() )
				foreach ( Type t in a.GetTypes() ) allFoundTypes.Add( t );
			if ( exactTypeName )
				searchType = allFoundTypes.Find( t => t.FullName == typeName );
			else searchType = allFoundTypes.Find( t => t.Name.ToLower().Contains( typeName ) );

			if ( searchType == null ) return;
			UnityEngine.Object foundObj = UnityEngine.Object.FindObjectOfType( searchType, true );
			if ( foundObj == null ) return;
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
			List<string> lines = new List<string>();
			foreach ( KeyValuePair<string, string> p in TypeAliases )
				lines.Add( "[" + p.Key + "] = " + p.Value );
			return string.Join( "\n", lines.ToArray() );
		}
	}
}
