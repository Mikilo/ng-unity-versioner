using System;
using System.Reflection;

namespace NGUnityVersioner
{
	[Serializable]
	public class Type
	{
		public bool		isPublic;
		public string	name;
		public byte[]	versions; // Use byte for performance, the day it grows over 256, switch to mask.
		public Member[]	members;

		public void	Aggregate(MemberTypes memberType, string memberName, int version)
		{
			for (int i = 0, max = this.members.Length; i < max; ++i)
			{
				Member	member = this.members[i];

				if (member.name == memberName)
				{
					int	j = 0;
					int	max2 = member.versions.Length;

					for (; j < max2; ++j)
					{
						if (member.versions[j] == version)
							break;
					}

					if (j == max2)
					{
						Array.Resize(ref member.versions, member.versions.Length + 1);
						member.versions[member.versions.Length - 1] = (byte)version;
					}

					return;
				}
			}

			Array.Resize(ref this.members, this.members.Length + 1);
			this.members[this.members.Length - 1] = new Member()
			{
				type = memberType,
				name = memberName,
				versions = new byte[] { (byte)version }
			};
		}
	}
}