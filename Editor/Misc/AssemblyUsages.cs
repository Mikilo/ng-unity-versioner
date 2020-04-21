using Mono.Cecil;
using NGToolsStandalone_For_NGUnityVersionerEditor;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace NGUnityVersioner
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

	[Serializable]
	public class AssemblyUsages
	{
		public static DebugItems	debug = DebugItems.None;
		public static bool			useMultithreading = true;

		private static AssemblyUsages			usages = null;
		private static int						lastUsagesHash = 0;

		[SerializeField]
		private string[]	assemblies;
		public string[]		Assemblies { get { return this.assemblies; } }
		[SerializeField]
		private FilterText[]	filterNamespaces;
		public FilterText[]		FilterNamespaces { get { return this.filterNamespaces; } }
		[SerializeField]
		private string[]	targetNamespaces;
		public string[]		TargetNamespaces { get { return this.targetNamespaces; } }

		// Events & properties are not "used" directly, they are replaced with fields & methods in IL.
		public List<TypeReference>		visibleTypes = new List<TypeReference>();
		public HashSet<TypeReference>	types = new HashSet<TypeReference>();
		public List<FieldReference>		visibleFields = new List<FieldReference>();
		public HashSet<FieldReference>	fields = new HashSet<FieldReference>();
		public List<MethodReference>	visibleMethods = new List<MethodReference>();
		public HashSet<MethodReference>	methods = new HashSet<MethodReference>();

		private Dictionary<TypeReference, bool>	typeVisibilityCache = new Dictionary<TypeReference, bool>();
		private TypeReference					lastTypeRef;
		private bool							lastTypeRefVisibility;

		public static AssemblyUsagesResult[]	CheckCompatibilities(IEnumerable<string> assembliesPath, FilterText[] filterNamespaces, string[] targetNamespaces, IEnumerable<string> unityVersions)
		{
			if (targetNamespaces.Length == 0)
			{
				Debug.LogWarning("You must target at least one namespace.");
				return null;
			}

			int	hash = 0;

			hash += assembliesPath.GetHashCode();

			for (int i = 0, max = filterNamespaces.Length; i < max; ++i)
			{
				FilterText	filter = filterNamespaces[i];

				if (filter.active == true)
					hash += filter.text.GetHashCode() + filter.active.GetHashCode();
			}

			for (int i = 0, max = targetNamespaces.Length; i < max; ++i)
				hash += targetNamespaces[i].GetHashCode();

			if (AssemblyUsages.usages == null || AssemblyUsages.lastUsagesHash != hash)
			{
				AssemblyUsages.lastUsagesHash = hash;

				using ((AssemblyUsages.debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Extracted usages from target assemblies"))
				{
					AssemblyUsages.usages = AssemblyUsages.InspectAssembly(assembliesPath, filterNamespaces, targetNamespaces);
				}
			}

			List<AssemblyUsagesResult>	results = new List<AssemblyUsagesResult>();
			int							processorCount = Environment.ProcessorCount;
			Exception					threadException = null;
			DatabaseMeta				database = DatabaseMeta.GetDatabase();

			using ((AssemblyUsages.debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Resolving all"))
			{
				foreach (string unityVersion in unityVersions)
				{
					try
					{
						UnityMeta	unityMeta = database.Get(unityVersion);

						if (unityMeta != null)
						{
							AssemblyUsagesResult	result = new AssemblyUsagesResult() { assemblyUsages = usages, unityMeta = unityMeta };

							using ((AssemblyUsages.debug & DebugItems.WatchTime) == 0 ? null : WatchTime.Get("Resolved against " + unityMeta.Version))
							{
								result.ResolveReferences(AssemblyUsages.usages.visibleTypes, AssemblyUsages.usages.visibleFields, AssemblyUsages.usages.visibleMethods);
							}

							lock (results)
							{
								results.Add(result);
							}
						}
					}
					catch (Exception ex)
					{
						Debug.LogException(ex);
						threadException = ex;
					}
				}
			}

			if (threadException != null)
				throw threadException;

			results.Sort(Utility.CompareVersion);

			return results.ToArray();
		}

		public static AssemblyUsages	InspectAssembly(IEnumerable<string> assembliesPath, FilterText[] filterNamespaces, string[] targetNamespaces)
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

			if (this.types.Contains(typeRef) == false)
			{
				this.types.Add(typeRef);

				if (this.IsTypeVisible(typeRef) == true)
					this.visibleTypes.Add(typeRef);

				return true;
			}

			return false;
		}

		public bool	RegisterFieldRef(FieldReference fieldRef)
		{
			if (this.fields.Contains(fieldRef) == false)
			{
				this.fields.Add(fieldRef);

				if (this.IsFieldVisible(fieldRef) == true)
				{
					this.RegisterTypeRef(fieldRef.DeclaringType);
					this.RegisterTypeRef(fieldRef.FieldType);
					this.visibleFields.Add(fieldRef);
				}

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
				this.methods.Add(methodRef);

				if (this.IsMethodVisible(methodRef) == true)
				{
					this.RegisterTypeRef(methodRef.DeclaringType);
					this.RegisterTypeRef(methodRef.ReturnType);
					this.visibleMethods.Add(methodRef);
				}

				return true;
			}

			return false;
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

		private bool	IsTargetted(TypeReference typeRef)
		{
			string	@namespace = typeRef.Namespace;

			for (int i = 0, max = this.targetNamespaces.Length; i < max; ++i)
			{
				if (@namespace.StartsWith(this.targetNamespaces[i]) == true)
					return true;
			}

			return false;
		}

		private bool	IsTypeVisible(TypeReference typeRef)
		{
			if (this.lastTypeRef == typeRef)
				return this.lastTypeRefVisibility;

			bool	visibility;
			if (this.typeVisibilityCache.TryGetValue(typeRef, out visibility) == true)
				return visibility;

			this.lastTypeRef = typeRef;

			if (typeRef.Name[0] == '<') // Generated by compiler.
			{
				this.typeVisibilityCache.Add(typeRef, false);
				this.lastTypeRefVisibility = false;
				return false;
			}

			if (this.IsTargetted(typeRef) == false)
			{
				this.typeVisibilityCache.Add(typeRef, false);
				this.lastTypeRefVisibility = false;
				return false;
			}

			TypeDefinition	typeDef = typeRef as TypeDefinition;

			if (typeDef != null &&
				this.IsCompiledGenerated(typeDef) == true)
			{
				this.typeVisibilityCache.Add(typeRef, false);
				this.lastTypeRefVisibility = false;
				return false;
			}

			this.typeVisibilityCache.Add(typeRef, true);
			this.lastTypeRefVisibility = true;
			return true;
		}

		private bool	IsFieldVisible(FieldReference field)
		{
			if (field.Name[0] == '<')
				return false;

			if (this.IsTypeVisible(field.DeclaringType) == false)
				return false;

			FieldDefinition	fieldDef = field as FieldDefinition;

			if (fieldDef != null && this.IsCompiledGenerated(fieldDef) == true)
				return false;

			return true;
		}

		private bool	IsMethodVisible(MethodReference method)
		{
			if (this.IsTypeVisible(method.DeclaringType) == false)
				return false;

			MethodDefinition	methodDef = method as MethodDefinition;

			if (methodDef != null &&
				(this.IsCompiledGenerated(methodDef) == true ||
				 (methodDef.IsAddOn == false && methodDef.IsRemoveOn == false && methodDef.IsGetter == false && methodDef.IsSetter == false)))
			{
				return false;
			}

			return true;
		}
	}
}