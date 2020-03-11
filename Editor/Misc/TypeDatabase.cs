using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NGUnityVersioner
{
	public class TypeDatabase
	{
		private string[]	versions;
		public string[]		Versions { get { return this.versions; } }

		private Type[]	types;

		public static string	GetDefaultDatabasePath()
		{
			return Path.Combine(Application.persistentDataPath, "typedatabase.bin");
		}

		public	TypeDatabase(string filepath = null)
		{
			if (filepath == null)
				filepath = TypeDatabase.GetDefaultDatabasePath();

			if (File.Exists(filepath) == false)
				throw new FileNotFoundException(filepath + " does not exist.");

			using (FileStream fileStream = File.Open(filepath, FileMode.Open, FileAccess.Read))
			using (BinaryReader reader = new BinaryReader(fileStream))
			{
				int	max = reader.ReadInt32();

				this.versions = new string[max];
				for (int i = 0; i < max; ++i)
					this.versions[i] = reader.ReadString();

				max = reader.ReadInt32();
				this.types = new Type[max];

				for (int i = 0; i < max; ++i)
				{
					Type	type = new Type()
					{
						isPublic = reader.ReadBoolean(),
						name = reader.ReadString(),
						versions = reader.ReadBytes(reader.ReadInt32()),
						members = new Member[reader.ReadInt32()]
					};

					this.types[i] = type;

					for (int j = 0, max2 = type.members.Length; j < max2; ++j)
					{
						type.members[j] = new Member()
						{
							type = (MemberTypes)reader.ReadByte(),
							name = reader.ReadString(),
							versions = reader.ReadBytes(reader.ReadInt32()),
						};
					}
				}
			}
		}

		public	TypeDatabase(DatabaseMeta database)
		{
			Dictionary<string, Type>	types = new Dictionary<string, Type>();

			for (int i = 0, max = database.UnityMeta.Length; i < max; ++i)
			{
				UnityMeta	unityMeta = database.UnityMeta[i];

				for (int j = 0, max2 = unityMeta.AssembliesMeta.Length; j < max2; ++j)
				{
					AssemblyMeta	assemblyMeta = unityMeta.AssembliesMeta[j];

					for (int k = 0, max3 = assemblyMeta.Types.Length; k < max3; ++k)
					{
						TypeMeta	typeMeta = assemblyMeta.Types[k];
						Type		type;

						if (types.TryGetValue(typeMeta.FullName, out type) == false)
						{
							type = new Type()
							{
								isPublic = typeMeta.IsPublic,
								name = typeMeta.FullName,
								members = new Member[0],
								versions = new byte[] { (byte)i }
							};
							types.Add(typeMeta.FullName, type);
						}
						else
						{
							int	l = 0;
							int	max4 = type.versions.Length;

							for (; l < max4; ++l)
							{
								if (type.versions[l] == i)
									break;
							}

							if (l == max4)
							{
								Array.Resize(ref type.versions, type.versions.Length + 1);
								type.versions[type.versions.Length - 1] = (byte)i;
							}
						}

						for (int l = 0, max4 = typeMeta.Events.Length; l < max4; ++l)
							type.Aggregate(MemberTypes.Event, typeMeta.Events[l].Name, i);

						for (int l = 0, max4 = typeMeta.Properties.Length; l < max4; ++l)
							type.Aggregate(MemberTypes.Property, typeMeta.Properties[l].Name, i);

						for (int l = 0, max4 = typeMeta.Fields.Length; l < max4; ++l)
							type.Aggregate(MemberTypes.Field, typeMeta.Fields[l].Name, i);

						for (int l = 0, max4 = typeMeta.Methods.Length; l < max4; ++l)
							type.Aggregate(MemberTypes.Method, typeMeta.Methods[l].Name, i);
					}
				}
			}

			this.versions = new string[database.UnityMeta.Length];
			for (int i = 0, max = database.UnityMeta.Length; i < max; ++i)
				this.versions[i] = database.UnityMeta[i].Version;

			this.types = new List<Type>(types.Values).ToArray();
		}

		public TypeResult	Scan(string typeInput, string memberInput)
		{
			return new TypeResult(typeInput, memberInput, this.versions, this.types);
		}

		public void	Save(string filepath = null)
		{
			if (filepath == null)
				filepath = TypeDatabase.GetDefaultDatabasePath();

			using (BinaryWriter assembliesWriter = new BinaryWriter(File.Open(filepath, FileMode.Create, FileAccess.Write)))
			{
				assembliesWriter.Write(this.versions.Length);
				for (int i = 0, max = this.versions.Length; i < max; ++i)
					assembliesWriter.Write(this.versions[i]);

				assembliesWriter.Write(this.types.Length);

				for (int i = 0, max = this.types.Length; i < max; ++i)
				{
					Type	type = this.types[i];

					assembliesWriter.Write(type.isPublic);
					assembliesWriter.Write(type.name);
					assembliesWriter.Write(type.versions.Length);
					for (int j = 0, max2 = type.versions.Length; j < max2; ++j)
						assembliesWriter.Write(type.versions[j]);

					assembliesWriter.Write(type.members.Length);
					for (int j = 0, max2 = type.members.Length; j < max2; ++j)
					{
						Member	member = type.members[j];

						assembliesWriter.Write((byte)member.type);
						assembliesWriter.Write(member.name);

						assembliesWriter.Write(member.versions.Length);
						for (int k = 0, max3 = member.versions.Length; k < max3; ++k)
							assembliesWriter.Write(member.versions[k]);
					}
				}
			}
		}
	}
}