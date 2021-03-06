﻿using Mono.Cecil;
using NGToolsStandalone_For_NGUnityVersioner;
using NGToolsStandalone_For_NGUnityVersionerEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NGUnityVersioner
{
	[Serializable]
	public class AssemblyUsagesResult
	{
		public AssemblyUsages	assemblyUsages;
		public UnityMeta		unityMeta;
		public List<TypeMeta>	missingTypes = new List<TypeMeta>();
		public List<FieldMeta>	missingFields = new List<FieldMeta>();
		public List<MethodMeta>	missingMethods = new List<MethodMeta>();
		public List<TypeMeta>	foundTypes = new List<TypeMeta>();
		public List<FieldMeta>	foundFields = new List<FieldMeta>();
		public List<MethodMeta>	foundMethods = new List<MethodMeta>();

		public void	ResolveReferences(ICollection<TypeReference> types, ICollection<FieldReference> fields, ICollection<MethodReference> methods)
		{
			int									assembliesMetaLength = this.unityMeta.AssembliesMeta.Length;
			DynamicOrderedArray<AssemblyMeta>	assemblies = new DynamicOrderedArray<AssemblyMeta>(this.unityMeta.AssembliesMeta);

			foreach (TypeReference typeRef in types)
			{
				TypeMeta	meta = null;
				int			j = 0;

				for (; j < assembliesMetaLength && meta == null; ++j)
					meta = assemblies.array[j].Resolve(typeRef);

				if (meta != null)
				{
					assemblies.BringToTop(j - 1);

					if (meta.ErrorMessage != null)
						this.foundTypes.Add(meta);
				}
				else
				{
					// Type not found, maybe look into other types. Might be renamed.
					TypeMeta	lastFound = null;
					string		typeRefNamespace = typeRef.Namespace;
					string		typeRefName = typeRef.Name;

					j = 0;

					for (; j < assembliesMetaLength && lastFound == null; ++j)
					{
						for (int k = 0, max = assemblies.array[j].Types.Length; k < max; ++k)
						{
							TypeMeta	typeMeta = assemblies.array[j].Types[k];

							if (typeMeta.Name == typeRefName)
							{
								if (lastFound == null || this.GetLevenshteinDistance(lastFound.Namespace, typeRefNamespace) > this.GetLevenshteinDistance(typeMeta.Namespace, typeRefNamespace))
								{
									lastFound = typeMeta;
									break;
								}
							}
						}
					}

					if (lastFound != null)
						this.missingTypes.Add(new TypeMeta(typeRef, "Type not found, but a similar Type has been found at \"" + lastFound.FullName + "\"."));
					else
						this.missingTypes.Add(new TypeMeta(typeRef));
				}
			}

			foreach (FieldReference fieldRef in fields)
			{
				FieldMeta	meta = null;
				int			j = 0;

				for (; j < assembliesMetaLength && meta == null; ++j)
					meta = assemblies.array[j].Resolve(fieldRef);

				if (meta != null)
				{
					assemblies.BringToTop(j - 1);

					if (meta.ErrorMessage != null)
						this.foundFields.Add(meta);
				}
				else
					this.missingFields.Add(new FieldMeta(fieldRef));
			}

			foreach (MethodReference methodRef in methods)
			{
				MethodMeta	meta = null;
				int			j = 0;

				for (; j < assembliesMetaLength && meta == null; ++j)
					meta = assemblies.array[j].Resolve(methodRef);

				if (meta != null)
				{
					assemblies.BringToTop(j - 1);

					if (meta.ErrorMessage != null)
						this.foundMethods.Add(meta);
				}
				else
					this.missingMethods.Add(new MethodMeta(methodRef));
			}
		}

		public void	Export(StringBuilder buffer)
		{
			int	missingRefsCount = this.missingTypes.Count + this.missingFields.Count + this.missingMethods.Count;
			int	warningsCount = this.foundTypes.Count + this.foundFields.Count + this.foundMethods.Count;

			buffer.Append("Unity : ");
			buffer.AppendLine(Utility.GetUnityVersion(this.unityMeta.Version));
			buffer.Append("Inspected : ");

			for (int j = 0, max2 = this.assemblyUsages.Assemblies.Length; j < max2; ++j)
			{
				if (j > 0)
				{
					buffer.Append("            ");
				}
					buffer.AppendLine(this.assemblyUsages.Assemblies[j]);
			}

			buffer.Append("Filtered in namespaces : ");

			for (int j = 0, max2 = this.assemblyUsages.FilterNamespaces.Length; j < max2; ++j)
			{
				FilterText	filter = this.assemblyUsages.FilterNamespaces[j];

				if (j > 0)
					buffer.Append(", ");

				if (filter.type == Filter.Type.Inclusive)
					buffer.Append('+');
				else
					buffer.Append('-');

				buffer.Append(Path.GetFileNameWithoutExtension(filter.text));
			}
			buffer.AppendLine();

			buffer.Append("Targeted namespaces : ");

			for (int j = 0, max2 = this.assemblyUsages.TargetNamespaces.Length; j < max2; ++j)
			{
				if (j > 0)
					buffer.Append(", ");
				buffer.Append(Path.GetFileNameWithoutExtension(this.assemblyUsages.TargetNamespaces[j]));
			}

			buffer.AppendLine();

			if (missingRefsCount + warningsCount > 0)
			{
				if (missingRefsCount + warningsCount == 1)
					buffer.AppendLine((missingRefsCount + warningsCount) + " anomaly detected");
				else
					buffer.AppendLine((missingRefsCount + warningsCount) + " anomalies detected");

				if (this.missingTypes.Count > 0)
				{
					buffer.AppendLine("  Missing Types (" + this.missingTypes.Count + ")");
					for (int j = 0, max2 = this.missingTypes.Count; j < max2; ++j)
					{
						buffer.Append("    ");
						buffer.AppendLine(this.missingTypes[j].ToString());

						if (this.missingTypes[j].ErrorMessage != null)
						{
							buffer.Append("      ");
							buffer.AppendLine(this.missingTypes[j].ErrorMessage);
						}
					}
				}

				if (this.missingFields.Count > 0)
				{
					buffer.AppendLine("  Missing Fields (" + this.missingFields.Count + ")");

					for (int j = 0, max2 = this.missingFields.Count; j < max2; ++j)
					{
						buffer.Append("    ");
						buffer.AppendLine(this.missingFields[j].ToString());

						if (this.missingFields[j].ErrorMessage != null)
						{
							buffer.Append("      ");
							buffer.AppendLine(this.missingFields[j].ErrorMessage);
						}
					}
				}

				if (this.missingMethods.Count > 0)
				{
					buffer.AppendLine("  Missing Methods (" + this.missingMethods.Count + ")");

					for (int j = 0, max2 = this.missingMethods.Count; j < max2; ++j)
					{
						buffer.Append("    ");
						buffer.AppendLine(this.missingMethods[j].ToString());

						if (this.missingMethods[j].ErrorMessage != null)
						{
							buffer.Append("      ");
							buffer.AppendLine(this.missingMethods[j].ErrorMessage);
						}
					}
				}
			}

			if (this.foundTypes.Count > 0)
			{
				buffer.AppendLine("  Found Types with error (" + this.foundTypes.Count + ")");

				for (int j = 0, max2 = this.foundTypes.Count; j < max2; ++j)
				{
					buffer.Append("    ");
					buffer.AppendLine(this.foundTypes[j].ToString());
					buffer.Append("      ");
					buffer.AppendLine(this.foundTypes[j].ErrorMessage);
				}
			}

			if (this.foundFields.Count > 0)
			{
				buffer.AppendLine("  Found Fields with error (" + this.foundFields.Count + ")");

				for (int j = 0, max2 = this.foundFields.Count; j < max2; ++j)
				{
					buffer.Append("    ");
					buffer.AppendLine(this.foundFields[j].ToString());
					buffer.Append("      ");
					buffer.AppendLine(this.foundFields[j].ErrorMessage);
				}
			}

			if (this.foundMethods.Count > 0)
			{
				buffer.AppendLine("  Found Methods with error (" + this.foundMethods.Count + ")");

				for (int j = 0, max2 = this.foundMethods.Count; j < max2; ++j)
				{
					buffer.Append("    ");
					buffer.AppendLine(this.foundMethods[j].ToString());
					buffer.Append("      ");
					buffer.AppendLine(this.foundMethods[j].ErrorMessage);
				}
			}

			buffer.Length -= Environment.NewLine.Length;
		}

		// Thanks Sam Allen @ https://www.dotnetperls.com/levenshtein
		private int	GetLevenshteinDistance(string s, string t)
		{
			int		n = s.Length;
			int		m = t.Length;
			int[,]	d = new int[n + 1, m + 1];

			// Step 1
			if (n == 0)
				return m;

			if (m == 0)
				return n;

			// Step 2
			for (int i = 0; i <= n; d[i, 0] = i++);

			for (int j = 0; j <= m; d[0, j] = j++);

			// Step 3
			for (int i = 1; i <= n; i++)
			{
				//Step 4
				for (int j = 1; j <= m; j++)
				{
					// Step 5
					int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

					// Step 6
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost);
				}
			}
			// Step 7
			return d[n, m];
		}
	}
}