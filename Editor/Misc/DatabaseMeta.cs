using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using UnityEngine;

namespace NGUnityVersioner
{
	public class DatabaseMeta
	{
		public const string	MetaExtension = "metadll";
		public const string	DatabaseFilename = "Database." + DatabaseMeta.MetaExtension;

		private static string	databasePath;
		public static string	DatabasePath
		{
			get
			{
				return DatabaseMeta.databasePath;
			}
			set
			{
				if (DatabaseMeta.databasePath != value)
				{
					DatabaseMeta.databasePath = value;
					DatabaseMeta.database = null;
				}
			}
		}

		private static DatabaseMeta	database;

		public bool	IsReady { get { return this.constructingThread == null || this.databaseStream != null; } }

		private UnityMeta[]	unityMeta;
		public UnityMeta[]	UnityMeta { get { return this.unityMeta; } }
		private string[]	unityVersions;
		public string[]		UnityVersions { get { return this.unityVersions; } }

		//private string			path;
		private long			sharedTableOffset;
		private long[]			metaOffsets;
		private Thread			constructingThread;
		private MemoryStream	databaseStream;
		private SharedTable		sharedTable;

		public static DatabaseMeta	GetDatabase()
		{
			if (DatabaseMeta.database == null)
			{
				try
				{
					if (File.Exists(DatabaseMeta.DatabasePath) == true)
						DatabaseMeta.database = new DatabaseMeta(DatabaseMeta.DatabasePath);
				}
				catch (Exception ex)
				{
					Debug.LogException(ex);
				}
				finally
				{
					if (DatabaseMeta.database == null)
						DatabaseMeta.database = new DatabaseMeta();
				}
			}

			return DatabaseMeta.database;
		}

		public	DatabaseMeta()
		{
			this.unityMeta = new UnityMeta[0];
			this.unityVersions = new string[0];
		}

		public	DatabaseMeta(UnityMeta[] unityMeta)
		{
			this.unityMeta = unityMeta;
			this.unityVersions = new string[0];
		}

		public	DatabaseMeta(string filepath)
		{
			FileStream		fileStream = File.Open(filepath, FileMode.Open, FileAccess.Read);
			BinaryReader	fileReader = new BinaryReader(fileStream, Encoding.UTF8);

			this.unityVersions = new string[fileReader.ReadInt32()];
			this.metaOffsets = new long[this.unityVersions.Length];

			for (int i = 0, max = this.unityVersions.Length; i < max; ++i)
			{
				this.metaOffsets[i] = fileReader.ReadInt64();
				this.unityVersions[i] = fileReader.ReadString();
			}

			this.sharedTableOffset = fileReader.BaseStream.Position;
			this.unityMeta = new UnityMeta[0];

			this.constructingThread = new Thread(new ParameterizedThreadStart(this.ConstructDatabase))
			{
				Name = "Construct database"
			};
			this.constructingThread.Start(fileStream);
		}

		/// <summary>Extracts all Unity meta from memory, guaranteeing all versions are loaded.</summary>
		public void	ExtractAll()
		{
			for (int i = 0, max = this.unityVersions.Length; i < max; ++i)
				this.Get(this.unityVersions[i]);
		}

		public UnityMeta	Get(string version)
		{
			for (int i = 0, max = this.unityMeta.Length; i < max; ++i)
			{
				UnityMeta	meta = this.unityMeta[i];

				if (meta.Version == version)
					return meta;
			}

			lock (this)
			{
				while (this.constructingThread != null); // Wait for constructor to finish.

				if (this.databaseStream != null)
				{
					BinaryReader	reader = new BinaryReader(this.databaseStream, Encoding.UTF8);

					for (int i = 0, max = this.unityVersions.Length; i < max; ++i)
					{
						if (this.unityVersions[i] == version)
						{
							reader.BaseStream.Position = this.metaOffsets[i];

							UnityMeta		meta = new UnityMeta(reader, this.sharedTable);
							List<UnityMeta>	result = new List<UnityMeta>(this.unityMeta);

							result.Add(meta);
							result.Sort((a, b) => Utility.CompareVersion(a.Version, b.Version));

							this.unityMeta = result.ToArray();

							return meta;
						}
					}
				}
			}

			return null;
		}

