using System.Collections.Generic;
using System.IO;

namespace NGUnityVersioner
{
	public class SharedTable : ISharedTable
	{
		private class Table<T> where T : IMetaSignature
		{
			internal List<T>				rawTable = new List<T>();
			/// <summary>Contains string hash to index in rawTable.</summary>
			private Dictionary<int, int>	table;

			public void	Init(int capacity)
			{
				this.rawTable.Clear();
				this.rawTable.Capacity = capacity;
			}

			public void	Add(T value)
			{
				this.rawTable.Add(value);
			}

			public int	Register(T field)
			{
				if (this.table == null)
				{
					this.table = new Dictionary<int, int>(this.rawTable.Capacity);

					for (int i = 0, max = this.rawTable.Count; i < max; ++i)
						this.table.Add(this.rawTable[i].GetSignatureHash(), i);
				}

				int	hash = field.GetSignatureHash();
				int	index;

				if (this.table.TryGetValue(hash, out index) == false)
				{
					index = this.table.Count;
					this.table.Add(hash, index);
					this.rawTable.Add(field);
				}

				return index;
			}
		}

		public int	Count { get { return this.rawStringTable.Count; } }

		/// <summary>Contains string hash to index in rawStringTable.</summary>
		private Dictionary<int, int>	stringTable;
		private List<string>			rawStringTable = new List<string>();

		private Table<AssemblyMeta>	assemblyTable = new Table<AssemblyMeta>();
		private Table<TypeMeta>		typeTable = new Table<TypeMeta>();
		private Table<EventMeta>	eventTable = new Table<EventMeta>();
		private Table<FieldMeta>	fieldTable = new Table<FieldMeta>();
		private Table<PropertyMeta>	propertyTable = new Table<PropertyMeta>();
		private Table<MethodMeta>	methodTable = new Table<MethodMeta>();

		public	SharedTable()
		{
			this.rawStringTable.Add(null);
		}

		public	SharedTable(BinaryReader reader)
		{
			int	length = reader.ReadInt32();
			this.rawStringTable.Capacity = length + 1;
			this.rawStringTable.Add(null);

			for (int i = 0, max = length; i < max; ++i)
				this.rawStringTable.Add(reader.ReadString());

			length = reader.ReadInt32();
			this.eventTable.Init(length);
			for (int i = 0, max = length; i < max; ++i)
				this.eventTable.Add(new EventMeta(this, null, reader));

			length = reader.ReadInt32();
			this.fieldTable.Init(length);
			for (int i = 0, max = length; i < max; ++i)
				this.fieldTable.Add(new FieldMeta(this, null, reader));

			length = reader.ReadInt32();
			this.propertyTable.Init(length);
			for (int i = 0, max = length; i < max; ++i)
				this.propertyTable.Add(new PropertyMeta(this, null, reader));

			length = reader.ReadInt32();
			this.methodTable.Init(length);
			for (int i = 0, max = length; i < max; ++i)
				this.methodTable.Add(new MethodMeta(this, null, reader));

			length = reader.ReadInt32();
			this.typeTable.Init(length);
			for (int i = 0, max = length; i < max; ++i)
				this.typeTable.Add(new TypeMeta(this, reader));

			length = reader.ReadInt32();
			this.assemblyTable.Init(length);
			for (int i = 0, max = length; i < max; ++i)
				this.assemblyTable.Add(new AssemblyMeta(this, reader));
		}

		public void	Save(BinaryWriter writer)
		{
			using (MemoryStream memberStream = new MemoryStream(1 << 22)) // 4MB
			using (BinaryWriter memberWriter = new BinaryWriter(memberStream))
			using (MemoryStream typeStream = new MemoryStream(1 << 22)) // 4MB
			using (BinaryWriter typeWriter = new BinaryWriter(typeStream))
			using (MemoryStream assemblyStream = new MemoryStream(1 << 21)) // 2MB
			using (BinaryWriter assemblyWriter = new BinaryWriter(assemblyStream))
			{
				assemblyWriter.Write(this.assemblyTable.rawTable.Count);
				for (int i = 0, max = this.assemblyTable.rawTable.Count; i < max; ++i)
					this.assemblyTable.rawTable[i].Save(this, assemblyWriter);

				this.WriteTable(typeWriter, this.typeTable);

				this.WriteTable(memberWriter, this.eventTable);

				this.WriteTable(memberWriter, this.fieldTable);

				this.WriteTable(memberWriter, this.propertyTable);

				this.WriteTable(memberWriter, this.methodTable);

				writer.Write(this.rawStringTable.Count - 1);
				for (int i = 1, max = this.rawStringTable.Count; i < max; ++i)
					writer.Write(this.rawStringTable[i]);

				memberStream.WriteTo(writer.BaseStream);
				typeStream.WriteTo(writer.BaseStream);
				assemblyStream.WriteTo(writer.BaseStream);
			}
		}

		private void	WriteTable<T>(BinaryWriter writer, Table<T> table) where T : IMeta
		{
			writer.Write(table.rawTable.Count);
			for (int i = 0, max = table.rawTable.Count; i < max; ++i)
				table.rawTable[i].Save(this, writer);
		}

		public int	RegisterString(string content)
		{
			if (this.stringTable == null)
			{
				this.stringTable = new Dictionary<int, int>(this.rawStringTable.Capacity + 1);
				this.stringTable.Add(0, 0);

				for (int i = 1, max = this.rawStringTable.Count; i < max; ++i)
					this.stringTable.Add(this.rawStringTable[i].GetHashCode(), i);
			}

			if (content == null)
				return 0;

			int	hash = content.GetHashCode();
			int	index;

			if (this.stringTable.TryGetValue(hash, out index) == false)
			{
				index = this.stringTable.Count;
				this.stringTable.Add(hash, index);
				this.rawStringTable.Add(content);
			}

			return index;
		}

		public string	FetchString(int index)
		{
			return this.rawStringTable[index];
		}

		public int	RegisterAssembly(AssemblyMeta meta)
		{
			return this.assemblyTable.Register(meta);
		}

		public AssemblyMeta	FetchAssembly(int index)
		{
			return this.assemblyTable.rawTable[index];
		}

		public int	RegisterType(TypeMeta meta)
		{
			return this.typeTable.Register(meta);
		}

		public TypeMeta	FetchType(int index)
		{
			return this.typeTable.rawTable[index];
		}

		public int	RegisterEvent(EventMeta meta)
		{
			return this.eventTable.Register(meta);
		}

		public EventMeta	FetchEvent(int index)
		{
			return this.eventTable.rawTable[index];
		}

		public int	RegisterField(FieldMeta meta)
		{
			return this.fieldTable.Register(meta);
		}

		public FieldMeta	FetchField(int index)
		{
			return this.fieldTable.rawTable[index];
		}

		public int	RegisterProperty(PropertyMeta meta)
		{
			return this.propertyTable.Register(meta);
		}

		public PropertyMeta	FetchProperty(int index)
		{
			return this.propertyTable.rawTable[index];
		}

		public int	RegisterMethod(MethodMeta meta)
		{
			return this.methodTable.Register(meta);
		}

		public MethodMeta	FetchMethod(int index)
		{
			return this.methodTable.rawTable[index];
		}
	}
}