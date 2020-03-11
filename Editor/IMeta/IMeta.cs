using System.IO;

namespace NGUnityVersioner
{
	public interface IMeta : IMetaSignature
	{
		string	Name { get; }
		string	ErrorMessage { get; }

		void	Save(ISharedTable stringTable, BinaryWriter writer);
	}
}