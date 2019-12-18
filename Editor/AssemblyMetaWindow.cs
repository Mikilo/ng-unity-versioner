using NGToolsEditor;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NGUnityVersioner
{
	public class AssemblyMetaWindow : EditorWindow
	{
		public const string	Title = "Assembly Inspector";

		public AssemblyMeta[]	meta;
		public int				selectedMeta;
		public bool				displayFriends;
		public bool				displayNamespaces;

		[NonSerialized]
		private string[]	metaLabel;
		[NonSerialized]
		private string[]	typesLabel;

		private Vector2	scrollPosition;

		protected virtual void	OnEnable()
		{
			this.wantsMouseMove = true;
		}

		protected virtual void	OnGUI()
		{
			EditorGUILayout.HelpBox("Meta assemblies are libraries (or DLL) converted, compacted & saved to be reused offline.\nAllowing to verify compatibility with a Unity version without installing it.", MessageType.Info);

			if (this.metaLabel == null || this.metaLabel.Length != this.meta.Length)
			{
				this.metaLabel = new string[this.meta.Length];

				for (int i = 0, max = this.metaLabel.Length; i < max; ++i)
					this.metaLabel[i] = (i + 1) + " - " + Path.GetFileNameWithoutExtension(this.meta[i].AssemblyPath);
			}

			using (LabelWidthRestorer.Get(100F))
			{
				EditorGUI.BeginChangeCheck();
				this.selectedMeta = EditorGUILayout.Popup("Meta Assembly", this.selectedMeta, this.metaLabel);
				if (EditorGUI.EndChangeCheck() == true)
				{
					this.typesLabel = null;
				}
			}

			if (this.selectedMeta >= 0 && this.selectedMeta < this.meta.Length)
			{
				using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
				{
					this.scrollPosition = scroll.scrollPosition;

					AssemblyMeta	assemblyMeta = this.meta[this.selectedMeta];

					using (LabelWidthRestorer.Get(60F))
					{
						EditorGUILayout.LabelField("Location", assemblyMeta.AssemblyPath);

						using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
						{
							this.displayFriends = EditorGUILayout.Foldout(this.displayFriends, "Friends (" + assemblyMeta.FriendAssemblies.Length + " assemblies)", true);
						}
					}

					if (this.displayFriends == true)
					{
						++EditorGUI.indentLevel;
						for (int i = 0, max = assemblyMeta.FriendAssemblies.Length; i < max; ++i)
							EditorGUILayout.LabelField(assemblyMeta.FriendAssemblies[i]);
						--EditorGUI.indentLevel;
					}

					GUILayout.Space(10F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						EditorGUILayout.LabelField(this.displayNamespaces == true ? "Namespaces" : "Types (" + assemblyMeta.Types.Length + ")");

						GUILayout.FlexibleSpace();

						if (GUILayout.Button(this.displayNamespaces == true ? "Display Type" : "Display Namespace", EditorStyles.toolbarButton) == true)
							this.displayNamespaces = !this.displayNamespaces;
					}

					if (this.displayNamespaces == true)
					{
						for (int i = 0, max = assemblyMeta.GlobalNamespace.Namespaces.Count; i < max; ++i)
							this.DrawNamespace(assemblyMeta.GlobalNamespace.Namespaces[i]);
					}
					else
					{
						if (this.typesLabel == null || this.typesLabel.Length != assemblyMeta.Types.Length)
						{
							this.typesLabel = new string[assemblyMeta.Types.Length];

							StringBuilder	buffer = Utility.GetBuffer();

							for (int i = 0, max = assemblyMeta.Types.Length; i < max; ++i)
							{
								TypeMeta	typeMeta = assemblyMeta.Types[i];

								buffer.Length = 0;

								buffer.Append(typeMeta.FullName);
								//buffer.Append(" (");

								if (typeMeta.Events.Length > 0)
								{
									buffer.Append(" - ");
									buffer.Append(typeMeta.Events.Length);
									buffer.Append(" events");
								}

								if (typeMeta.Fields.Length > 0)
								{
									buffer.Append(" - ");
									buffer.Append(typeMeta.Fields.Length);
									buffer.Append(" fields");
								}

								if (typeMeta.Properties.Length > 0)
								{
									buffer.Append(" - ");
									buffer.Append(typeMeta.Properties.Length);
									buffer.Append(" properties");
								}

								if (typeMeta.Methods.Length > 0)
								{
									buffer.Append(" - ");
									buffer.Append(typeMeta.Methods.Length);
									buffer.Append(" methods");
								}

								//buffer.Append(')');

								this.typesLabel[i] = buffer.ToString();
							}

							Utility.RestoreBuffer(buffer);
						}

						Event	eventCurrent = Event.current;

						if (eventCurrent.type == EventType.MouseMove)
							this.Repaint();

						for (int i = 0, max = assemblyMeta.Types.Length; i < max; ++i)
						{
							EditorGUILayout.LabelField(this.typesLabel[i]);

							Rect	r = GUILayoutUtility.GetLastRect();

							if (r.Contains(eventCurrent.mousePosition) == true)
							{
								r.xMin = r.xMax - 100F;

								if (GUI.Button(r, "Inspect") == true)
								{
									Utility.OpenWindow<TypeMetaWindow>(true, TypeMetaWindow.Title, true, null, w => w.meta = assemblyMeta.Types[i]);
									return;
								}
							}
						}
					}
				}
			}
		}

		private void	DrawNamespace(NamespaceMeta namespaceMeta)
		{
			EditorGUILayout.LabelField(namespaceMeta.Name + " (" + namespaceMeta.Types.Count + " Types)");

			++EditorGUI.indentLevel;
			for (int i = 0, max = namespaceMeta.Namespaces.Count; i < max; ++i)
			{
				this.DrawNamespace(namespaceMeta.Namespaces[i]);
			}
			--EditorGUI.indentLevel;
		}
	}
}