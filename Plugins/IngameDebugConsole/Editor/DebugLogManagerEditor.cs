using UnityEditor;

namespace IngameDebugConsole
{
	[CustomEditor( typeof( DebugLogManager ) )]
	public class DebugLogManagerEditor : Editor
	{
		private SerializedProperty singleton;
		private SerializedProperty minimumHeight;
		private SerializedProperty enableHorizontalResizing;
		private SerializedProperty resizeFromRight;
		private SerializedProperty minimumWidth;
		private SerializedProperty enablePopup;
		private SerializedProperty startInPopupMode;
		private SerializedProperty startMinimized;
		private SerializedProperty toggleWithKey;
		private SerializedProperty toggleKey;
		private SerializedProperty enableSearchbar;
		private SerializedProperty topSearchbarMinWidth;
		private SerializedProperty captureLogTimestamps;
		private SerializedProperty alwaysDisplayTimestamps;
		private SerializedProperty clearCommandAfterExecution;
		private SerializedProperty commandHistorySize;
		private SerializedProperty showCommandSuggestions;
		private SerializedProperty receiveLogcatLogsInAndroid;
		private SerializedProperty logcatArguments;

		private void OnEnable()
		{
			singleton = serializedObject.FindProperty( "singleton" );
			minimumHeight = serializedObject.FindProperty( "minimumHeight" );
			enableHorizontalResizing = serializedObject.FindProperty( "enableHorizontalResizing" );
			resizeFromRight = serializedObject.FindProperty( "resizeFromRight" );
			minimumWidth = serializedObject.FindProperty( "minimumWidth" );
			enablePopup = serializedObject.FindProperty( "enablePopup" );
			startInPopupMode = serializedObject.FindProperty( "startInPopupMode" );
			startMinimized = serializedObject.FindProperty( "startMinimized" );
			toggleWithKey = serializedObject.FindProperty( "toggleWithKey" );
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			toggleKey = serializedObject.FindProperty( "toggleBinding" );
#else
			toggleKey = serializedObject.FindProperty( "toggleKey" );
#endif
			enableSearchbar = serializedObject.FindProperty( "enableSearchbar" );
			topSearchbarMinWidth = serializedObject.FindProperty( "topSearchbarMinWidth" );
			captureLogTimestamps = serializedObject.FindProperty( "captureLogTimestamps" );
			alwaysDisplayTimestamps = serializedObject.FindProperty( "alwaysDisplayTimestamps" );
			clearCommandAfterExecution = serializedObject.FindProperty( "clearCommandAfterExecution" );
			commandHistorySize = serializedObject.FindProperty( "commandHistorySize" );
			showCommandSuggestions = serializedObject.FindProperty( "showCommandSuggestions" );
			receiveLogcatLogsInAndroid = serializedObject.FindProperty( "receiveLogcatLogsInAndroid" );
			logcatArguments = serializedObject.FindProperty( "logcatArguments" );
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField( singleton );
			EditorGUILayout.PropertyField( minimumHeight );

			EditorGUILayout.PropertyField( enableHorizontalResizing );
			if( enableHorizontalResizing.boolValue )
			{
				DrawSubProperty( resizeFromRight );
				DrawSubProperty( minimumWidth );
			}

			EditorGUILayout.PropertyField( enablePopup );
			if( enablePopup.boolValue )
				DrawSubProperty( startInPopupMode );
			else
				DrawSubProperty( startMinimized );

			EditorGUILayout.PropertyField( toggleWithKey );
			if( toggleWithKey.boolValue )
				DrawSubProperty( toggleKey );

			EditorGUILayout.PropertyField( enableSearchbar );
			if( enableSearchbar.boolValue )
				DrawSubProperty( topSearchbarMinWidth );

			EditorGUILayout.PropertyField( captureLogTimestamps );
			if( captureLogTimestamps.boolValue )
				DrawSubProperty( alwaysDisplayTimestamps );

			EditorGUILayout.PropertyField( clearCommandAfterExecution );
			EditorGUILayout.PropertyField( commandHistorySize );
			EditorGUILayout.PropertyField( showCommandSuggestions );

			EditorGUILayout.PropertyField( receiveLogcatLogsInAndroid );
			if( receiveLogcatLogsInAndroid.boolValue )
				DrawSubProperty( logcatArguments );

			DrawPropertiesExcluding( serializedObject, "m_Script" );
			serializedObject.ApplyModifiedProperties();
		}

		private void DrawSubProperty( SerializedProperty property )
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField( property );
			EditorGUI.indentLevel--;
		}
	}
}