		public void	Add(UnityMeta unityMeta)
		{
			Array.Resize(ref this.unityMeta, this.unityMeta.Length + 1);
			this.unityMeta[this.unityMeta.Length - 1] = unityMeta;

			List<string>	unityVersions = new List<string>(this.unityMeta.Length);

			for (int i = 0, max = this.unityMeta.Length; i < max; ++i)
				unityVersions.Add(this.unityMeta[i].Version);

			unityVersions.Sort(Utility.CompareVersion);

			this.unityVersions = unityVersions.ToArray();
		}

		/// <summary>Saves the current state of the database to <paramref name="filepath"/>.</summary>
		/// <param name="filepath"></param>
		public void	Save(string filepath)
		{
			using (MemoryStream assembliesStream = new MemoryStream(1 << 27)) // 128MB
			using (BinaryWriter assembliesWriter = new BinaryWriter(assembliesStream))
			{
				SharedTable		sharedTable = new SharedTable();
				List<UnityMeta>	sortedMeta = new List<UnityMeta>(this.unityMeta);
				long[]			metaOffsets = new long[this.unityMeta.Length];

				sortedMeta.Sort((a, b) => Utility.CompareVersion(a.Version, b.Version));

				// Write assemblies into the buffer to populate the string table.
				for (int i = 0, max = sortedMeta.Count; i < max; ++i)
				{
					metaOffsets[i] = assembliesWriter.BaseStream.Position;
					sortedMeta[i].Save(assembliesWriter, sharedTable);
				}

				using (MemoryStream sharedTableStream = new MemoryStream(1 << 22)) // 4MB
				using (BinaryWriter sharedTableWriter = new BinaryWriter(sharedTableStream))
				{
					sharedTable.Save(sharedTableWriter);

					using (BinaryWriter finalWriter = new BinaryWriter(File.Open(filepath, FileMode.Create, FileAccess.Write)))
					using (GZipStream compressionStream = new GZipStream(finalWriter.BaseStream, CompressionMode.Compress, true))
					{
						finalWriter.Write(metaOffsets.Length);

						for (int i = 0, max = metaOffsets.Length; i < max; ++i)
						{
							finalWriter.Write(metaOffsets[i] + sharedTableStream.Length);
							finalWriter.Write(sortedMeta[i].Version);
						}

						sharedTableStream.WriteTo(compressionStream);
						assembliesStream.WriteTo(compressionStream);
					}
				}
			}
		}

		private void	ConstructDatabase(object baseStream)
		{
			try
			{
				using (FileStream fileStream = (FileStream)baseStream)
				using (GZipStream decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress))
				{
					MemoryStream	databaseStream = new MemoryStream(1 << 24); // 16MB

					fileStream.Position = this.sharedTableOffset;

					const int	BufferSize = 1 << 16; // 64kB
					byte[]		buffer = new byte[BufferSize];
					int			n = decompressionStream.Read(buffer, 0, BufferSize);

					while (n == BufferSize)
					{
						databaseStream.Write(buffer, 0, n);
						n = decompressionStream.Read(buffer, 0, BufferSize);
					}

					if (n > 0)
						databaseStream.Write(buffer, 0, n);
					databaseStream.Seek(0L, SeekOrigin.Begin);

					BinaryReader	reader = new BinaryReader(databaseStream, Encoding.UTF8);

					this.sharedTable = new SharedTable(reader);

					List<UnityMeta>	result = new List<UnityMeta>(this.metaOffsets.Length);

					for (int i = 0, max = this.metaOffsets.Length; i < max; ++i)
					{
						reader.BaseStream.Position = this.metaOffsets[i];
						result.Add(new UnityMeta(reader, this.sharedTable));
					}

					result.Sort((a, b) => Utility.CompareVersion(a.Version, b.Version));

					this.unityMeta = result.ToArray();
					this.databaseStream = databaseStream;
				}
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
			finally
			{
				this.constructingThread = null;
			}
		}
	}
}