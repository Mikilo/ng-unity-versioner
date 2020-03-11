namespace NGUnityVersioner
{
	public interface ISharedTable
	{
		int				RegisterString(string content);
		string			FetchString(int index);

		int				RegisterAssembly(AssemblyMeta meta);
		AssemblyMeta	FetchAssembly(int index);

		int				RegisterType(TypeMeta meta);
		TypeMeta		FetchType(int index);

		int				RegisterEvent(EventMeta meta);
		EventMeta		FetchEvent(int index);

		int				RegisterField(FieldMeta meta);
		FieldMeta		FetchField(int index);

		int				RegisterProperty(PropertyMeta meta);
		PropertyMeta	FetchProperty(int index);

		int				RegisterMethod(MethodMeta meta);
		MethodMeta		FetchMethod(int index);
	}
}