using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace NGUnityVersioner
{
	[Serializable]
	public class UnityMeta
	{
		[SerializeField]
		private string	version;
		public string	Version { get { return this.version; } }

		[SerializeField]
		private AssemblyMeta[]	assembliesMeta;
		public AssemblyMeta[]	AssembliesMeta { get { return this.assembliesMeta; } }

		public static UnityMeta	Load(string filepath)
		{
			using (FileStream fileStream = File.Open(filepath, FileMode.Open, FileAccess.Read))
			using (GZipStream decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress))
			using (MemoryStream decompressedStringTableStream = new MemoryStream(1 << 20))
			{
				decompressionStream.CopyTo(decompressedStringTableStream);
				decompressedStringTableStream.Seek(0, SeekOrigin.Begin);

				using (BinaryReader reader = new BinaryReader(decompressedStringTableStream))
				{
					StringTable	sharedStringTable;
					string		unityVersion = reader.ReadString();

					sharedStringTable = new StringTable(reader);

					List<AssemblyMeta>	result = new List<AssemblyMeta>();

					while (reader.BaseStream.Position != reader.BaseStream.Length)
						result.Add(new AssemblyMeta(reader, sharedStringTable));

					return new UnityMeta(unityVersion, result.ToArray());
				}
			}
		}

		public	UnityMeta()
		{
			this.version = "ERROR";
			this.assembliesMeta = new AssemblyMeta[0];
		}

		public	UnityMeta(string version, AssemblyMeta[] assembliesMeta)
		{
			this.version = version;
			this.assembliesMeta = assembliesMeta;
		}

		public void	Save(string filepath)
		{
			using (MemoryStream assembliesStream = new MemoryStream(1 << 20)) // 1MB
			using (BinaryWriter assembliesWriter = new BinaryWriter(assembliesStream))
			{
				StringTable	sharedStringTable = new StringTable();

				// Write assemblies into the buffer to populate the string table.
				for (int i = 0, max = this.AssembliesMeta.Length; i < max; ++i)
					this.AssembliesMeta[i].Save(assembliesWriter, sharedStringTable);

				using (BinaryWriter finalWriter = new BinaryWriter(File.Open(filepath, FileMode.Create, FileAccess.Write)))
				using (MemoryStream stringTableStream = new MemoryStream(1 << 20))
				using (BinaryWriter stringTableWriter = new BinaryWriter(stringTableStream))
				{
					stringTableWriter.Write(this.Version);

					sharedStringTable.Save(stringTableWriter);

					using (MemoryStream GZipStringTableStream = new MemoryStream(1 << 20))
					using (GZipStream compressionStream = new GZipStream(finalWriter.BaseStream, CompressionMode.Compress, true))
					{
						stringTableStream.WriteTo(compressionStream);
						assembliesStream.WriteTo(compressionStream);
					}
				}
			}
		}
	}
}