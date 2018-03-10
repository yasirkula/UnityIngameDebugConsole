#if !UNITY_EDITOR && UNITY_ANDROID
using System.Collections.Generic;
using UnityEngine;
#endif

// Credit: https://stackoverflow.com/a/41018028/2373034
namespace IngameDebugConsole
{
	public class DebugLogLogcatListener
#if !UNITY_EDITOR && UNITY_ANDROID
	: AndroidJavaProxy
	{
		private Queue<string> queuedLogs;
		private AndroidJavaObject nativeObject;

		public DebugLogLogcatListener() : base( "com.yasirkula.unity.LogcatLogReceiver" )
		{
			queuedLogs = new Queue<string>( 16 );
		}

		~DebugLogLogcatListener()
		{
			Stop();

			if( nativeObject != null )
				nativeObject.Dispose();
		}

		public void Start( string arguments )
		{
			if( nativeObject == null )
				nativeObject = new AndroidJavaObject( "com.yasirkula.unity.LogcatLogger" );

			nativeObject.Call( "Start", this, arguments );
		}

		public void Stop()
		{
			if( nativeObject != null )
				nativeObject.Call( "Stop" );
		}

		public void OnLogReceived( string log )
		{
			queuedLogs.Enqueue( log );
		}

		public string GetLog()
		{
			if( queuedLogs.Count > 0 )
				return queuedLogs.Dequeue();

			return null;
		}
	}
#else
	{
		public void Start( string arguments )
		{
		}

		public void Stop()
		{
		}

		public string GetLog()
		{
			return null;
		}
	}
#endif
}