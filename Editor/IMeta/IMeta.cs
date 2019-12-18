using System.IO;

namespace NGUnityVersioner
{
	public interface IMeta
	{
		string	Name { get; }
		string	ErrorMessage { get; }

		void	Save(BinaryWriter writer);
	}
}