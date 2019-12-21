using NGToolsEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NGUnityVersioner
{
	public class NGUnityVersionerWindow : EditorWindow
	{
		public const string	Title = "NG Unity Versioner";
		public const float	ResetTargetAssembliesWidth = 100F;

		public static readonly CategoryTips[]	Tips = new CategoryTips[]
		{
			new CategoryTips
			{
				tips = new Tip[]
				{
					new Tip("Hold Ctrl when toggling any checkbox to only focus it.", () => XGUIHighlightManager.Highlight(NGUnityVersionerWindow.Title + ".checkbox")),
					new Tip("Use multithreading to speed up the execution.", () => XGUIHighlightManager.Highlight(NGUnityVersionerWindow.Title + ".useMultithreading")),
					new Tip("If you need a specific version, contact me at support@ngtools.tech.", () => ContactFormWizard.Open(ContactFormWizard.Subject.Contact, NGUnityVersionerWindow.Title, string.Empty))
				}
			}
		};

		public List<string>	targetAssemblies = new List<string>() { @"Library\ScriptAssemblies" };
		public List<string>	filterNamespaces = new List<string>();
		public List<string>	targetNamespaces = new List<string>() { "UnityEngine", "UnityEditor" };
		public string		metaVersionsPath = "Packages/com.mikilo.ng-unity-versioner/Versions";
		public List<string>	activeMetas = new List<string>();
		public List<string>	activeVersions = new List<string>();
		public bool			displayMetaVersions = true;
		public bool			displayUnityInstallPaths = true;
		public bool			useMultithreading;

		private ReorderableList	listAssemblies;
		private ReorderableList	listFilterNamespaces;
		private ReorderableList	listTargetNamespaces;
		private Vector2			scrollPosition;
		[NonSerialized]
		private string			detectedAssemblies;
		[NonSerialized]
		private string			detectedAssembliesTooltip;
		[NonSerialized]
		private string[]		assembliesMeta;
		[NonSerialized]
		private string[]		assembliesMetaAsLabel;

		private ErrorPopup	errorPopup = new ErrorPopup(NGUnityVersionerWindow.Title, "An error occurred, try to reopen " + NGUnityVersionerWindow.Title + ". If it persists contact the author.");

		[MenuItem("Window/" + NGUnityVersionerWindow.Title, priority = - 50)]
		public static void	Open()
		{
			Utility.OpenWindow<NGUnityVersionerWindow>(NGUnityVersionerWindow.Title);
		}

		protected virtual void	OnEnable()
		{
			Utility.LoadEditorPref(this, NGEditorPrefs.GetPerProjectPrefix());

			this.listAssemblies = new ReorderableList(this.targetAssemblies, typeof(string));
			this.listAssemblies.drawHeaderCallback = (r) =>
			{
				GUI.Label(r, "Target Assemblies (\".dll\" or folders containing \".dll\")");

				r.x += r.width - NGUnityVersionerWindow.ResetTargetAssembliesWidth;
				r.width = NGUnityVersionerWindow.ResetTargetAssembliesWidth;
				if (GUI.Button(r, "Reset") == true)
				{
					this.targetAssemblies.Clear();
					this.targetAssemblies.Add(@"Library\ScriptAssemblies");
				}
			};
			this.listAssemblies.drawElementCallback = (r, i, a, b) => { r.y += 2F; r.height = Constants.SingleLineHeight; this.targetAssemblies[i] = EditorGUI.TextField(r, this.targetAssemblies[i]); };
			this.listAssemblies.onAddCallback = (r) => this.targetAssemblies.Add(string.Empty);
			this.listAssemblies.onChangedCallback = (r) => this.detectedAssemblies = null;

			this.listFilterNamespaces = new ReorderableList(this.filterNamespaces, typeof(string));
			this.listFilterNamespaces.drawHeaderCallback = (r) => GUI.Label(r, "Filter Namespaces (Extract usages from namespaces beginning with)");
			this.listFilterNamespaces.drawElementCallback = (r, i, a, b) => { r.y += 1F; r.height = Constants.SingleLineHeight; this.filterNamespaces[i] = EditorGUI.TextField(r, this.filterNamespaces[i]); };
			this.listFilterNamespaces.onAddCallback = (r) => this.filterNamespaces.Add(string.Empty);

			this.listTargetNamespaces = new ReorderableList(this.targetNamespaces, typeof(string));
			this.listTargetNamespaces.drawHeaderCallback = (r) => GUI.Label(r, "Target Namespaces (beginning with)");
			this.listTargetNamespaces.drawElementCallback = (r, i, a, b) => { r.y += 1F; r.height = Constants.SingleLineHeight; this.targetNamespaces[i] = EditorGUI.TextField(r, this.targetNamespaces[i]); };
			this.listTargetNamespaces.onAddCallback = (r) => this.targetNamespaces.Add(string.Empty);

			this.wantsMouseMove = true;
		}

		protected virtual void	OnDisable()
		{
			Utility.SaveEditorPref(this, NGEditorPrefs.GetPerProjectPrefix());
		}

		protected virtual void	OnGUI()
		{
			if (Event.current.type == EventType.MouseMove)
				this.Repaint();

			this.errorPopup.OnGUILayout();

			try
			{
				using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
				{
					this.scrollPosition = scroll.scrollPosition;

					EditorGUI.BeginChangeCheck();
					this.listAssemblies.DoLayoutList();
					if (EditorGUI.EndChangeCheck() == true)
						this.detectedAssemblies = null;

					if (this.detectedAssemblies == null)
					{
						List<string>	assemblies = this.GetTargetAssemblies();

						if (assemblies.Count == 0)
						{
							this.detectedAssemblies = "No assembly detected.";
							this.detectedAssembliesTooltip = null;
						}
						else
						{
							if (assemblies.Count > 1)
								this.detectedAssemblies = assemblies.Count + " assemblies detected.";
							else
								this.detectedAssemblies = "1 assembly detected.";

							StringBuilder	buffer = Utility.GetBuffer();

							for (int i = 0, max = assemblies.Count; i < max; ++i)
								buffer.AppendLine(assemblies[i]);

							buffer.Length -= Environment.NewLine.Length;

							this.detectedAssembliesTooltip = Utility.ReturnBuffer(buffer);
						}
					}

					Utility.content.text = this.detectedAssemblies;
					Rect	r2 = GUILayoutUtility.GetLastRect();
					r2.xMin += 16F;
					r2.y -= 2F;
					r2.yMin = r2.yMax - 16F;
					GUI.Label(r2, Utility.content, EditorStyles.miniLabel);
					r2.xMin -= 12F;
					GUI.Label(r2, "↳");

					if (this.detectedAssembliesTooltip != null)
					{
						r2.width = GUI.skin.label.CalcSize(Utility.content).x;
						TooltipHelper.Label(r2, this.detectedAssembliesTooltip);
					}

					GUILayout.Space(3F);

					this.listFilterNamespaces.DoLayoutList();

					GUILayout.Space(3F);

					this.listTargetNamespaces.DoLayoutList();

					GUILayout.Space(2F);

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
					{
						this.displayMetaVersions = EditorGUILayout.Foldout(this.displayMetaVersions, "Assembly Meta Versions", true);

						if (GUILayout.Button("All", EditorStyles.toolbarButton) == true)
						{
							this.activeMetas.Clear();
							for (int i = 0, max = this.assembliesMeta.Length; i < max; ++i)
								this.activeMetas.Add(this.assembliesMeta[i]);
						}

						if (GUILayout.Button("None", EditorStyles.toolbarButton) == true)
							this.activeMetas.Clear();

						if (GUILayout.Button("", EditorStyles.toolbarDropDown, GUILayoutOptionPool.Width(30F)) == true)
						{
							GenericMenu menu = new GenericMenu();

							menu.AddItem(new GUIContent("Select highest of each version"), false, this.SelectHighestOfEachVersion);
							menu.AddItem(new GUIContent("Select lowest of each version"), false, this.SelectLowestOfEachVersion);
							menu.AddItem(new GUIContent("Select Unity >= 2020"), false, this.SelectVersionsSuperiorOrEqual, "2020");
							menu.AddItem(new GUIContent("Select Unity 2020"), false, this.SelectVersionsEqual, "2020");
							menu.AddItem(new GUIContent("Select Unity >= 2019"), false, this.SelectVersionsSuperiorOrEqual, "2019");
							menu.AddItem(new GUIContent("Select Unity 2019"), false, this.SelectVersionsEqual, "2019");
							menu.AddItem(new GUIContent("Select Unity >= 2018"), false, this.SelectVersionsSuperiorOrEqual, "2018");
							menu.AddItem(new GUIContent("Select Unity 2018"), false, this.SelectVersionsEqual, "2018");
							menu.AddItem(new GUIContent("Select Unity >= 2017"), false, this.SelectVersionsSuperiorOrEqual, "2017");
							menu.AddItem(new GUIContent("Select Unity 2017"), false, this.SelectVersionsEqual, "2017");
							menu.AddItem(new GUIContent("Select Unity >= 5"), false, this.SelectVersionsSuperiorOrEqual, "5");
							menu.AddItem(new GUIContent("Select Unity 5"), false, this.SelectVersionsEqual, "5");
							menu.AddItem(new GUIContent("Select Unity >= 4"), false, this.SelectVersionsSuperiorOrEqual, "4");
							menu.AddItem(new GUIContent("Select Unity 4"), false, this.SelectVersionsEqual, "4");

							menu.ShowAsContext();
						}
					}

					GUILayout.Space(3F);

					EditorGUI.BeginChangeCheck();
					this.metaVersionsPath = NGEditorGUILayout.OpenFolderField(null, this.metaVersionsPath);
					if (EditorGUI.EndChangeCheck() == true)
					{
						this.assembliesMeta = null;
						this.activeMetas.Clear();
					}
					TooltipHelper.HelpBox("Path to a folder containing \"." + AssemblyUsages.MetaExtension + "\"", MessageType.Info);

					if (this.assembliesMeta == null)
					{
						if (Directory.Exists(this.metaVersionsPath) == false)
							EditorGUILayout.HelpBox("Folder does not exist.", MessageType.Warning);
						else
						{
							List<string>	meta = new List<string>(Directory.GetFiles(this.metaVersionsPath, "*." + AssemblyUsages.MetaExtension));

							System.Comparison<string> aa = (a, b) =>
							{
								string[]	aParts = a.Substring(this.metaVersionsPath.Length + 1).Split('.');
								string[]	bParts = b.Substring(this.metaVersionsPath.Length + 1).Split('.');

								if (aParts.Length != bParts.Length)
									return bParts.Length - aParts.Length;

								try
								{
									for (int i = 0, max = aParts.Length; i < max; ++i)
									{
										if (i < 2)
										{
											int	aNum = int.Parse(aParts[i]);
											int	bNum = int.Parse(bParts[i]);

											if (aNum != bNum)
												return bNum - aNum;
										}
										else if (aParts[i] != bParts[i])
											return bParts[i].CompareTo(aParts[i]);
									}
								}
								catch (Exception)
								{
									return 0;
								}

								return b.CompareTo(a);
							};

							meta.Sort(aa);

							this.assembliesMeta = meta.ToArray();
							this.assembliesMetaAsLabel = new string[this.assembliesMeta.Length];

							for (int i = 0, max = this.assembliesMeta.Length; i < max; ++i)
								this.assembliesMetaAsLabel[i] = this.assembliesMeta[i].Substring(this.metaVersionsPath.Length + 1);

							this.Repaint();
						}
					}

					if (this.assembliesMeta != null && this.displayMetaVersions == true)
					{
						for (int i = 0, max = this.assembliesMetaAsLabel.Length; i < max; ++i)
						{
							string		metaFile = this.assembliesMeta[i];
							Rect		r = GUILayoutUtility.GetRect(0F, Constants.SingleLineHeight, EditorStyles.label);
							GUIContent	content = new GUIContent(Utility.GetUnityVersion(metaFile));
							float		versionWidth = GUI.skin.label.CalcSize(content).x;

							EditorGUI.BeginChangeCheck();
							bool	toggle = EditorGUI.ToggleLeft(r, GUIContent.none, this.activeMetas.Contains(metaFile));
							Rect	r3 = r;
							r3.width = 15F;
							XGUIHighlightManager.DrawHighlight(NGUnityVersionerWindow.Title + ".checkbox", this, r3, XGUIHighlights.Wave | XGUIHighlights.Glow);
							r.width -= versionWidth;
							r.xMin += 15F;
							EditorGUI.LabelField(r, this.assembliesMetaAsLabel[i]);
							r.xMin -= 15F;
							r.width += versionWidth;
							if (EditorGUI.EndChangeCheck() == true)
							{
								if (Event.current.control == true)
								{
									this.activeMetas.Clear();
									this.activeMetas.Add(metaFile);
								}
								else
								{
									if (toggle == true)
										this.activeMetas.Add(metaFile);
									else
										this.activeMetas.Remove(metaFile);
								}
							}
							r.x += r.width - versionWidth;

							using (new EditorGUI.DisabledScope(true))
							{
								r.width = versionWidth;
								GUI.Label(r, content);
							}
						}
					}

					using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbarButton))
					{
						this.displayUnityInstallPaths = EditorGUILayout.Foldout(this.displayUnityInstallPaths, "Unity Install Paths", true);

						if (GUILayout.Button("All", EditorStyles.toolbarButton) == true)
						{
							this.activeVersions.Clear();
							foreach (var item in UnityInstalls.EachInstall)
								this.activeVersions.Add(item.Value);
						}

						if (GUILayout.Button("None", EditorStyles.toolbarButton) == true)
							this.activeVersions.Clear();

						Utility.content.text = "Installs";
						Utility.content.image = UtilityResources.FolderIcon;
						if (GUILayout.Button(Utility.content, EditorStyles.toolbarButton, GUILayoutOptionPool.MaxWidth(100F)) == true)
							UnityInstallsWindow.Open();
						Utility.content.image = null;
					}

					GUILayout.Space(3F);

					if (this.displayUnityInstallPaths == true)
					{
						if (UnityInstalls.UnityInstallsCount == 0)
							GUILayout.Button("No Unity install detected.", EditorStyles.largeLabel);
						else
						{
							foreach (var item in UnityInstalls.EachInstall)
							{
								Rect		r = GUILayoutUtility.GetRect(0F, Constants.SingleLineHeight, EditorStyles.label);
								GUIContent	content = new GUIContent(Utility.GetUnityVersion(item.Value));
								float		versionWidth = GUI.skin.label.CalcSize(content).x;

								EditorGUI.BeginChangeCheck();
								bool	toggle = EditorGUI.ToggleLeft(r, GUIContent.none, this.activeVersions.Contains(item.Value)); Rect r3 = r;
								r3.width = 15F;
								XGUIHighlightManager.DrawHighlight(NGUnityVersionerWindow.Title + ".checkbox", this, r3, XGUIHighlights.Wave | XGUIHighlights.Glow);
								r.width -= versionWidth;
								r.xMin += 15F;
								EditorGUI.LabelField(r, item.Value);
								r.xMin -= 15F;
								r.width += versionWidth;
								if (EditorGUI.EndChangeCheck() == true)
								{
									if (Event.current.control == true)
									{
										this.activeVersions.Clear();
										this.activeVersions.Add(item.Value);
									}
									else
									{
										if (toggle == true)
											this.activeVersions.Add(item.Value);
										else
											this.activeVersions.Remove(item.Value);
									}
								}
								r.x += r.width - versionWidth;

								using (new EditorGUI.DisabledScope(true))
								{
									r.width = versionWidth;
									GUI.Label(r, content);
								}

								GUILayout.Space(2F);
							}
						}
					}
				}

				EditorGUILayout.HelpBox("BEWARE! Compatibility check does not handle Unity API moved to Package Manager.", MessageType.Warning);
				EditorGUILayout.HelpBox("BEWARE! API references between pre-processing directives can not be handled.", MessageType.Warning);

				using (new EditorGUILayout.HorizontalScope())
				{
					using (BgColorContentRestorer.Get(GeneralStyles.HighlightActionButton))
					{
						if (GUILayout.Button("Check Compatibilities") == true)
						{
							List<string>	targetAssemblies = new List<string>();

							for (int i = 0, max = this.targetAssemblies.Count; i < max; ++i)
							{
								var element = this.targetAssemblies[i];

								if (Directory.Exists(this.targetAssemblies[i]) == true)
								{
									string[]	subAssemblies = Directory.GetFiles(this.targetAssemblies[i], "*.dll");

									targetAssemblies.AddRange(subAssemblies);
								}
								else if (File.Exists(this.targetAssemblies[i]) == true)
									targetAssemblies.Add(this.targetAssemblies[i]);
								else
									Debug.LogWarning("Assembly at \"" + this.targetAssemblies[i] + "\" does not exist.");
							}

							if (targetAssemblies.Count == 0)
							{
								this.ShowNotification(new GUIContent("No target assembly available."));
								Debug.LogWarning("No target assembly available.");
								return;
							}

							if (this.activeMetas.Count == 0 && this.activeVersions.Count == 0)
							{
								this.ShowNotification(new GUIContent("No meta or version selected."));
								Debug.LogWarning("No meta or version selected.");
								return;
							}

							targetAssemblies.Distinct();

							using (WatchTime.Get("Check Compatibilities"))
							{
								AssemblyUsagesResult[]	results = AssemblyUsages.CheckCompatibilities(targetAssemblies, this.filterNamespaces.ToArray(), this.targetNamespaces.ToArray(), this.activeMetas, this.activeVersions, this.metaVersionsPath, this.useMultithreading);

								if (results != null)
									Utility.OpenWindow<AssemblyUsagesResultWindow>(AssemblyUsagesResultWindow.Title, true, w => w.SetResults(results));
							}
						}
					}

					this.useMultithreading = EditorGUILayout.ToggleLeft("Use Multithreading", this.useMultithreading, GUILayoutOptionPool.Width(130F));
					XGUIHighlightManager.DrawHighlightLayout(NGUnityVersionerWindow.Title + ".useMultithreading", this, XGUIHighlights.Wave | XGUIHighlights.Glow);
				}
			}
			catch (Exception ex)
			{
				this.errorPopup.error = ex;
			}

			TooltipHelper.PostOnGUI();
		}

		private List<string>	GetTargetAssemblies()
		{
			List<string>	targetAssemblies = new List<string>();

			for (int i = 0, max = this.targetAssemblies.Count; i < max; ++i)
			{
				string	path = this.targetAssemblies[i];

				if (Directory.Exists(path) == true)
				{
					string[]	subAssemblies = Directory.GetFiles(path, "*.dll");

					for (int j = 0, max2 = subAssemblies.Length; j < max2; ++j)
					{
						if (targetAssemblies.Contains(subAssemblies[j]) == false)
							targetAssemblies.Add(subAssemblies[j]);
					}
				}
				else if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) == true && File.Exists(path) == true)
					targetAssemblies.Add(path);
			}

			targetAssemblies.Distinct();

			return targetAssemblies;
		}

		private void	SelectVersionsSuperiorOrEqual(object raw)
		{
			string	majorVersion = (string)raw;
			int		majorVersionInt = int.Parse(majorVersion);

			this.activeMetas.Clear();

			for (int i = 0, max = this.assembliesMetaAsLabel.Length; i < max; ++i)
			{
				string	metaFile = this.assembliesMetaAsLabel[i];
				int		dot = metaFile.IndexOf('.');

				if (dot == -1)
					continue;

				string	metaMajorVersion = metaFile.Substring(0, dot);
				int		metaMajorVersionInt;

				if (int.TryParse(metaMajorVersion, out metaMajorVersionInt) == true)
				{
					if (metaMajorVersionInt >= majorVersionInt)
						this.activeMetas.Add(this.assembliesMeta[i]);
				}

				//if (majorVersion[0] != '2')
				//{
				//	if (metaFile.StartsWith(majorVersion) == true)
				//		this.activeMetas.Add(metaFile);
				//}
				//else
				//{
				//	if (metaFile.CompareTo(majorVersion) >= 0)
				//	{
				//		Debug.Log(metaFile);
				//		this.activeMetas.Add(metaFile);
				//	}
				//}
			}
		}

		private void	SelectVersionsEqual(object raw)
		{
			string	version = (string)raw;

			this.activeMetas.Clear();

			for (int i = 0, max = this.assembliesMetaAsLabel.Length; i < max; ++i)
			{
				string	metaFile = this.assembliesMetaAsLabel[i];

				if (metaFile.StartsWith(version) == true)
					this.activeMetas.Add(this.assembliesMeta[i]);
			}
		}

		private void	SelectLowestOfEachVersion()
		{
			Dictionary<string, string>	lowestVersions = new Dictionary<string, string>();

			for (int i = 0, max = this.assembliesMetaAsLabel.Length; i < max; ++i)
			{
				string	metaFile = this.assembliesMetaAsLabel[i];
				int		dot = metaFile.IndexOf('.');

				if (dot == -1)
					continue;

				dot = metaFile.IndexOf('.', dot + 1);
				if (dot == -1)
					continue;

				string	metaMajorVersion = metaFile.Substring(0, dot);

				if (lowestVersions.ContainsKey(metaMajorVersion) == false)
					lowestVersions.Add(metaMajorVersion, this.assembliesMeta[i]);
				else
				{
					if (lowestVersions[metaMajorVersion].CompareTo(this.assembliesMeta[i]) > 0)
						lowestVersions[metaMajorVersion] = this.assembliesMeta[i];
				}
			}

			this.activeMetas.Clear();
			foreach (string version in lowestVersions.Values)
				this.activeMetas.Add(version);
		}

		private void	SelectHighestOfEachVersion()
		{
			Dictionary<string, string>	highestVersions = new Dictionary<string, string>();

			for (int i = 0, max = this.assembliesMetaAsLabel.Length; i < max; ++i)
			{
				string	metaFile = this.assembliesMetaAsLabel[i];
				int		dot = metaFile.IndexOf('.');

				if (dot == -1)
					continue;

				dot = metaFile.IndexOf('.', dot + 1);
				if (dot == -1)
					continue;

				string	metaMajorVersion = metaFile.Substring(0, dot);

				if (highestVersions.ContainsKey(metaMajorVersion) == false)
				{
					highestVersions.Add(metaMajorVersion, this.assembliesMeta[i]);
				}
				else
				{
					if (highestVersions[metaMajorVersion].CompareTo(this.assembliesMeta[i]) < 0)
						highestVersions[metaMajorVersion] = this.assembliesMeta[i];
				}
			}

			this.activeMetas.Clear();
			foreach (string version in highestVersions.Values)
				this.activeMetas.Add(version);
		}

		protected virtual void	ShowButton(Rect r)
		{
			Event	currentEvent = Event.current;

			if (currentEvent.type == EventType.MouseDown && r.Contains(currentEvent.mousePosition) == true)
			{
				WindowTipsWindow.Open(NGUnityVersionerWindow.Title,
									  new Vector2(this.position.xMax - WindowTipsWindow.PopupWidth, this.position.y),
									  NGUnityVersionerWindow.Tips);
			}

			if (r.Contains(currentEvent.mousePosition) == true && Resources.FindObjectsOfTypeAll<WindowTipsWindow>().Length == 0)
				TooltipHelper.Custom(r, this.OnGUITips, WindowTipsWindow.PopupWidth, WindowTipsWindow.HeaderHeight + WindowTipsWindow.GetHeight(NGUnityVersionerWindow.Tips));
			XGUIHighlightManager.DrawHighlight(NGUnityVersionerWindow.Title + ".Tips", this, r, XGUIHighlights.Glow | XGUIHighlights.Wave);

			r.width -= 3F;
			r.height -= 3F;

			GUI.DrawTexture(r, UtilityResources.Book);
		}

		private void	OnGUITips(Rect r, object context)
		{
			WindowTipsWindow.OnGUITooltip(r, NGUnityVersionerWindow.Tips);
		}
	}
}