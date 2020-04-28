package com.yasirkula.unity;

import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;

/**
 * Created by yasirkula on 6.04.2020.
 */

public class DebugConsole
{
	public static void CopyText( Context context, String text )
	{
		ClipboardManager clipboard = (ClipboardManager) context.getSystemService( Context.CLIPBOARD_SERVICE );
		ClipData clip = ClipData.newPlainText( "log", text );
		clipboard.setPrimaryClip( clip );
	}
}