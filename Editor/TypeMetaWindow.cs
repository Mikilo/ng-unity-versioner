using UnityEditor;
using UnityEngine;

namespace NGUnityVersioner
{
	public class TypeMetaWindow : EditorWindow
	{
		public const string	Title = "Type Inspector";

		public TypeMeta	typeMeta;

		private Vector2	scrollPosition;

		protected virtual void	OnGUI()
		{
			using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
			{
				this.scrollPosition = scroll.scrollPosition;

				EditorGUILayout.TextField("Namespace", this.typeMeta.Namespace);
				EditorGUILayout.TextField("Name", this.typeMeta.Name);
				if (this.typeMeta.ErrorMessage != null)
					EditorGUILayout.TextField("Error", this.typeMeta.ErrorMessage);
				EditorGUILayout.TextField("Is Public", this.typeMeta.IsPublic ? "True" : "False");

				if (this.typeMeta.Events.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Events (" + this.typeMeta.Events.Length + ")");
					}

					for (int i = 0, max = this.typeMeta.Events.Length; i < max; ++i)
						GUILayout.Label(this.typeMeta.Events[i].ToString());
				}

				if (this.typeMeta.Fields.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Fields (" + this.typeMeta.Fields.Length + ")");
					}

					for (int i = 0, max = this.typeMeta.Fields.Length; i < max; ++i)
						GUILayout.Label(this.typeMeta.Fields[i].ToString());
				}

				if (this.typeMeta.Properties.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Properties (" + this.typeMeta.Properties.Length + ")");
					}

					for (int i = 0, max = this.typeMeta.Properties.Length; i < max; ++i)
						GUILayout.Label(this.typeMeta.Properties[i].ToString());
				}

				if (this.typeMeta.Methods.Length > 0)
				{
					GUILayout.Space(5F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						GUILayout.Label("Methods (" + this.typeMeta.Methods.Length + ")");
					}

					for (int i = 0, max = this.typeMeta.Methods.Length; i < max; ++i)
						GUILayout.Label(this.typeMeta.Methods[i].ToString());
				}
			}
		}
	}
}