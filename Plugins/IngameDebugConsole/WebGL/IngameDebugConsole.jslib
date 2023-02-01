mergeInto( LibraryManager.library,
{
	IngameDebugConsoleStartCopy: function( textToCopy )
	{
		var textToCopyJS = UTF8ToString( textToCopy );
		
		// Delete if element exist
		var copyTextButton = document.getElementById( 'DebugConsoleCopyButtonGL' );
		if( !copyTextButton )
		{
			copyTextButton = document.createElement( 'button' );
			copyTextButton.setAttribute( 'id', 'DebugConsoleCopyButtonGL' );
			copyTextButton.setAttribute( 'style','display:none; visibility:hidden;' );
		}
		
		copyTextButton.onclick = function( event )
		{
			// Credit: https://stackoverflow.com/a/30810322/2373034
			if( navigator.clipboard )
			{
				navigator.clipboard.writeText( textToCopyJS ).then( function() { }, function( err )
				{
					console.error( "Couldn't copy text to clipboard using clipboard.writeText: ", err );
				} );
			}
			else
			{
				var textArea = document.createElement( 'textarea' );
				textArea.value = textToCopyJS;

				// Avoid scrolling to bottom
				textArea.style.top = "0";
				textArea.style.left = "0";
				textArea.style.position = "fixed";

				document.body.appendChild( textArea );
				textArea.focus();
				textArea.select();

				try
				{
					document.execCommand( 'copy' );
				}
				catch( err )
				{
					console.error( "Couldn't copy text to clipboard using document.execCommand", err );
				}

				document.body.removeChild( textArea );
			}
		};

		document.body.appendChild( copyTextButton );
		document.onmouseup = function()
		{
			document.onmouseup = null;
			copyTextButton.click();
			document.body.removeChild( copyTextButton );
		};
	},
	
	IngameDebugConsoleCancelCopy: function()
	{
		var copyTextButton = document.getElementById( 'DebugConsoleCopyButtonGL' );
		if( copyTextButton )
			document.body.removeChild( copyTextButton );
		
		document.onmouseup = null;
	}
} );