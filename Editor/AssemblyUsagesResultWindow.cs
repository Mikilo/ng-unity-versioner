using NGToolsEditor;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NGUnityVersioner
{
	public class AssemblyUsagesResultWindow : EditorWindow, IHasCustomMenu
	{
		public const string	Title = "Assembly Usages Result";
		public const float	ExportButtonWidth = 100F;
		public const float	InspectMetaButtonWidth = 100F;

		[NonSerialized]
		private AssemblyUsagesResult[]	results;
		[NonSerialized]
		private bool[]					resultsIsOpen;
		[NonSerialized]
		private string[]				resultsAsLabel;
		[NonSerialized]
		private string[]				resultsAnomalyAsLabel;
		[NonSerialized]
		private Vector2					scrollPosition;
		[NonSerialized]
		private GUIStyle				richFoldout;

		protected virtual void	OnGUI()
		{
			if (this.results == null)
				return;

			if (this.richFoldout == null)
			{
				this.richFoldout = new GUIStyle(EditorStyles.foldout);
				this.richFoldout.richText = true;
			}

			if (this.resultsAsLabel == null)
			{
				this.resultsAsLabel = new string[this.results.Length];
				this.resultsAnomalyAsLabel = new string[this.results.Length];

				for (int i = 0; i < this.results.Length; i++)
				{
					AssemblyUsagesResult	result = this.results[i];
					int						missingRefsCount = result.missingTypes.Count + result.missingFields.Count + result.missingMethods.Count;
					int						warningsCount = result.foundTypes.Count + result.foundFields.Count + result.foundMethods.Count;

					if (missingRefsCount + warningsCount == 0)
						this.resultsAsLabel[i] = result.unityMeta.Version + " <color=green>(Fully compatible)</color>";
					else if (missingRefsCount + warningsCount == 1)
					{
						this.resultsAnomalyAsLabel[i] = (missingRefsCount + warningsCount) + " anomaly detected";

						if (missingRefsCount > 0)
							this.resultsAsLabel[i] = result.unityMeta.Version + " <color=red>(" + (missingRefsCount + warningsCount) + " anomaly)</color>";
						else
							this.resultsAsLabel[i] = result.unityMeta.Version + " (" + (missingRefsCount + warningsCount) + " anomaly)";
					}
					else
					{
						this.resultsAnomalyAsLabel[i] = (missingRefsCount + warningsCount) + " anomalies detected";

						if (missingRefsCount > 0)
							this.resultsAsLabel[i] = result.unityMeta.Version + " <color=red>(" + (missingRefsCount + warningsCount) + " anomalies)</color>";
						else
							this.resultsAsLabel[i] = result.unityMeta.Version + " (" + (missingRefsCount + warningsCount) + " anomalies)";
					}
				}
			}

			using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
			{
				this.scrollPosition = scroll.scrollPosition;

				for (int i = 0; i < this.results.Length; i++)
				{
					AssemblyUsagesResult	result = this.results[i];
					int						missingRefsCount = result.missingTypes.Count + result.missingFields.Count + result.missingMethods.Count;
					int						warningsCount = result.foundTypes.Count + result.foundFields.Count + result.foundMethods.Count;

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						Rect		r = GUILayoutUtility.GetRect(0F, Constants.SingleLineHeight, EditorStyles.label);

						r.width -= AssemblyUsagesResultWindow.ExportButtonWidth + AssemblyUsagesResultWindow.InspectMetaButtonWidth;
						this.resultsIsOpen[i] = EditorGUI.Foldout(r, this.resultsIsOpen[i], this.resultsAsLabel[i], true, this.richFoldout);
						r.x += r.width;

						r.y -= 2F;
						r.width = AssemblyUsagesResultWindow.ExportButtonWidth;
						if (GUI.Button(r, "Export", EditorStyles.toolbarButton) == true)
						{
							StringBuilder	buffer = new StringBuilder(1024);

							result.Export(buffer);

							EditorGUIUtility.systemCopyBuffer = buffer.ToString();

							this.ShowNotification(new GUIContent("Result exported to clipboard."));
						}
						r.x += r.width;

						r.width = AssemblyUsagesResultWindow.InspectMetaButtonWidth;
						if (GUI.Button(r, "Inspect Meta", EditorStyles.toolbarButton) == true)
						{
							Utility.OpenWindow<AssemblyMetaWindow>(AssemblyMetaWindow.Title, true, w => w.meta = result.unityMeta.AssembliesMeta);
						}
					}

					if (this.resultsIsOpen[i] == true)
					{
						StringBuilder	buffer = new StringBuilder();

						for (int j = 0, max2 = result.assemblyUsages.Assemblies.Length; j < max2; ++j)
						{
							if (j > 0)
								buffer.Append(", ");
							buffer.Append(Path.GetFileNameWithoutExtension(result.assemblyUsages.Assemblies[j]));
						}

						EditorGUILayout.TextField("Inspected", buffer.ToString());

						buffer.Length = 0;

						for (int j = 0, max2 = result.assemblyUsages.FilterNamespaces.Length; j < max2; ++j)
						{
							if (j > 0)
								buffer.Append(", ");
							buffer.Append(Path.GetFileNameWithoutExtension(result.assemblyUsages.FilterNamespaces[j]));
						}

						EditorGUILayout.TextField("Filtered in namespaces", buffer.ToString());

						buffer.Length = 0;

						for (int j = 0, max2 = result.assemblyUsages.TargetNamespaces.Length; j < max2; ++j)
						{
							if (j > 0)
								buffer.Append(", ");
							buffer.Append(Path.GetFileNameWithoutExtension(result.assemblyUsages.TargetNamespaces[j]));
						}

						EditorGUILayout.TextField("Targeted namespaces", buffer.ToString());

						if (missingRefsCount + warningsCount > 0)
						{
							GUILayout.Space(5F);

							using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
							{
								GUILayout.Label(this.resultsAnomalyAsLabel[i]);
							}

							++EditorGUI.indentLevel;

							if (result.missingTypes.Count > 0)
							{
								GUILayout.Space(5F);

								using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
								{
									EditorGUILayout.LabelField($"Missing Types ({result.missingTypes.Count})");
								}

								++EditorGUI.indentLevel;
								for (int j = 0, max2 = result.missingTypes.Count; j < max2; ++j)
								{
									using (new EditorGUILayout.HorizontalScope())
									{
										EditorGUILayout.TextField(result.missingTypes[j].ToString());

										if (GUILayout.Button("See API Range", EditorStyles.miniButton, GUILayoutOptionPool.Width(100F)) == true)
										{
											Application.OpenURL("https://sabresaurus.com/unity-api-versioner/?api=" + result.missingTypes[j].ToString());
											return;
										}
									}

									if (result.missingTypes[j].ErrorMessage != null)
									{
										++EditorGUI.indentLevel;
										EditorGUILayout.TextField(result.missingTypes[j].ErrorMessage, EditorStyles.miniLabel);
										--EditorGUI.indentLevel;
									}
								}
								--EditorGUI.indentLevel;
							}

							if (result.missingFields.Count > 0)
							{
								GUILayout.Space(5F);

								using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
								{
									EditorGUILayout.LabelField($"Missing Fields ({result.missingFields.Count})");
								}

								++EditorGUI.indentLevel;
								for (int j = 0, max2 = result.missingFields.Count; j < max2; ++j)
								{
									EditorGUILayout.TextField(result.missingFields[j].ToString());

									if (result.missingFields[j].ErrorMessage != null)
									{
										++EditorGUI.indentLevel;
										EditorGUILayout.TextField(result.foundFields[j].ErrorMessage, EditorStyles.miniLabel);
										--EditorGUI.indentLevel;
									}
								}
								--EditorGUI.indentLevel;
							}

							if (result.missingMethods.Count > 0)
							{
								GUILayout.Space(5F);

								using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
								{
									EditorGUILayout.LabelField($"Missing Methods ({result.missingMethods.Count})");
								}

								++EditorGUI.indentLevel;
								for (int j = 0, max2 = result.missingMethods.Count; j < max2; ++j)
								{
									EditorGUILayout.TextField(result.missingMethods[j].ToString());

									if (result.missingMethods[j].ErrorMessage != null)
									{
										++EditorGUI.indentLevel;
										EditorGUILayout.TextField(result.missingMethods[j].ErrorMessage, EditorStyles.miniLabel);
										--EditorGUI.indentLevel;
									}
								}
								--EditorGUI.indentLevel;
							}

							if (result.foundTypes.Count > 0)
							{
								GUILayout.Space(5F);

								using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
								{
									EditorGUILayout.LabelField("Found Types (with error)");
								}

								for (int j = 0, max2 = result.foundTypes.Count; j < max2; ++j)
								{
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundTypes[j].ToString());
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundTypes[j].ErrorMessage, EditorStyles.miniLabel);
									--EditorGUI.indentLevel;
									--EditorGUI.indentLevel;
								}
							}

							if (result.foundFields.Count > 0)
							{
								GUILayout.Space(5F);

								using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
								{
									EditorGUILayout.LabelField("Found Fields (with error)");
								}

								for (int j = 0, max2 = result.foundFields.Count; j < max2; ++j)
								{
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundFields[j].ToString());
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundFields[j].ErrorMessage, EditorStyles.miniLabel);
									--EditorGUI.indentLevel;
									--EditorGUI.indentLevel;
								}
							}

							if (result.foundMethods.Count > 0)
							{
								GUILayout.Space(5F);

								using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
								{
									EditorGUILayout.LabelField("Found Methods (with error)");
								}

								for (int j = 0, max2 = result.foundMethods.Count; j < max2; ++j)
								{
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundMethods[j].ToString());
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundMethods[j].ErrorMessage, EditorStyles.miniLabel);
									--EditorGUI.indentLevel;
									--EditorGUI.indentLevel;
								}
							}

							--EditorGUI.indentLevel;
						}
					}
				}
			}
		}

		/// <summary>Must auto close. Because serializing an array of AssemblyUsagesResult greatly increases init duration.</summary>
		protected virtual void	Update()
		{
			if (EditorApplication.isCompiling == true || this.results == null)
				this.Close();
		}

		public void	SetResults(AssemblyUsagesResult[] results)
		{
			this.results = results;
			this.resultsAsLabel = null;

			if (this.resultsIsOpen != null)
				Array.Resize<bool>(ref this.resultsIsOpen, this.results.Length);
			else
				this.resultsIsOpen = new bool[this.results.Length];
		}

		void	IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Export All Results"), false, this.ExportAll);
		}

		private void	ExportAll()
		{
			StringBuilder	buffer = new StringBuilder(1 << 19); // 512kB

			for (int i = 0; i < this.results.Length; i++)
			{
				if (i > 0)
				{
					buffer.AppendLine();
					buffer.AppendLine();
				}

				this.results[i].Export(buffer);
			}

			EditorGUIUtility.systemCopyBuffer = buffer.ToString();
			this.ShowNotification(new GUIContent("All results exported to clipboard."));
		}
	}
}