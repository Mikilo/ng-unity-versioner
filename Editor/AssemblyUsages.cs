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

		private static Dictionary<string, AssemblyMeta[]>	assembliesMeta = new Dictionary<string, AssemblyMeta[]>();

		public List<string>	assemblies = new List<string>();
		public string[]		filterNamespaces;
		public string[]		targetNamespaces;

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

			AssemblyUsages.assembliesMeta.Clear();

			AssemblyUsages	usages;

			using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Extracted usages from many assemblies"))
			{
				usages = AssemblyUsagesExtractor.InspectAssembly(assembliesPath, filterNamespaces, targetNamespaces);
			}

			List<TypeReference>		types = new List<TypeReference>(usages.types);
			List<FieldReference>	fields = new List<FieldReference>(usages.fields);
			List<MethodReference>	methods = new List<MethodReference>(usages.methods);

			using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Filtering Usages (Targeting " + string.Join(", ", targetNamespaces) + ")"))
			{
				usages.FilterReferences(types, fields, methods);
			}

			//if (useMultithreading == true)
			//	Debug.Log("Running on " + Environment.ProcessorCount + " threads.");

			List<Thread>				threads = new List<Thread>();
			List<AssemblyUsagesResult>	results = new List<AssemblyUsagesResult>();
			int							processorCount = Environment.ProcessorCount;
			Exception					threadException = null;

			AssemblyUsages.assembliesMeta.Clear();
			foreach (string assemblyMetaPath in assemblyMetaPaths)
			{
				Action	callback = () =>
				{
					try
					{
						AssemblyMeta[]	assembliesMeta;

						if (AssemblyUsages.assembliesMeta.TryGetValue(assemblyMetaPath, out assembliesMeta) == false)
						{
							try
							{
								using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Read meta from \"" + assemblyMetaPath + "\""))
								{
									assembliesMeta = AssemblyMeta.Load(assemblyMetaPath);
								}
							}
							catch (Exception ex)
							{
								Debug.LogException(ex);
								assembliesMeta = new AssemblyMeta[0];
							}

							AssemblyUsages.assembliesMeta.Add(assemblyMetaPath, assembliesMeta);
						}

						AssemblyUsagesResult	result = new AssemblyUsagesResult() { assemblyUsages = usages, unityPath = assemblyMetaPath, assembliesMeta = assembliesMeta };

						using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Resolved for " + assemblyMetaPath))
						{
							result.ResolveReferences(types, fields, methods);
						}

						//this.DebugResult(result, AssemblyUsages.debug);

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
			AssemblyUsages.assembliesMeta.Clear();

			foreach (string unityPath in unityPaths)
			{
				string	assemblyMetaPath = Path.Combine(outputMetaPath, Utility.GetUnityVersion(unityPath) + "." + AssemblyUsages.MetaExtension);
				Action	callback = () =>
				{
					try
					{
						AssemblyMeta[]	assembliesMeta;

						if (AssemblyUsages.assembliesMeta.TryGetValue(assemblyMetaPath, out assembliesMeta) == false)
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
											meta.Add(new AssemblyMeta(unityEditor));
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
												meta.Add(new AssemblyMeta(runtimeAssemblies[i]));
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
													meta.Add(new AssemblyMeta(editorAssemblies[i]));
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
												meta.Add(new AssemblyMeta(extensionAssemblies[i]));
											}
											catch (Exception)
											{
												Debug.LogWarning("Extension assembly \"" + extensionAssemblies[i] + "\" failed extraction and has been discarded.");
											}
										}

										assembliesMeta = meta.ToArray();
									}

									if (assemblyMetaPath != null)
									{
										using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Save meta at \"" + assemblyMetaPath + "\""))
										{
											AssemblyMeta.Save(assemblyMetaPath, assembliesMeta);
										}
									}
								}
								else
								{
									using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Read meta from \"" + assemblyMetaPath +"\""))
									{
										assembliesMeta = AssemblyMeta.Load(assemblyMetaPath);
									}
								}
							}
							catch (Exception ex)
							{
								Debug.LogException(ex);
								assembliesMeta = new AssemblyMeta[0];
								throw;
							}

							AssemblyUsages.assembliesMeta.Add(assemblyMetaPath, assembliesMeta);
						}

						AssemblyUsagesResult	result = new AssemblyUsagesResult() { assemblyUsages = usages, unityPath = unityPath, assembliesMeta = assembliesMeta };

						using ((debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Resolved for " + assemblyMetaPath))
						{
							result.ResolveReferences(types, fields, methods);
						}

						//this.DebugResult(result, AssemblyUsages.debug);

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

			results.Sort((a, b) => a.unityPath.CompareTo(b.unityPath));

			return results.ToArray();
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

		private void	DebugResult(AssemblyUsagesResult result, DebugItems display)
		{
			if ((display & DebugItems.HeaderMissingRefs) != 0)
				Debug.Log($"Missing References ({result.missingTypes.Count + result.missingFields.Count + result.missingMethods.Count})");

			if ((display & DebugItems.MissingRefs) != 0)
			{
				if (result.missingTypes.Count > 0)
				{
					Debug.Log($"  Missing Types ({result.missingTypes.Count})");
					for (int j = 0, max2 = result.missingTypes.Count; j < max2; ++j)
						Debug.Log("    " + result.missingTypes[j]);
				}

				if (result.missingFields.Count > 0)
				{
					Debug.Log($"  Missing Fields ({result.missingFields.Count})");
					for (int j = 0, max2 = result.missingFields.Count; j < max2; ++j)
						Debug.Log("    " + result.missingFields[j]);
				}

				if (result.missingMethods.Count > 0)
				{
					Debug.Log($"  Missing Methods ({result.missingMethods.Count})");
					for (int j = 0, max2 = result.missingMethods.Count; j < max2; ++j)
						Debug.Log("    " + result.missingMethods[j]);
				}
			}

			if ((display & DebugItems.HeaderFoundRefs) != 0)
				Debug.Log($"Founds Refs ({result.foundTypes.Count + result.foundFields.Count + result.foundMethods.Count})");

			if ((display & DebugItems.FoundRefs) != 0)
			{
				if (result.foundTypes.Count > 0)
				{
					Debug.Log($"  Found Types ({result.foundTypes.Count})");
					for (int j = 0, max2 = result.foundTypes.Count; j < max2; ++j)
					{
						Debug.Log("    " + result.foundTypes[j]);
						if (result.foundTypes[j].ErrorMessage != null)
							Debug.Log("      Error: " + result.foundTypes[j].ErrorMessage);
					}
				}

				if (result.foundFields.Count > 0)
				{
					for (int j = 0, max2 = result.foundFields.Count; j < max2; ++j)
					{
						Debug.Log("    " + result.foundFields[j]);
						if (result.foundFields[j].ErrorMessage != null)
							Debug.Log("      Error: " + result.foundFields[j].ErrorMessage);
					}
				}

				if (result.foundMethods.Count > 0)
				{
					for (int j = 0, max2 = result.foundMethods.Count; j < max2; ++j)
					{
						Debug.Log("    " + result.foundMethods[j]);
						if (result.foundMethods[j].ErrorMessage != null)
							Debug.Log("      Error: " + result.foundMethods[j].ErrorMessage);
					}
				}
			}
			else
			{
				if (result.foundTypes.Count > 0)
				{
					for (int j = 0, first = 0, max2 = result.foundTypes.Count; j < max2; ++j)
					{
						if (result.foundTypes[j].ErrorMessage != null)
						{
							if (first == 0)
							{
								first = 1;
								Debug.Log("Found Types (with error)");
							}

							Debug.Log("  " + result.foundTypes[j]);
							Debug.Log("    Error: " + result.foundTypes[j].ErrorMessage);
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
								Debug.Log("Found Fields (with error)");
							}

							Debug.Log("  " + result.foundFields[j]);
							Debug.Log("    Error: " + result.foundFields[j].ErrorMessage);
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
								Debug.Log("Found Methods (with error)");
							}

							Debug.Log("  " + result.foundMethods[j]);
							Debug.Log("    Error: " + result.foundMethods[j].ErrorMessage);
						}
					}
				}
			}
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