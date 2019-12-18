using NGToolsEditor;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NGUnityVersioner
{
	public class AssemblyUsagesResultWindow : EditorWindow
	{
		public const string	Title = "Assembly Usages Result";
		public const float	ExportButtonWidth = 100F;
		public const float	InspectMetaButtonWidth = 100F;

		public AssemblyUsagesResult[]	results;
		public bool[]					resultsIsOpen;

		[NonSerialized]
		private string[]	resultsAsLabel;
		private Vector2		scrollPosition;

		protected virtual void	OnGUI()
		{
			if (this.resultsAsLabel == null)
			{
				this.resultsAsLabel = new string[this.results.Length];

				for (int i = 0; i < this.results.Length; i++)
				{
					AssemblyUsagesResult	result = this.results[i];
					int						missingRefsCount = result.missingTypes.Count + result.missingFields.Count + result.missingMethods.Count;
					int						warningsCount = 0;

					for (int j = 0, max2 = result.foundTypes.Count; j < max2; ++j)
					{
						if (result.foundTypes[j].ErrorMessage != null)
							++warningsCount;
					}

					for (int j = 0, max2 = result.foundFields.Count; j < max2; ++j)
					{
						if (result.foundFields[j].ErrorMessage != null)
							++warningsCount;
					}

					for (int j = 0, max2 = result.foundMethods.Count; j < max2; ++j)
					{
						if (result.foundMethods[j].ErrorMessage != null)
							++warningsCount;
					}

					this.resultsAsLabel[i] = Utility.GetUnityVersion(result.unityPath) + " (" + (missingRefsCount + warningsCount) + " anomalies)";
				}
			}

			if (this.resultsIsOpen == null)
				this.resultsIsOpen = new bool[this.results.Length];

			using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
			{
				this.scrollPosition = scroll.scrollPosition;

				for (int i = 0; i < this.results.Length; i++)
				{
					AssemblyUsagesResult	result = this.results[i];
					int						missingRefsCount = result.missingTypes.Count + result.missingFields.Count + result.missingMethods.Count;

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						Rect		r = GUILayoutUtility.GetRect(0F, Constants.SingleLineHeight, EditorStyles.label);
						GUIContent	content = new GUIContent(this.resultsAsLabel[i]);
						float		versionWidth = EditorStyles.miniLabel.CalcSize(content).x;

						r.width -= AssemblyUsagesResultWindow.ExportButtonWidth + AssemblyUsagesResultWindow.InspectMetaButtonWidth;
						this.resultsIsOpen[i] = EditorGUI.Foldout(r, this.resultsIsOpen[i], GUIContent.none, true);
						r.width -= versionWidth;
						r.xMin += 15F;
						EditorGUI.LabelField(r, result.unityPath);
						r.xMin -= 15F;
						r.width += versionWidth;
						r.x += r.width - versionWidth;

						using (new EditorGUI.DisabledScope(true))
						{
							r.width = versionWidth;
							GUI.Label(r, content, EditorStyles.miniLabel);
							r.x += r.width;
						}

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
							Utility.OpenWindow<AssemblyMetaWindow>(AssemblyMetaWindow.Title, true, w => w.meta = result.assembliesMeta);
						}
					}

					if (this.resultsIsOpen[i] == true)
					{
						StringBuilder	buffer = new StringBuilder();

						for (int j = 0, max2 = result.assemblyUsages.assemblies.Count; j < max2; ++j)
						{
							if (j > 0)
								buffer.Append(", ");
							buffer.Append(Path.GetFileNameWithoutExtension(result.assemblyUsages.assemblies[j]));
						}

						EditorGUILayout.TextField("Inspected", buffer.ToString());

						buffer.Length = 0;

						for (int j = 0, max2 = result.assemblyUsages.filterNamespaces.Length; j < max2; ++j)
						{
							if (j > 0)
								buffer.Append(", ");
							buffer.Append(Path.GetFileNameWithoutExtension(result.assemblyUsages.filterNamespaces[j]));
						}

						EditorGUILayout.TextField("Filtered in namespaces", buffer.ToString());

						buffer.Length = 0;

						for (int j = 0, max2 = result.assemblyUsages.targetNamespaces.Length; j < max2; ++j)
						{
							if (j > 0)
								buffer.Append(", ");
							buffer.Append(Path.GetFileNameWithoutExtension(result.assemblyUsages.targetNamespaces[j]));
						}

						EditorGUILayout.TextField("Targeted namespaces", buffer.ToString());

						if (missingRefsCount != 0)
						{
							using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
							{
								GUILayout.Label($"Missing References ({missingRefsCount})");
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

										if (GUILayout.Button("Check Versions", EditorStyles.miniButton, GUILayoutOptionPool.Width(100F)) == true)
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
							--EditorGUI.indentLevel;
						}

						if (result.foundTypes.Count > 0)
						{
							for (int j = 0, first = 0, max2 = result.foundTypes.Count; j < max2; ++j)
							{
								if (result.foundTypes[j].ErrorMessage != null)
								{
									if (first == 0)
									{
										first = 1;

										GUILayout.Space(5F);

										using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
										{
											EditorGUILayout.LabelField("Found Types (with error)");
										}
									}

									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundTypes[j].ToString());
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundTypes[j].ErrorMessage, EditorStyles.miniLabel);
									--EditorGUI.indentLevel;
									--EditorGUI.indentLevel;
								}
							}
						}

						if (result.foundFields.Count > 0)
						{
							for (int j = 0, first = 0, max2 = result.foundFields.Count; j < max2; ++j)
							{
								if (result.foundFields[j].ErrorMessage != null)
								{
									if (first == 0)
									{
										first = 1;

										GUILayout.Space(5F);

										using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
										{
											EditorGUILayout.LabelField("Found Fields (with error)");
										}
									}

									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundFields[j].ToString());
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundFields[j].ErrorMessage, EditorStyles.miniLabel);
									--EditorGUI.indentLevel;
									--EditorGUI.indentLevel;
								}
							}
						}

						if (result.foundMethods.Count > 0)
						{
							for (int j = 0, first = 0, max2 = result.foundMethods.Count; j < max2; ++j)
							{
								if (result.foundMethods[j].ErrorMessage != null)
								{
									if (first == 0)
									{
										first = 1;

										GUILayout.Space(5F);

										using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
										{
											EditorGUILayout.LabelField("Found Methods (with error)");
										}
									}

									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundMethods[j].ToString());
									++EditorGUI.indentLevel;
									EditorGUILayout.TextField(result.foundMethods[j].ErrorMessage, EditorStyles.miniLabel);
									--EditorGUI.indentLevel;
									--EditorGUI.indentLevel;
								}
							}
						}
					}
				}
			}
		}

		protected virtual void	Update()
		{
			if (EditorApplication.isCompiling == true)
				this.Close();
		}

		public void	SetResults(AssemblyUsagesResult[] results)
		{
			this.results = results;
			this.resultsAsLabel = null;
			this.resultsIsOpen = null;
		}
	}
}