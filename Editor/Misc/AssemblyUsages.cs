using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class AssemblyUsages
	{
		[Flags]
		public enum DebugItems
		{
			None = 0,
			HeaderTypes = 1 << 0,
			Types = 1 << 1,
			HeaderFields = 1 << 4,
			Fields = 1 << 5,
			HeaderMethods = 1 << 8,
			Methods = 1 << 9,
			HeaderMissingRefs = 1 << 10,
			MissingRefs = 1 << 11,
			HeaderFoundRefs = 1 << 11,
			FoundRefs = 1 << 12,
			WatchTime = 1 << 13,
			Headers = HeaderTypes | HeaderFields | HeaderMethods | HeaderMissingRefs | HeaderFoundRefs
		}

		public const string	MetaExtension = "metadll";

		public static DebugItems	debug = DebugItems.None;

		private static Dictionary<string, UnityMeta>	assembliesMeta = new Dictionary<string, UnityMeta>();

		[SerializeField]
		private string[]	assemblies;
		public string[]		Assemblies { get { return this.assemblies; } }
		[SerializeField]
		private string[]	filterNamespaces;
		public string[]		FilterNamespaces { get { return this.filterNamespaces; } }
		[SerializeField]
		private string[]	targetNamespaces;
		public string[]		TargetNamespaces { get { return this.targetNamespaces; } }

		// Events & properties are not "used" directly, they are replaced with fields & methods in IL.
		public HashSet<TypeReference>		types = new HashSet<TypeReference>();
		public HashSet<FieldReference>		fields = new HashSet<FieldReference>();
		public HashSet<MethodReference>		methods = new HashSet<MethodReference>();

		public static AssemblyUsagesResult[]	CheckCompatibilities(IEnumerable<string> assembliesPath, string[] filterNamespaces, string[] targetNamespaces, IEnumerable<string> assemblyMetaPaths, IEnumerable<string> unityPaths, string outputMetaPath, bool useMultithreading = false)
		{
			if (targetNamespaces.Length == 0)
			{
				Debug.LogWarning("You must target at least one namespace.");
				return null;
			}

			List<string>	parsedMeta = new List<string>();
			AssemblyUsages	usages;

			using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Extracted usages from target assemblies"))
			{
				usages = AssemblyUsages.InspectAssembly(assembliesPath, filterNamespaces, targetNamespaces);
			}

			List<TypeReference>		types = new List<TypeReference>(usages.types);
			List<FieldReference>	fields = new List<FieldReference>(usages.fields);
			List<MethodReference>	methods = new List<MethodReference>(usages.methods);

			using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Filtering usages (Targeting " + string.Join(", ", targetNamespaces) + ")"))
			{
				usages.FilterReferences(types, fields, methods);
			}

			List<Thread>				threads = new List<Thread>();
			List<AssemblyUsagesResult>	results = new List<AssemblyUsagesResult>();
			int							processorCount = Environment.ProcessorCount;
			Exception					threadException = null;

			foreach (string assemblyMetaPath in assemblyMetaPaths)
			{
				Action	callback = () =>
				{
					try
					{
						UnityMeta	unityMeta;

						lock (parsedMeta)
						{
							if (parsedMeta.Contains(assemblyMetaPath) == true)
								return;

							parsedMeta.Add(assemblyMetaPath);
						}

						if (AssemblyUsages.assembliesMeta.TryGetValue(assemblyMetaPath, out unityMeta) == false)
						{
							try
							{
								using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Read meta from \"" + assemblyMetaPath + "\""))
								{
									unityMeta = UnityMeta.Load(assemblyMetaPath);
								}
							}
							catch (Exception ex)
							{
								Debug.LogException(ex);
								unityMeta = new UnityMeta();
							}

							AssemblyUsages.assembliesMeta.Add(assemblyMetaPath, unityMeta);
						}

						AssemblyUsagesResult	result = new AssemblyUsagesResult() { assemblyUsages = usages, unityMeta = unityMeta };

						using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Resolved for " + assemblyMetaPath))
						{
							result.ResolveReferences(types, fields, methods);
						}

						lock (results)
						{
							results.Add(result);
						}
					}
					catch (Exception ex)
					{
						threadException = ex;
					}
				};

				if (useMultithreading == true)
				{
					while (threads.Count >= processorCount)
					{
						for (int i = 0, max = threads.Count; i < max; ++i)
						{
							if (threads[i].IsAlive == false)
							{
								threads.RemoveAt(i--);
								--max;
							}
						}

						Thread.Sleep(5);
					}

					Thread	thread = new Thread(new ThreadStart(callback));
					thread.Name = "Resolving " + assemblyMetaPath;
					threads.Add(thread);
					thread.Start();
				}
				else
					callback();
			}

			if (Directory.Exists(outputMetaPath) == false)
				Debug.LogWarning("Target folder for assembly meta files at \"" + outputMetaPath + "\" does not exist.");

			foreach (string unityPath in unityPaths)
			{
				string	unityVersion = Utility.GetUnityVersion(unityPath);
				string	assemblyMetaPath = Path.Combine(outputMetaPath, unityVersion + "." + AssemblyUsages.MetaExtension);
				Action	callback = () =>
				{
					try
					{
						UnityMeta	unityMeta;

						lock (parsedMeta)
						{
							if (parsedMeta.Contains(assemblyMetaPath) == true)
								return;

							parsedMeta.Add(assemblyMetaPath);
						}

						if (AssemblyUsages.assembliesMeta.TryGetValue(assemblyMetaPath, out unityMeta) == false)
						{
							try
							{
								if (File.Exists(assemblyMetaPath) == false)
								{
									using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Read assemblies from \"" + unityPath + "\""))
									{
										// Gather all assemblies referencing UnityEditor.
										string	unityEditor = Path.Combine(unityPath, @"Editor\Data\Managed\UnityEditor.dll");

										if (File.Exists(unityEditor) == false)
										{
											Debug.LogError("Assembly at \"" + unityEditor + "\" was not found.");
											return;
										}

										string[]	editorAssemblies = Directory.GetFiles(Path.Combine(unityPath, @"Editor\Data\Managed"), "*.dll");

										for (int i = 0, max = editorAssemblies.Length; i < max; ++i)
										{
											using (AssemblyDefinition assemblyDef = AssemblyDefinition.ReadAssembly(editorAssemblies[i]))
											{
												int	j = 0;
												int	max2 = assemblyDef.Modules.Count;

												for (; j < max2; ++j)
												{
													ModuleDefinition	moduleDef = assemblyDef.Modules[j];

													if (moduleDef.HasAssemblyReferences == true)
													{
														int	k = 0;
														int	max3 = moduleDef.AssemblyReferences.Count;

														for (; k < max3; ++k)
														{
															if (moduleDef.AssemblyReferences[k].Name == "UnityEditor")
																break;
														}

														if (k < max3)
															break;
													}
												}

												if (j == max2)
													editorAssemblies[i] = null;
											}
										}

										string		runtimeAssembliesPath = Path.Combine(unityPath, @"Editor\Data\Managed\UnityEngine");
										string[]	runtimeAssemblies;

										if (Directory.Exists(runtimeAssembliesPath) == true)
											runtimeAssemblies = Directory.GetFiles(runtimeAssembliesPath, "*.dll");
										else
										{
											// Switch to Unity <=2017.1.
											runtimeAssembliesPath = Path.Combine(unityPath, @"Editor\Data\Managed\UnityEngine.dll");

											if (File.Exists(runtimeAssembliesPath) == false)
											{
												Debug.LogError("Runtime assembly at \"" + runtimeAssembliesPath + "\" was not found.");
												return;
											}

											runtimeAssemblies = new string[] { runtimeAssembliesPath };
										}

										string		extensionAssembliesPath = Path.Combine(unityPath, @"Editor\Data\UnityExtensions\Unity");
										string[]	extensionAssemblies;

										if (Directory.Exists(extensionAssembliesPath) == false) // Does not exist in Unity 4.5.
											extensionAssemblies = new string[0];
										else
											extensionAssemblies = Directory.GetFiles(extensionAssembliesPath, "*.dll", SearchOption.AllDirectories);

										List<AssemblyMeta>	meta = new List<AssemblyMeta>();

										try
										{
											meta.Add(new AssemblyMeta(unityEditor.Substring(unityPath.Length + 1), unityEditor));
										}
										catch (Exception)
										{
											Debug.LogError("Main editor assembly \"" + unityEditor + "\" failed extraction.");
											throw;
										}

										for (int i = 0, max = runtimeAssemblies.Length; i < max; ++i)
										{
											try
											{
												meta.Add(new AssemblyMeta(runtimeAssemblies[i].Substring(unityPath.Length + 1), runtimeAssemblies[i]));
											}
											catch (Exception)
											{
												Debug.LogError("Runtime assembly \"" + runtimeAssemblies[i] + "\" failed extraction.");
												throw;
											}
										}

										for (int i = 0, max = editorAssemblies.Length; i < max; ++i)
										{
											try
											{
												if (editorAssemblies[i] != null)
													meta.Add(new AssemblyMeta(editorAssemblies[i].Substring(unityPath.Length + 1), editorAssemblies[i]));
											}
											catch (Exception)
											{
												Debug.LogError("Editor assembly \"" + editorAssemblies[i] + "\" failed extraction.");
												throw;
											}
										}

										for (int i = 0, max = extensionAssemblies.Length; i < max; ++i)
										{
											try
											{
												meta.Add(new AssemblyMeta(extensionAssemblies[i].Substring(unityPath.Length + 1), extensionAssemblies[i]));
											}
											catch (Exception)
											{
												Debug.LogWarning("Extension assembly \"" + extensionAssemblies[i] + "\" failed extraction and has been discarded.");
											}
										}

										unityMeta = new UnityMeta(unityVersion, meta.ToArray());
									}

									if (assemblyMetaPath != null)
									{
										using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Save meta at \"" + assemblyMetaPath + "\""))
										{
											unityMeta.Save(assemblyMetaPath);
										}
									}
								}
								else
								{
									using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Read meta from \"" + assemblyMetaPath +"\""))
									{
										unityMeta = UnityMeta.Load(assemblyMetaPath);
									}
								}
							}
							catch (Exception ex)
							{
								Debug.LogException(ex);
								unityMeta = new UnityMeta();
								throw;
							}

							AssemblyUsages.assembliesMeta.Add(assemblyMetaPath, unityMeta);
						}

						AssemblyUsagesResult	result = new AssemblyUsagesResult() { assemblyUsages = usages, unityMeta = unityMeta };

						using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Resolved against " + unityMeta.Version))
						{
							result.ResolveReferences(types, fields, methods);
						}

						lock (results)
						{
							results.Add(result);
						}
					}
					catch (Exception ex)
					{
						threadException = ex;
					}
				};

				if (useMultithreading == true)
				{
					while (threads.Count >= processorCount)
					{
						for (int i = 0, max = threads.Count; i < max; ++i)
						{
							if (threads[i].IsAlive == false)
							{
								threads.RemoveAt(i--);
								--max;
							}
						}

						Thread.Sleep(5);
					}

					Thread	thread = new Thread(new ThreadStart(callback));
					thread.Name = "Resolving " + assemblyMetaPath;
					threads.Add(thread);
					thread.Start();
				}
				else
					callback();
			}

			if (useMultithreading == true)
			{
				while (threads.Count > 0)
					{
					if (threads[0].IsAlive == false)
						threads.RemoveAt(0);
					Thread.Sleep(10);
				}
			}

			if (threadException != null)
				throw threadException;

			results.Sort(Utility.CompareVersion);

			return results.ToArray();
		}

		public static AssemblyUsages	InspectAssembly(IEnumerable<string> assembliesPath, string[] filterNamespaces, string[] targetNamespaces)
		{
			AssemblyUsages	result = new AssemblyUsages()
			{
				assemblies = new List<string>(assembliesPath).ToArray(),
				filterNamespaces = filterNamespaces,
				targetNamespaces = targetNamespaces,
			};

			foreach (string assemblyPath in assembliesPath)
			{
				using (AssemblyDefinition	assemblyDef = AssemblyDefinition.ReadAssembly(assemblyPath))
				{
					AssemblyUsagesExtractor.InspectAssembly(result, assemblyDef);
				}
			}

			return result;
		}

		private	AssemblyUsages()
		{
		}

		public bool	RegisterTypeRef(TypeReference typeRef)
		{
			if (typeRef.IsArray == true || typeRef.IsByReference == true || typeRef.IsPointer == true || typeRef.IsFunctionPointer == true)
				return this.RegisterTypeRef(typeRef.GetElementType());
			else if (this.types.Contains(typeRef) == false)
			{
				this.types.Add(typeRef);
				return true;
			}

			return false;
		}

		public bool	RegisterFieldRef(FieldReference fieldRef)
		{
			if (this.fields.Contains(fieldRef) == false)
			{
				this.RegisterTypeRef(fieldRef.DeclaringType);
				this.RegisterTypeRef(fieldRef.FieldType);
				this.fields.Add(fieldRef);
				return true;
			}

			return false;
		}

		public bool	RegisterMethodRef(MethodReference methodRef)
		{
			if (methodRef.DeclaringType.IsArray == true)
				return false;

			if (this.methods.Contains(methodRef) == false)
			{
				this.RegisterTypeRef(methodRef.DeclaringType);
				this.RegisterTypeRef(methodRef.ReturnType);
				this.methods.Add(methodRef);
				return true;
			}

			return false;
		}

		private void	FilterReferences(List<TypeReference> types, List<FieldReference> fields, List<MethodReference> methods)
		{
			int	typesCount = types.Count;
			int	fieldsCount = fields.Count;
			int	methodsCount = methods.Count;

			for (int i = 0, max = types.Count; i < max; ++i)
			{
				if (this.IsTypeVisible(types[i]) == false)
				{
					types.RemoveAt(i--);
					--max;
				}
			}

			types = types.Distinct(new CompareType()).ToList();

			if ((AssemblyUsages.debug & DebugItems.HeaderTypes) != 0)
				Debug.Log("Types Referenced (" + types.Count + " / " + typesCount + ")");

			if ((AssemblyUsages.debug & DebugItems.Types) != 0)
			{
				types.Sort((a, b) => this.GetFullNameWithoutNamespace(a).CompareTo(this.GetFullNameWithoutNamespace(b)));

				foreach (var item in types)
					Debug.Log(this.GetFullNameWithoutNamespace(item));
			}

			for (int i = 0, max = fields.Count; i < max; ++i)
			{
				FieldReference	field = fields[i];

				if (this.IsTypeVisible(field.DeclaringType) == false)
				{
					fields.RemoveAt(i--);
					--max;
					continue;
				}

				FieldDefinition	fieldDef = field as FieldDefinition;

				if (fieldDef != null && this.IsCompiledGenerated(fieldDef) == true)
				{
					fields.RemoveAt(i--);
					--max;
					continue;
				}

				if (field.Name[0] == '<')
				{
					fields.RemoveAt(i--);
					--max;
				}
			}

			if ((AssemblyUsages.debug & DebugItems.HeaderFields) != 0)
				Debug.Log("Fields Referenced (" + fields.Count + " / " + fieldsCount + ")");

			if ((AssemblyUsages.debug & DebugItems.Fields) != 0)
			{
				fields.Sort((a, b) => this.GetFullNameWithoutNamespace(a).CompareTo(this.GetFullNameWithoutNamespace(b)));

				foreach (var item in fields)
					Debug.Log(this.GetFullNameWithoutNamespace(item));
			}

			for (int i = 0, max = methods.Count; i < max; ++i)
			{
				MethodReference	method = methods[i];

				if (this.IsTypeVisible(method.DeclaringType) == false)
				{
					methods.RemoveAt(i--);
					--max;
					continue;
				}

				MethodDefinition	methodDef = method as MethodDefinition;

				if (methodDef != null && this.IsCompiledGenerated(methodDef) == true &&
					methodDef.IsAddOn == false && methodDef.IsRemoveOn == false && methodDef.IsGetter == false && methodDef.IsSetter == false)
				{
					methods.RemoveAt(i--);
					--max;
					continue;
				}
			}

			methods = methods.Distinct(new CompareMethod()).ToList();

			if ((AssemblyUsages.debug & DebugItems.HeaderMethods) != 0)
				Debug.Log("Methods Referenced (" + methods.Count + " / " + methodsCount + ")");

			if ((AssemblyUsages.debug & DebugItems.Methods) != 0)
			{
				methods.Sort((a, b) => this.GetFullNameWithoutNamespace(a).CompareTo(this.GetFullNameWithoutNamespace(b)));

				foreach (var item in methods)
					Debug.Log(item);
			}
		}

		private string	GetFullNameWithoutNamespace(TypeReference type)
		{
			if (type.IsNested == true)
				return this.GetFullNameWithoutNamespace(type.DeclaringType) + "." + type.Name;
			return type.FullName;
		}

		private string	GetFullNameWithoutNamespace(MemberReference field)
		{
			return this.GetFullNameWithoutNamespace(field.DeclaringType) + '.' + field.Name;
		}

		private bool	IsCompiledGenerated(ICustomAttributeProvider attributeProvider)
		{
			if (attributeProvider.HasCustomAttributes == true)
			{
				foreach (CustomAttribute attribute in attributeProvider.CustomAttributes)
				{
					if (attribute.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName)
						return true;
				}
			}

			return false;
		}

		private bool	IsTypeVisible(TypeReference typeRef)
		{
			if (this.targetNamespaces.Length > 0 && this.targetNamespaces.FirstOrDefault(ns => typeRef.Namespace.StartsWith(ns)) == null)
				return false;

			TypeDefinition	typeDef = typeRef as TypeDefinition;
			if (typeDef != null && this.IsCompiledGenerated(typeDef) == true)
				return false;

			if (typeRef.Name[0] == '<') // Generated by compiler.
				return false;

			return true;
		}

		private class CompareType : IEqualityComparer<TypeReference>
		{
			bool	IEqualityComparer<TypeReference>.Equals(TypeReference x, TypeReference y)
			{
				return x.Name == y.Name;
			}

			int		IEqualityComparer<TypeReference>.GetHashCode(TypeReference obj)
			{
				return obj.Name.GetHashCode();
			}
		}

		private class CompareMethod : IEqualityComparer<MethodReference>
		{
			bool	IEqualityComparer<MethodReference>.Equals(MethodReference x, MethodReference y)
			{
				return x.ToString() == y.ToString();
			}

			int		IEqualityComparer<MethodReference>.GetHashCode(MethodReference obj)
			{
				return obj.ToString().GetHashCode();
			}
		}
	}
}