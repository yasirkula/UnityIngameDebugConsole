package com.yasirkula.unity;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;

/**
 * Created by yasirkula on 7.11.2017.
 */

public class DebugConsoleLogcatLogger
{
	private static class LogcatWorker implements Runnable
	{
		private DebugConsoleLogcatLogReceiver logReceiver;
		private String command;

		private volatile boolean running = true;

		public LogcatWorker( DebugConsoleLogcatLogReceiver logReceiver, String command )
		{
			this.logReceiver = logReceiver;
			this.command = command;
		}

		@Override
		public void run()
		{
			// Credit: http://chintanrathod.com/read-logs-programmatically-in-android/
			try
			{
				Runtime.getRuntime().exec( "logcat -c" );

				Process process = Runtime.getRuntime().exec( command );
				BufferedReader bufferedReader = new BufferedReader( new InputStreamReader( process.getInputStream() ) );

				String line;
				while( running )
				{
					while( ( line = bufferedReader.readLine() ) != null )
						logReceiver.OnLogReceived( line );

					try
					{
						Thread.sleep( 1000L );
					}
					catch( InterruptedException e )
					{
					}
				}
			}
			catch( IOException e )
			{
			}
		}

		public void terminate()
		{
			running = false;
		}
	}

	private LogcatWorker worker;

	public void Start( DebugConsoleLogcatLogReceiver logReceiver, String arguments )
	{
		Stop();

		if( logReceiver == null )
			return;

		String command = "logcat";
		if( arguments != null )
		{
			arguments = arguments.trim();
			if( arguments.length() > 0 )
				command += " " + arguments;
		}

		worker = new LogcatWorker( logReceiver, command );
		Thread thread = new Thread( worker );
		thread.start();
	}

	public void Stop()
	{
		if( worker != null )
		{
			worker.terminate();
			worker = null;
		}
	}
}