using System;
using System.Reflection;

namespace NGUnityVersioner
{
	[Serializable]
	public class Member
	{
		public MemberTypes	type;
		public string		name;
		public byte[]		versions; // Use byte for performance, the day it grows over 256, switch to mask.
	}
}