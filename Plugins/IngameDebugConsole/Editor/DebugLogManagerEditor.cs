using UnityEditor;
using UnityEngine;

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
		private SerializedProperty receiveLogsWhileInactive;
		private SerializedProperty receiveInfoLogs;
		private SerializedProperty receiveWarningLogs;
		private SerializedProperty receiveErrorLogs;
		private SerializedProperty receiveExceptionLogs;
		private SerializedProperty captureLogTimestamps;
		private SerializedProperty alwaysDisplayTimestamps;
		private SerializedProperty queuedLogLimit;
		private SerializedProperty clearCommandAfterExecution;
		private SerializedProperty commandHistorySize;
		private SerializedProperty showCommandSuggestions;
		private SerializedProperty receiveLogcatLogsInAndroid;
		private SerializedProperty logcatArguments;
		private SerializedProperty avoidScreenCutout;
		private SerializedProperty popupAvoidsScreenCutout;
		private SerializedProperty autoFocusOnCommandInputField;

		private readonly GUIContent receivedLogTypesLabel = new GUIContent( "Received Log Types", "Only these logs will be received by the console window, other logs will simply be skipped" );
		private readonly GUIContent receiveInfoLogsLabel = new GUIContent( "Info" );
		private readonly GUIContent receiveWarningLogsLabel = new GUIContent( "Warning" );
		private readonly GUIContent receiveErrorLogsLabel = new GUIContent( "Error" );
		private readonly GUIContent receiveExceptionLogsLabel = new GUIContent( "Exception" );

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
			receiveLogsWhileInactive = serializedObject.FindProperty( "receiveLogsWhileInactive" );
			receiveInfoLogs = serializedObject.FindProperty( "receiveInfoLogs" );
			receiveWarningLogs = serializedObject.FindProperty( "receiveWarningLogs" );
			receiveErrorLogs = serializedObject.FindProperty( "receiveErrorLogs" );
			receiveExceptionLogs = serializedObject.FindProperty( "receiveExceptionLogs" );
			captureLogTimestamps = serializedObject.FindProperty( "captureLogTimestamps" );
			alwaysDisplayTimestamps = serializedObject.FindProperty( "alwaysDisplayTimestamps" );
			queuedLogLimit = serializedObject.FindProperty( "queuedLogLimit" );
			clearCommandAfterExecution = serializedObject.FindProperty( "clearCommandAfterExecution" );
			commandHistorySize = serializedObject.FindProperty( "commandHistorySize" );
			showCommandSuggestions = serializedObject.FindProperty( "showCommandSuggestions" );
			receiveLogcatLogsInAndroid = serializedObject.FindProperty( "receiveLogcatLogsInAndroid" );
			logcatArguments = serializedObject.FindProperty( "logcatArguments" );
			avoidScreenCutout = serializedObject.FindProperty( "avoidScreenCutout" );
			popupAvoidsScreenCutout = serializedObject.FindProperty( "popupAvoidsScreenCutout" );
			autoFocusOnCommandInputField = serializedObject.FindProperty( "autoFocusOnCommandInputField" );
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField( singleton );

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField( minimumHeight );

			EditorGUILayout.PropertyField( enableHorizontalResizing );
			if( enableHorizontalResizing.boolValue )
			{
				DrawSubProperty( resizeFromRight );
				DrawSubProperty( minimumWidth );
			}

			EditorGUILayout.PropertyField( avoidScreenCutout );
			DrawSubProperty( popupAvoidsScreenCutout );

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField( enablePopup );
			if( enablePopup.boolValue )
				DrawSubProperty( startInPopupMode );
			else
				DrawSubProperty( startMinimized );

			EditorGUILayout.PropertyField( toggleWithKey );
			if( toggleWithKey.boolValue )
				DrawSubProperty( toggleKey );

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField( enableSearchbar );
			if( enableSearchbar.boolValue )
				DrawSubProperty( topSearchbarMinWidth );

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField( receiveLogsWhileInactive );

			EditorGUILayout.PrefixLabel( receivedLogTypesLabel );
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField( receiveInfoLogs, receiveInfoLogsLabel );
			EditorGUILayout.PropertyField( receiveWarningLogs, receiveWarningLogsLabel );
			EditorGUILayout.PropertyField( receiveErrorLogs, receiveErrorLogsLabel );
			EditorGUILayout.PropertyField( receiveExceptionLogs, receiveExceptionLogsLabel );
			EditorGUI.indentLevel--;

			EditorGUILayout.PropertyField( receiveLogcatLogsInAndroid );
			if( receiveLogcatLogsInAndroid.boolValue )
				DrawSubProperty( logcatArguments );

			EditorGUILayout.PropertyField( captureLogTimestamps );
			if( captureLogTimestamps.boolValue )
				DrawSubProperty( alwaysDisplayTimestamps );

			EditorGUILayout.PropertyField( queuedLogLimit );

			EditorGUILayout.Space();

			EditorGUILayout.PropertyField( clearCommandAfterExecution );
			EditorGUILayout.PropertyField( commandHistorySize );
			EditorGUILayout.PropertyField( showCommandSuggestions );
			EditorGUILayout.PropertyField( autoFocusOnCommandInputField );

			EditorGUILayout.Space();

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