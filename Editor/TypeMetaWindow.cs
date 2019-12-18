using UnityEditor;
using UnityEngine;

namespace NGUnityVersioner
{
	public class TypeMetaWindow : EditorWindow
	{
		public const string	Title = "Type Inspector";

		public TypeMeta	meta;

		private Vector2	scrollPosition;

		protected virtual void	OnGUI()
		{
			using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
			{
				this.scrollPosition = scroll.scrollPosition;

				EditorGUILayout.TextField("Namespace", this.meta.Namespace);
				EditorGUILayout.TextField("Name", this.meta.Name);
				if (this.meta.ErrorMessage != null)
					EditorGUILayout.TextField("Error", this.meta.ErrorMessage);
				EditorGUILayout.TextField("Is Public", this.meta.IsPublic ? "True" : "False");

				if (this.meta.Events.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Events (" + this.meta.Events.Length + ")");
					}

					for (int i = 0, max = this.meta.Events.Length; i < max; ++i)
						GUILayout.Label(this.meta.Events[i].ToString());
				}

				if (this.meta.Fields.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Fields (" + this.meta.Fields.Length + ")");
					}

					for (int i = 0, max = this.meta.Fields.Length; i < max; ++i)
						GUILayout.Label(this.meta.Fields[i].ToString());
				}

				if (this.meta.Properties.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Properties (" + this.meta.Properties.Length + ")");
					}

					for (int i = 0, max = this.meta.Properties.Length; i < max; ++i)
						GUILayout.Label(this.meta.Properties[i].ToString());
				}

				if (this.meta.Methods.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Methods (" + this.meta.Methods.Length + ")");
					}

					for (int i = 0, max = this.meta.Methods.Length; i < max; ++i)
						GUILayout.Label(this.meta.Methods[i].ToString());
				}
			}
		}
	}
}