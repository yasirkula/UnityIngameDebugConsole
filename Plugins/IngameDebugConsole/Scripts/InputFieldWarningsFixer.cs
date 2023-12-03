using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace IngameDebugConsole
{
	// Fixes: https://github.com/yasirkula/UnityIngameDebugConsole/issues/77
	// This was caused by Canvas.ForceUpdateCanvases in InputField.UpdateLabel (added in 2022.1 to resolve another bug: https://issuetracker.unity3d.com/issues/input-fields-width-doesnt-change-after-entering-specific-combinations-of-text-when-the-content-size-fitter-is-used)
	// which is triggered from InputField.OnValidate. UpdateLabel isn't invoked if a variable called m_PreventFontCallback is true,
	// which is what this component is doing: temporarily switching that variable before InputField.OnValidate to avoid this issue.
#if UNITY_2022_1_OR_NEWER && UNITY_EDITOR
	[DefaultExecutionOrder( -50 )]
#endif
	public class InputFieldWarningsFixer : MonoBehaviour
	{
#if UNITY_2022_1_OR_NEWER && UNITY_EDITOR
		private static readonly FieldInfo preventFontCallback = typeof( InputField ).GetField( "m_PreventFontCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );

		protected void OnValidate()
		{
			if( preventFontCallback != null && TryGetComponent( out InputField inputField ) )
			{
				preventFontCallback.SetValue( inputField, true );
				UnityEditor.EditorApplication.delayCall += () => preventFontCallback.SetValue( inputField, false );
			}
		}
#endif
	}
}