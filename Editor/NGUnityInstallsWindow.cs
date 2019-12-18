using NGToolsEditor;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NGUnityVersioner
{
	public class UnityInstallsWindow : EditorWindow
	{
		public const string	Title = "NG Unity Installations";

		private Vector2	scrollPosition;

		[MenuItem(Constants.MenuItemPath + UnityInstallsWindow.Title, priority = -50)]
		private static void	Open()
		{
			EditorWindow.GetWindow<UnityInstallsWindow>(true, UnityInstallsWindow.Title, true);
		}

		protected virtual void	OnGUI()
		{
			EditorGUILayout.LabelField("Unity Installations Paths");

			if (UnityInstalls.InstallPathsCount == 0 || UnityInstalls.UnityInstallsCount == 0)
				EditorGUILayout.HelpBox("Your Unity folders must end by their version. (e.g. \"Unity 4.2.1f3\", \"Unity5.1.3p4\")", MessageType.Info);

			foreach (var pair in UnityInstalls.EachPath)
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUI.BeginChangeCheck();
					string	path = EditorGUILayout.TextField(pair.Value);
					if (EditorGUI.EndChangeCheck() == true)
						UnityInstalls.SetPath(pair.Key, path);

					if (GUILayout.Button("Browse", GUILayout.Width(60F)) == true)
					{
						path = pair.Value;
						if (string.IsNullOrEmpty(path) == false)
							path = Path.GetDirectoryName(path);

						string	projectPath = EditorUtility.OpenFolderPanel("Folder with Unity installations ending by A.B.C[abfpx]NN", path, string.Empty);

						if (string.IsNullOrEmpty(projectPath) == false)
						{
							UnityInstalls.SetPath(pair.Key, projectPath);
							GUI.FocusControl(null);
						}
					}

					if (GUILayout.Button("X", GUILayout.Width(16F)) == true)
					{
						UnityInstalls.RemovePath(pair.Key);
						return;
					}
				}
			}

			if (GUILayout.Button("Add installation path") == true)
				UnityInstalls.AddPath();

			using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
			{
				this.scrollPosition = scroll.scrollPosition;

				foreach (var pair in UnityInstalls.EachInstall)
					EditorGUILayout.LabelField(pair.Value + " [" + pair.Key + "]");
			}
		}
	}
}