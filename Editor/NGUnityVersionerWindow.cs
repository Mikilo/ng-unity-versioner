using NGToolsStandalone_For_NGUnityVersioner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NGUnityVersioner
{
	using NGToolsStandalone_For_NGUnityVersionerEditor;

	public class NGUnityVersionerWindow : EditorWindow, IHasCustomMenu
	{
		public enum Tab
		{
			NamespaceSearch,
			TypeSearch
		}

		public const string	Title = "NG Unity Versioner";
		public const float	ResetTargetAssembliesWidth = 100F;
		public const float	TypeLabelWidth = 120F;
		public const int	MaxSimilarTypesDisplayed = 50;

		public static readonly CategoryTips[]	Tips = new CategoryTips[]
		{
			new CategoryTips
			{
				tips = new Tip[]
				{
					new Tip("Hold Ctrl when toggling a Unity version to only focus it.", () => XGUIHighlightManager.Highlight(NGUnityVersionerWindow.Title + ".checkbox")),
					new Tip("Press Ctrl-1 or Ctrl-2 to switch between tabs Namespace & Type."),
					new Tip("Press Ctrl-F to focus the Type input."),
					new Tip("If you need a specific version, contact me at support@ngtools.tech.", () => ContactFormWindow.Open(ContactFormWindow.Subject.Contact, NGUnityVersionerWindow.Title, string.Empty)),
					//new Tip("Use multithreading to speed up the execution. [Debug mode only]", () => XGUIHighlightManager.Highlight(NGUnityVersionerWindow.Title + ".useMultithreading")),
				}
			}
		};

		public Tab	tab = Tab.NamespaceSearch;

		#region Namespace
		public List<string> targetAssemblies = new List<string>() { @"Library\ScriptAssemblies" };
		public List<string>	filterNamespaces = new List<string>();
		public List<string>	targetNamespaces = new List<string>() { "UnityEngine", "UnityEditor" };
		public string		databasePath = "Packages/com.mikilo.ng-unity-versioner/Versions/" + DatabaseMeta.DatabaseFilename;
		[NonSerialized]
		private string		databasePathOrigin;
		public List<string>	activeUnityVersions = new List<string>();
		public bool			useMultithreading;
		public bool			displayWarnings = true;

		private ReorderableList	listAssemblies;
		private ReorderableList	listFilterNamespaces;
		private ReorderableList	listTargetNamespaces;
		private Vector2			scrollPositionNamespace;
		[NonSerialized]
		private string			detectedAssemblies;
		[NonSerialized]
		private string			detectedAssembliesTooltip;
		#endregion

		#region Type
		public string	typeInput = string.Empty;
		public string	memberInput = string.Empty;

		private Vector2			scrollPositionType;
		private TypeDatabase	typeDatabase;
		private bool			hasResult;
		private TypeResult		typeResult;
		private MemberTypes		filterMembers = MemberTypes.All;

		[NonSerialized]
		public GUIStyle	majorLabel;
		[NonSerialized]
		public GUIStyle	minorLabel;
		[NonSerialized]
		public GUIStyle	richLeftButton;
		#endregion

		[NonSerialized]
		private bool	tabNavigating;

		private ErrorPopup	errorPopup = new ErrorPopup(NGUnityVersionerWindow.Title, "An error occurred, try to reopen " + NGUnityVersionerWindow.Title + ". If it persists contact the author.");

		[MenuItem(
#if !NGTOOLS
			"Window/" +
#else
			Constants.MenuItemPath +
#endif
			NGUnityVersionerWindow.Title, priority = - 50)]
		public static void	Open()
		{
			Utility.OpenWindow<NGUnityVersionerWindow>(NGUnityVersionerWindow.Title);
		}

		protected virtual void	OnEnable()
		{
			Utility.RestoreIcon(this);

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
			this.listFilterNamespaces.elementHeight = 18F;
			this.listFilterNamespaces.drawElementCallback = (r, i, a, b) => { r.height = Constants.SingleLineHeight; this.filterNamespaces[i] = EditorGUI.TextField(r, this.filterNamespaces[i]); };
			this.listFilterNamespaces.onAddCallback = (r) => this.filterNamespaces.Add(string.Empty);

			this.listTargetNamespaces = new ReorderableList(this.targetNamespaces, typeof(string));
			this.listTargetNamespaces.drawHeaderCallback = (r) => GUI.Label(r, "Target Namespaces (beginning with)");
			this.listTargetNamespaces.elementHeight = 18F;
			this.listTargetNamespaces.drawElementCallback = (r, i, a, b) => { r.height = Constants.SingleLineHeight; this.targetNamespaces[i] = EditorGUI.TextField(r, this.targetNamespaces[i]); };
			this.listTargetNamespaces.onAddCallback = (r) => this.targetNamespaces.Add(string.Empty);

			this.databasePathOrigin = this.databasePath;
			DatabaseMeta.DatabasePath = this.databasePath;
			AssemblyUsages.useMultithreading = this.useMultithreading;

			this.wantsMouseMove = true;
		}

		protected virtual void	OnDisable()
		{
			Utility.SaveEditorPref(this, NGEditorPrefs.GetPerProjectPrefix());
		}

		protected virtual void	OnGUI()
		{
			Event	currentEvent = Event.current;

			if (currentEvent.type == EventType.MouseMove)
				this.Repaint();

			this.errorPopup.OnGUILayout();

			try
			{
				using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
				{
					EditorGUI.BeginChangeCheck();
					GUILayout.Toggle(this.tab == Tab.NamespaceSearch, "Namespace", GeneralStyles.ToolbarToggle);
					if (EditorGUI.EndChangeCheck() == true)
						this.tab = Tab.NamespaceSearch;

					if (this.tabNavigating == true)
					{
						Rect	r = GUILayoutUtility.GetLastRect();
						r.width = 10F;
						GUI.Label(r, "1", GUI.skin.textField);
					}

					EditorGUI.BeginChangeCheck();
					GUILayout.Toggle(this.tab == Tab.TypeSearch, "Type", GeneralStyles.ToolbarToggle);
					if (EditorGUI.EndChangeCheck() == true)
						this.tab = Tab.TypeSearch;

					if (this.tabNavigating == true)
					{
						Rect	r = GUILayoutUtility.GetLastRect();
						r.width = 10F;
						GUI.Label(r, "2", GUI.skin.textField);
					}
				}

				GUILayout.Space(2F);

				if (this.tab == Tab.NamespaceSearch)
					this.OnGUINamespaceSearch();
				else
					this.OnGUITypeSearch();
			}
			catch (Exception ex)
			{
				this.errorPopup.error = ex;
			}

			if (currentEvent.type == EventType.KeyDown)
			{
				if (currentEvent.keyCode == KeyCode.LeftControl ||
					currentEvent.keyCode == KeyCode.RightControl)
				{
					this.tabNavigating = !this.tabNavigating;
					this.Repaint();
				}
				else if (this.tabNavigating == true)
				{
					if (currentEvent.keyCode == KeyCode.Alpha1)
					{
						this.tab = Tab.NamespaceSearch;
						this.tabNavigating = false;
						GUI.FocusControl(null);
						currentEvent.Use();
						this.Repaint();
					}
					else if (currentEvent.keyCode == KeyCode.Alpha2)
					{
						this.tab = Tab.TypeSearch;
						this.tabNavigating = false;
						GUI.FocusControl(null);
						currentEvent.Use();
						this.Repaint();
					}
				}
			}

			TooltipHelper.PostOnGUI();
		}

		private void	OnGUINamespaceSearch()
		{
			DatabaseMeta	db = DatabaseMeta.GetDatabase();

			using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPositionNamespace))
			{
				this.scrollPositionNamespace = scroll.scrollPosition;

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

				string[]	versions = db.UnityVersions;

				using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
				{
					GUILayout.Label("Unity Versions");

					if (GUILayout.Button("All", EditorStyles.toolbarButton) == true)
					{
						this.activeUnityVersions.Clear();
						for (int i = 0, max = versions.Length; i < max; ++i)
							this.activeUnityVersions.Add(versions[i]);
					}

					if (GUILayout.Button("None", EditorStyles.toolbarButton) == true)
						this.activeUnityVersions.Clear();

					if (GUILayout.Button(string.Empty, EditorStyles.toolbarDropDown, GUILayoutOptionPool.Width(30F)) == true)
					{
						GenericMenu	menu = new GenericMenu();

						menu.AddItem(new GUIContent("Select above current version (" + Application.unityVersion + ")"), false, this.SelectAboveCurrentVersion);
						menu.AddItem(new GUIContent("Select highest of each version"), false, this.SelectHighestOfEachVersion);
						menu.AddItem(new GUIContent("Select lowest of each version"), false, this.SelectLowestOfEachVersion);

						if (versions.Length > 0)
						{
							List<int>	majors = new List<int>(8);

							for (int i = 0, max = versions.Length; i < max; ++i)
							{
								int	major = Utility.ParseInt(versions[i]);

								if (majors.Contains(major) == false)
								{
									majors.Add(major);

									string	majorString = major.ToString();

									menu.AddItem(new GUIContent("Select Unity >= " + majorString), false, this.SelectVersionsSuperiorOrEqual, majorString);
									menu.AddItem(new GUIContent("Select Unity " + majorString), false, this.SelectVersionsEqual, majorString);
								}
							}
						}

						menu.ShowAsContext();
					}
				}

				GUILayout.Space(3F);

				this.databasePath = NGEditorGUILayout.OpenFileField(null, this.databasePath);
				TooltipHelper.HelpBox("Path to a folder containing \"." + DatabaseMeta.MetaExtension + "\"", MessageType.Info);

				if (this.databasePath.EndsWith(DatabaseMeta.MetaExtension) == false)
					EditorGUILayout.HelpBox("Database file must end with \"" + DatabaseMeta.MetaExtension + "\".", MessageType.Error);
				else if (File.Exists(this.databasePath) == false)
					EditorGUILayout.HelpBox("Database does not exist at this path.", MessageType.Error);
				else
				{
					if (this.databasePath != this.databasePathOrigin &&
						GUILayout.Button("Update database") == true)
					{
						this.databasePathOrigin = this.databasePath;
						this.activeUnityVersions.Clear();
						DatabaseMeta.DatabasePath = this.databasePath;
					}
				}

				int lastMajor = 0;
				int	lastMinor = 0;

				for (int i = 0, max = versions.Length; i < max; ++i)
				{
					string	version = versions[i];
					Rect	r = GUILayoutUtility.GetRect(0F, Constants.SingleLineHeight, EditorStyles.label);
					float	width = r.width;

					EditorGUI.BeginChangeCheck();
					bool	toggle = EditorGUI.ToggleLeft(r, version, this.activeUnityVersions.Contains(version));
					if (EditorGUI.EndChangeCheck() == true)
					{
						if (Event.current.control == true)
						{
							this.activeUnityVersions.Clear();
							this.activeUnityVersions.Add(version);
						}
						else
						{
							if (toggle == true)
								this.activeUnityVersions.Add(version);
							else
								this.activeUnityVersions.Remove(version);
						}
					}

					r.width = 15F;
					XGUIHighlightManager.DrawHighlight(NGUnityVersionerWindow.Title + ".checkbox", this, r, XGUIHighlights.Wave | XGUIHighlights.Glow);
					r.width = width;

					this.DrawVersionSeparator(r, version, ref lastMajor, ref lastMinor);
				}
			}

			if (this.displayWarnings == true)
			{
				EditorGUILayout.HelpBox("Compatibility check does not handle Unity API moved to Package Manager.", MessageType.Warning);
				EditorGUILayout.HelpBox("API references between pre-processing directives can not be handled.", MessageType.Warning);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button(UtilityResources.WarningSmallIcon, GUILayoutOptionPool.ExpandWidthFalse) == true)
					this.displayWarnings = !this.displayWarnings;

				bool	isReady = db.IsReady;

				if (db.UnityVersions.Length == 0)
					Utility.content.tooltip = "No version available.";
				else if (isReady == false)
				{
					Utility.content.image = GeneralStyles.StatusWheel.image;
					Utility.content.tooltip = "Initializing database...";
					this.Repaint();
				}

				using (new EditorGUI.DisabledScope(Utility.content.tooltip != null))
				using (BgColorContentRestorer.Get(GeneralStyles.HighlightActionButton))
				{
					Utility.content.text = "Check Compatibilities";

					if (GUILayout.Button(Utility.content) == true)
						this.ScanNamespaceCompatibilities();

					Utility.content.image = null;
					Utility.content.tooltip = null;
				}

				if (Conf.DebugMode == Conf.DebugState.Verbose)
				{
					EditorGUI.BeginChangeCheck();
					this.useMultithreading = EditorGUILayout.ToggleLeft("Use Multithreading", this.useMultithreading, GUILayoutOptionPool.Width(130F));
					if (EditorGUI.EndChangeCheck() == true)
						AssemblyUsages.useMultithreading = this.useMultithreading;
					XGUIHighlightManager.DrawHighlightLayout(NGUnityVersionerWindow.Title + ".useMultithreading", this, XGUIHighlights.Wave | XGUIHighlights.Glow);
				}
			}
		}

		private void	OnGUITypeSearch()
		{
			Event	currentEvent = Event.current;

			if (currentEvent.type == EventType.ValidateCommand)
			{
				if (currentEvent.commandName == "Find")
					currentEvent.Use();
			}
			else if (currentEvent.type == EventType.ExecuteCommand)
			{
				if (currentEvent.commandName == "Find")
				{
					GUI.FocusControl("typeInput");
					EditorGUIUtility.editingTextField = true;
					this.scrollPositionType = Vector2.zero;
				}
			}

			using (var scroll = new EditorGUILayout.ScrollViewScope(this.scrollPositionType))
			{
				this.scrollPositionType = scroll.scrollPosition;

				Utility.content.text = this.typeInput;
				float	width = Mathf.Max(140F, EditorStyles.textField.CalcSize(Utility.content).x + 10F);

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.Label("Type", GUILayoutOptionPool.Width(width));
					GUILayout.Label("Member (Field, Property, Event, Method)");
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUI.BeginChangeCheck();
					GUI.SetNextControlName("typeInput");
					this.typeInput = EditorGUILayout.TextField(this.typeInput, GUILayoutOptionPool.Width(width));
					if (EditorGUI.EndChangeCheck() == true)
						Utility.RegisterIntervalCallback(this.ScanType, 50, 1);

					if (string.IsNullOrEmpty(this.typeInput) == true)
						GUI.Label(GUILayoutUtility.GetLastRect(), "Namespace.ClassName", GeneralStyles.TextFieldPlaceHolder);

					using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(this.typeInput)))
					{
						EditorGUI.BeginChangeCheck();
						this.memberInput = EditorGUILayout.TextField(this.memberInput);
						if (EditorGUI.EndChangeCheck() == true)
							Utility.RegisterIntervalCallback(this.ScanType, 50, 1);
					}
				}

				if (this.hasResult == true && this.typeResult != null)
				{
					GUILayout.Space(5F);

					if (this.typeResult.hasActiveType == false)
					{
						EditorGUILayout.HelpBox("Type \"" + this.typeResult.typeSearched + "\" not found.", MessageType.Warning);

						if (this.typeResult.similarTypes.Count > 0)
						{
							using (new EditorGUILayout.HorizontalScope(GeneralStyles.Toolbar))
							{
								GUILayout.Label("Similar types found:");

								if (this.typeResult.similarTypes.Count > NGUnityVersionerWindow.MaxSimilarTypesDisplayed)
								{
									GUILayout.FlexibleSpace();
									GUILayout.Label("(Too many results, " + (this.typeResult.similarTypes.Count - NGUnityVersionerWindow.MaxSimilarTypesDisplayed) + " discarded)", EditorStyles.centeredGreyMiniLabel);
								}
							}

							if (this.richLeftButton == null)
							{
								this.richLeftButton = new GUIStyle(GeneralStyles.LeftButton);
								this.richLeftButton.richText = true;
							}

							for (int i = 0, max = Mathf.Min(NGUnityVersionerWindow.MaxSimilarTypesDisplayed, this.typeResult.similarTypes.Count); i < max; ++i)
							{
								if (GUILayout.Button(this.typeResult.similarTypesLabel[i], this.richLeftButton) == true)
								{
									GUI.FocusControl(null);
									this.typeInput = this.typeResult.similarTypes[i].name;
									this.ScanType();
									return;
								}
							}
						}
					}
					else
					{
						if (this.typeResult.activeType.name != this.typeResult.typeSearched)
						{
							using (new EditorGUILayout.HorizontalScope())
							{
								EditorGUILayout.HelpBox("The Type found was the only result and it does not exactly match your input.", MessageType.Warning);

								if (GUILayout.Button("Fix", GUILayoutOptionPool.Height(40F)) == true)
								{
									GUI.FocusControl(null);
									this.typeResult.typeSearched = this.typeResult.activeType.name;
									this.typeInput = this.typeResult.activeType.name;
									return;
								}
							}

							GUILayout.Space(5F);
						}

						if (this.typeResult.activeMember.Count == 0 && this.typeResult.similarMembers.Count > 0)
						{
							using (new EditorGUILayout.HorizontalScope(GeneralStyles.Toolbar))
							{
								GUILayout.Label(this.typeResult.similarMembers.Count.ToString(), GUILayoutOptionPool.ExpandWidthFalse);
								GUILayout.Space(-4F);
								GUILayout.Label("similar members found:");

								this.DrawMemberTypeFilter(MemberTypes.Field);
								this.DrawMemberTypeFilter(MemberTypes.Property);
								this.DrawMemberTypeFilter(MemberTypes.Event);
								this.DrawMemberTypeFilter(MemberTypes.Method);
							}

							if (this.richLeftButton == null)
							{
								this.richLeftButton = new GUIStyle(GeneralStyles.LeftButton);
								this.richLeftButton.richText = true;
							}

							for (int i = 0, max = this.typeResult.similarMembers.Count; i < max; ++i)
							{
								if ((this.filterMembers & this.typeResult.similarMembers[i].type) == 0)
									continue;

								using (new EditorGUILayout.HorizontalScope())
								{
									GUILayout.Label(Enum.GetName(typeof(MemberTypes), this.typeResult.similarMembers[i].type), EditorStyles.centeredGreyMiniLabel, GUILayoutOptionPool.Width(55F));

									if (GUILayout.Button(this.typeResult.similarMembersLabel[i], this.richLeftButton) == true)
									{
										GUI.FocusControl(null);
										this.memberInput = this.typeResult.similarMembers[i].name;
										this.ScanType();
										return;
									}
								}
							}

							GUILayout.Space(5F);
						}

						string	fullSearch = this.typeResult.activeType.name + (this.typeResult.memberSearched.Length > 0 ? '.' + this.typeResult.memberSearched : string.Empty);

						using (new EditorGUILayout.HorizontalScope())
						{
							if (this.typeResult.activeType.isPublic == false)
								GUILayout.Label("internal", GeneralStyles.TextFieldPlaceHolder, GUILayoutOptionPool.ExpandWidthFalse);

							int	n = this.typeResult.activeType.name.LastIndexOf('.');

							if (n != -1)
							{
								using (ColorStyleRestorer.Get(GeneralStyles.Title1, Color.grey))
								{
									GUILayout.Label(this.typeResult.activeType.name.Substring(0, n + 1), GeneralStyles.Title1);
								}

								GUILayout.Space(-4F);
								GUILayout.Label(this.typeResult.activeType.name.Substring(n + 1), GeneralStyles.Title1);
							}
							else
								GUILayout.Label(this.typeResult.activeType.name, GeneralStyles.Title1);

							if (this.typeResult.memberSearched.Length > 0)
							{
								GUILayout.Space(-4F);
								GUILayout.Label(".", GeneralStyles.Title1);

								using (ColorStyleRestorer.Get(GeneralStyles.Title1, this.typeResult.activeMember.Count > 0 ? Color.green : Color.red))
								{
									GUILayout.Space(-4F);

									Utility.content.text = this.typeResult.memberSearched;

									Rect	r = GUILayoutUtility.GetRect(Utility.content, GeneralStyles.Title1);

									GUI.Label(r, Utility.content, GeneralStyles.Title1);
									EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);

									Rect	r2 = r;
									r2.x += r2.width;
									r2.width = 20F;

									if (GUI.Button(r2, string.Empty, GeneralStyles.ToolbarDropDown) == true ||
										(currentEvent.type == EventType.MouseUp &&
										 r.Contains(currentEvent.mousePosition) == true))
									{
										List<Member> members = new List<Member>(this.typeResult.activeType.members);

										members.Sort((a, b) => a.name.CompareTo(b.name));

										GenericMenu	menu = new GenericMenu();

										for (int i = 0, max = members.Count; i < max; ++i)
										{
											Member	member = members[i];

											menu.AddItem(new GUIContent(member.name), this.memberInput == member.name, s => { GUI.FocusControl(null); this.memberInput = (string)s; this.ScanType(); }, member.name);
										}

										menu.DropDown(r);
									}
								}
							}

							GUILayout.FlexibleSpace();
						}

						GUILayout.Space(5F);

						using (LabelWidthRestorer.Get(100F))
						{
							GUILayout.Label("Type preprocessor directives");
							EditorGUILayout.TextField("First Introduced", this.typeResult.typeFirstIntroducedPreprocessor);
							Utility.content.text = "Present In";
							EditorGUILayout.TextField(Utility.content, this.typeResult.typePresentInVersionsPreprocessor);
							TooltipHelper.HelpBox("Preprocessor directive might not be 100% accurate. Because the database does not contain all existing versions (releases, patches, beta, alpha).", MessageType.Warning);

							if (string.IsNullOrEmpty(this.typeResult.memberSearched) == false)
							{
								GUILayout.Space(2F);

								GUILayout.Label("Member preprocessor directives");
								if (string.IsNullOrEmpty(this.typeResult.memberFirstIntroducedPreprocessor) == false)
								{
									EditorGUILayout.TextField("First Introduced", this.typeResult.memberFirstIntroducedPreprocessor);
									EditorGUILayout.TextField(Utility.content, this.typeResult.memberPresentInVersionsPreprocessor);
									TooltipHelper.HelpBox("Preprocessor directive might not be 100% accurate. Because the database does not contain all existing versions (releases, patches, beta, alpha).", MessageType.Warning);
								}
								else
									GUILayout.Label("Member could not be found in any version.", EditorStyles.miniBoldLabel);
							}

							Utility.content.tooltip = null;
						}

						GUILayout.Space(5F);

						using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
						{
							GUILayout.Label("Type", GUILayoutOptionPool.Width(NGUnityVersionerWindow.TypeLabelWidth));

							if (string.IsNullOrEmpty(this.typeResult.memberSearched) == false)
								GUILayout.Label("Member");

							GUILayout.FlexibleSpace();
						}

						int	lastMajor = 0;
						int	lastMinor = 0;

						for (int i = 0, max = this.typeResult.activeType.versions.Length; i < max; ++i)
						{
							string	version = this.typeResult.versions[this.typeResult.activeType.versions[i]];

							using (new EditorGUILayout.HorizontalScope())
							using (new EditorGUI.DisabledScope(this.typeResult.activeType.isPublic == false))
							{
								if (GUILayout.Button(version, EditorStyles.miniButton, GUILayoutOptionPool.Width(NGUnityVersionerWindow.TypeLabelWidth)) == true)
								{
									string[]	parts = version.Split('.');

									Application.OpenURL("https://docs.unity3d.com/" + parts[0] + "." + parts[1] + "/Documentation/ScriptReference/30_search.html?q=" + this.typeResult.typeSearched);
								}

								if (this.typeResult.activeMember != null)
								{
									if (this.typeResult.memberPresentInVersions[i] == true)
									{
										if (GUILayout.Button("Present" + (this.typeResult.activeMember.Count > 1 ? " (" + this.typeResult.activeMember.Count + ')' : string.Empty), EditorStyles.miniButton, GUILayoutOptionPool.Width(NGUnityVersionerWindow.TypeLabelWidth)) == true)
										{
											string[]	parts = version.Split('.');

											Application.OpenURL("https://docs.unity3d.com/" + parts[0] + "." + parts[1] + "/Documentation/ScriptReference/30_search.html?q=" + fullSearch);
										}
									}
									else if (string.IsNullOrEmpty(this.typeResult.memberSearched) == false)
										GUILayout.Label("Absent", EditorStyles.miniLabel);
									else
										GUILayout.Label(string.Empty);
								}
							}

							this.DrawVersionSeparator(GUILayoutUtility.GetLastRect(), version, ref lastMajor, ref lastMinor);
						}
					}
				}
			}
		}

		private void	DrawVersionSeparator(Rect r, string version, ref int lastMajor, ref int lastMinor)
		{
			int	major = Utility.ParseInt(version);
			int	minor = Utility.ParseInt(version, major.ToString().Length + 1);

			if (lastMajor != major)
			{
				r.y -= 1F;

				if (lastMajor != 0)
				{
					r.height = 1F;
					EditorGUI.DrawRect(r, Color.grey);
				}

				if (this.majorLabel == null)
				{
					this.majorLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
					this.majorLabel.fontSize = 10;
					this.majorLabel.alignment = TextAnchor.UpperRight;
				}

				r.height = 16F;
				GUI.Label(r, major + "." + minor, this.majorLabel);
			}
			else if (lastMinor != minor)
			{
				r.y -= 1F;
				r.height = 1F;
				EditorGUI.DrawRect(r, Color.grey * .65F);

				if (this.minorLabel == null)
				{
					this.minorLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
					this.minorLabel.fontSize = 9;
					this.minorLabel.alignment = TextAnchor.UpperRight;
				}

				r.height = 16F;
				GUI.Label(r, major + "." + minor, this.minorLabel);
			}

			lastMajor = major;
			lastMinor = minor;
		}

		private void	DrawMemberTypeFilter(MemberTypes type)
		{
			if (this.HasMemberType(type) == true)
			{
				EditorGUI.BeginChangeCheck();
				string	name = Enum.GetName(typeof(MemberTypes), type);
				Utility.content.text = name;
				float	width = EditorStyles.toggle.CalcSize(Utility.content).x + 4F;
				EditorGUILayout.ToggleLeft(name, (this.filterMembers & type) != 0, GUILayoutOptionPool.Width(width));
				if (EditorGUI.EndChangeCheck() == true)
				{
					if (Event.current.control == true)
						this.filterMembers = type;
					else
						this.filterMembers ^= type;
				}
			}
		}

		private bool	HasMemberType(MemberTypes memberType)
		{
			for (int i = 0, max = this.typeResult.similarMembers.Count; i < max; ++i)
			{
				if ((this.typeResult.similarMembers[i].type & memberType) != 0)
					return true;
			}

			return false;
		}

		private void	ScanNamespaceCompatibilities()
		{
			List<string>	targetAssemblies = new List<string>();

			for (int i = 0, max = this.targetAssemblies.Count; i < max; ++i)
			{
				string	assemblyPath = this.targetAssemblies[i];

				if (Directory.Exists(assemblyPath) == true)
					targetAssemblies.AddRange(Directory.GetFiles(assemblyPath, "*.dll"));
				else if (File.Exists(assemblyPath) == true)
					targetAssemblies.Add(assemblyPath);
				else
					Debug.LogWarning("Assembly at \"" + assemblyPath + "\" does not exist.");
			}

			if (targetAssemblies.Count == 0)
			{
				this.ShowNotification(new GUIContent("No target assembly available."));
				Debug.LogWarning("No target assembly available.");
				return;
			}

			if (this.activeUnityVersions.Count == 0)
			{
				this.ShowNotification(new GUIContent("No versions selected."));
				Debug.LogWarning("No versions selected.");
				return;
			}

			if (File.Exists(this.databasePath) == false)
			{
				this.ShowNotification(new GUIContent("Database at \"" + this.databasePath + "\" does not exist."));
				Debug.LogWarning("Database at \"" + this.databasePath + "\" does not exist.");
				return;
			}

			targetAssemblies.Distinct();

			using (WatchTime.Get("Scan code compatibilities completed."))
			{
				AssemblyUsagesResult[]	results = AssemblyUsages.CheckCompatibilities(targetAssemblies, this.filterNamespaces.ToArray(), this.targetNamespaces.ToArray(), this.activeUnityVersions);

				if (results != null)
					Utility.OpenWindow<AssemblyUsagesResultWindow>(AssemblyUsagesResultWindow.Title, true, w => w.SetResults(results));
			}
		}

		private void	ScanType()
		{
			this.Repaint();

			if (this.typeDatabase == null)
			{
				try
				{
					EditorUtility.DisplayProgressBar(NGUnityVersionerWindow.Title, "Building type database from cache...", 0F);
					this.typeDatabase = new TypeDatabase();

					string[]	versions = DatabaseMeta.GetDatabase().UnityVersions;

					if (versions.Length != this.typeDatabase.Versions.Length)
						this.typeDatabase = null;
					else
					{
						int hash = 0;

						for (int i = 0, max = this.typeDatabase.Versions.Length; i < max; ++i)
							hash += this.typeDatabase.Versions[i].GetHashCode();

						for (int i = 0, max = versions.Length; i < max; ++i)
							hash -= versions[i].GetHashCode();

						if (hash != 0)
							this.typeDatabase = null;
					}
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
				finally
				{
					if (this.typeDatabase == null)
					{
						DatabaseMeta	db = DatabaseMeta.GetDatabase();

						EditorUtility.DisplayProgressBar(NGUnityVersionerWindow.Title, "Type database is obsolete and must update. Extracting...", .33F);
						db.ExtractAll();

						EditorUtility.DisplayProgressBar(NGUnityVersionerWindow.Title, "Generating type database...", .66F);

						this.typeDatabase = new TypeDatabase(db);
						this.typeDatabase.Save();
					}

					EditorUtility.ClearProgressBar();
				}
			}

			if (string.IsNullOrWhiteSpace(this.typeInput) == false)
			{
				this.typeResult = this.typeDatabase.Scan(this.typeInput.Trim(), this.memberInput.Trim());
				this.hasResult = true;
			}
			else
				this.hasResult = false;
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
			string[]	versions = DatabaseMeta.GetDatabase().UnityVersions;
			string		majorVersion = (string)raw;
			int			majorVersionInt = int.Parse(majorVersion);

			this.activeUnityVersions.Clear();

			for (int i = 0, max = versions.Length; i < max; ++i)
			{
				string	metaFile = versions[i];
				int		dot = metaFile.IndexOf('.');

				if (dot == -1)
					continue;

				string	metaMajorVersion = metaFile.Substring(0, dot);
				int		metaMajorVersionInt;

				if (int.TryParse(metaMajorVersion, out metaMajorVersionInt) == true)
				{
					if (metaMajorVersionInt >= majorVersionInt)
						this.activeUnityVersions.Add(metaFile);
				}
			}
		}

		private void	SelectVersionsEqual(object raw)
		{
			string[]	versions = DatabaseMeta.GetDatabase().UnityVersions;
			string		version = (string)raw;

			this.activeUnityVersions.Clear();

			for (int i = 0, max = versions.Length; i < max; ++i)
			{
				string	metaFile = versions[i];

				if (metaFile.StartsWith(version) == true)
					this.activeUnityVersions.Add(metaFile);
			}
		}

		private void	SelectLowestOfEachVersion()
		{
			string[]					versions = DatabaseMeta.GetDatabase().UnityVersions;
			Dictionary<string, string>	lowestVersions = new Dictionary<string, string>();

			for (int i = 0, max = versions.Length; i < max; ++i)
			{
				string	metaFile = versions[i];
				int		dot = metaFile.IndexOf('.');

				if (dot == -1)
					continue;

				dot = metaFile.IndexOf('.', dot + 1);
				if (dot == -1)
					continue;

				string	metaMajorVersion = metaFile.Substring(0, dot);

				if (lowestVersions.ContainsKey(metaMajorVersion) == false)
					lowestVersions.Add(metaMajorVersion, versions[i]);
				else
				{
					if (lowestVersions[metaMajorVersion].CompareTo(versions[i]) > 0)
						lowestVersions[metaMajorVersion] = versions[i];
				}
			}

			this.activeUnityVersions.Clear();
			foreach (string version in lowestVersions.Values)
				this.activeUnityVersions.Add(version);
		}

		private void	SelectHighestOfEachVersion()
		{
			string[]					versions = DatabaseMeta.GetDatabase().UnityVersions;
			Dictionary<string, string>	highestVersions = new Dictionary<string, string>();

			for (int i = 0, max = versions.Length; i < max; ++i)
			{
				string	metaFile = versions[i];
				int		dot = metaFile.IndexOf('.');

				if (dot == -1)
					continue;

				dot = metaFile.IndexOf('.', dot + 1);
				if (dot == -1)
					continue;

				string	metaMajorVersion = metaFile.Substring(0, dot);

				if (highestVersions.ContainsKey(metaMajorVersion) == false)
					highestVersions.Add(metaMajorVersion, versions[i]);
				else
				{
					if (highestVersions[metaMajorVersion].CompareTo(versions[i]) < 0)
						highestVersions[metaMajorVersion] = versions[i];
				}
			}

			this.activeUnityVersions.Clear();
			foreach (string version in highestVersions.Values)
				this.activeUnityVersions.Add(version);
		}

		private void	SelectAboveCurrentVersion()
		{
			List<string>	versions = new List<string>(DatabaseMeta.GetDatabase().UnityVersions);

			versions.Add(Application.unityVersion);
			versions.Sort(Utility.CompareVersion);

			for (int i = 0, max = versions.Count; i < max; ++i)
			{
				string	metaFile = versions[i];

				if (metaFile == Application.unityVersion)
				{
					--i;

					this.activeUnityVersions.Clear();
					for (; i >= 0; --i)
						this.activeUnityVersions.Add(versions[i]);

					break;
				}
			}
		}

		protected virtual void	ShowButton(Rect r)
		{
			WindowTipsWindow.DefaultShowButton(r, this, NGUnityVersionerWindow.Title, NGUnityVersionerWindow.Tips);
		}

		void	IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
		{
			menu.AddItem(new GUIContent("Update Database"), false, this.UpdateDatabase);
		}

		private void	UpdateDatabase()
		{
			AssemblyUsages.debug = DebugItems.WatchTime;
			DatabaseMeta	db = DatabaseMeta.GetDatabase();

			foreach (var pair in UnityInstalls.EachInstall)
			{
				string		unityPath = pair.Value;
				UnityMeta	unityMeta = db.Get(Utility.GetUnityVersion(unityPath));

				if (unityMeta == null)
				{
					UnityMeta.Create(unityPath);
					Debug.Log("Added \"" + unityPath + "\" to the meta database.");
				}
			}

			using (WatchTime.Get("Database written at " + DatabaseMeta.DatabasePath + " (" + db.UnityMeta.Length + " versions)"))
			{
				db.Save(DatabaseMeta.DatabasePath);
			}
		}
	}
}