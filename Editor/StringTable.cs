using System.Collections.Generic;
using System.IO;

namespace NGUnityVersioner
{
	public class StringTable
	{
		public int	Count { get { return this.rawStringTable.Count; } }

		/// <summary>Contains string hash to index in rawStringTable.</summary>
		private Dictionary<int, int>	stringTable = new Dictionary<int, int>();
		private List<string>			rawStringTable = new List<string>();

		public	StringTable()
		{
		}

		public	StringTable(BinaryReader reader)
		{
			int	length = reader.ReadInt32();

			this.rawStringTable.Capacity = length;
			for (int i = 0, max = length; i < max; ++i)
			{
				this.rawStringTable.Add(reader.ReadString());
				this.stringTable.Add(this.rawStringTable[i].GetHashCode(), i);
			}
		}

		public void	Save(BinaryWriter writer)
		{
			writer.Write(this.rawStringTable.Count);
			for (int i = 0, max = this.rawStringTable.Count; i < max; ++i)
				writer.Write(this.rawStringTable[i]);
		}

		public int	RegisterString(string content)
		{
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
			if (index == 0)
				return null;
			return this.rawStringTable[index];
		}
	}
}