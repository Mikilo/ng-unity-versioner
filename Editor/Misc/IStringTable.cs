namespace NGUnityVersioner
{
	public interface IStringTable
	{
		int		RegisterString(string content);
		string	FetchString(int index);
	}
}