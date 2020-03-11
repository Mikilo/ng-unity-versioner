using System;
using System.Collections.Generic;
using System.Text;

namespace NGUnityVersioner
{
	[Serializable]
	public class TypeResult
	{
		public const string	HighlightOcurrenceColor = "green";

		public string[]		versions;
		public bool			hasActiveType;
		public Type			activeType;
		public List<Member>	activeMember = new List<Member>();
		public bool[]		memberPresentInVersions;

		public List<Type>	similarTypes = new List<Type>();
		public string[]		similarTypesLabel;
		public List<Member>	similarMembers = new List<Member>();
		public string[]		similarMembersLabel;

		public string	typeSearched;
		public string	memberSearched;
		public string	typeFirstIntroducedPreprocessor;
		public string	typePresentInVersionsPreprocessor;
		public string	memberFirstIntroducedPreprocessor;
		public string	memberPresentInVersionsPreprocessor;

		public	TypeResult(string typeInput, string memberInput, string[] versions, Type[] types)
		{
			this.versions = versions;
			this.typeSearched = typeInput;
			this.memberSearched = memberInput;
			
			for (int i = 0, max = types.Length; i < max; ++i)
			{
				Type	type = types[i];

				if (string.Equals(type.name, typeInput, StringComparison.OrdinalIgnoreCase))
				{
					if (this.activeType == null)
					{
						this.hasActiveType = true;
						this.activeType = type;
						this.similarTypes.Clear();
						break;
					}
				}

				if (this.activeType == null &&
					type.name.IndexOf(typeInput, StringComparison.OrdinalIgnoreCase) != -1)
				{
					this.similarTypes.Add(type);
				}
			}

			if (this.similarTypes.Count == 1)
			{
				this.hasActiveType = true;
				this.activeType = this.similarTypes[0];
			}
			else
			{
				this.similarTypesLabel = new string[this.similarTypes.Count];

				for (int i = 0, max = this.similarTypes.Count; i < max; ++i)
				{
					string	name = this.similarTypes[i].name;
					int		n = name.IndexOf(this.typeSearched, StringComparison.OrdinalIgnoreCase);

					name = name.Insert(n + this.typeSearched.Length, "</color>");

					this.similarTypesLabel[i] = name.Insert(n, "<color=" + TypeResult.HighlightOcurrenceColor + ">");
				}
			}

			if (this.hasActiveType  == true)
			{
				this.memberPresentInVersions = new bool[this.activeType.versions.Length];

				if (string.IsNullOrEmpty(memberInput) == false)
				{
					for (int i = 0, max = this.activeType.members.Length; i < max; ++i)
					{
						Member	member = this.activeType.members[i];

						if (string.Equals(member.name, memberInput, StringComparison.OrdinalIgnoreCase) == true)
						{
							this.activeMember.Add(member);

							for (int k = 0, max2 = this.activeType.versions.Length; k < max2; ++k)
							{
								if (this.memberPresentInVersions[k] == true)
									continue;

								for (int j = 0, max3 = member.versions.Length; j < max3; ++j)
								{
									if (member.versions[j] == this.activeType.versions[k])
									{
										this.memberPresentInVersions[k] = true;
										break;
									}
								}
							}
						}
						else if (member.name.IndexOf(memberInput, StringComparison.OrdinalIgnoreCase) != -1)
						{
							if (this.similarMembers.Exists(m => m.name == member.name) == false)
								this.similarMembers.Add(member);
						}
					}

					this.similarMembers.Sort((a, b) => a.name.CompareTo(b.name));
					this.similarMembersLabel = new string[this.similarMembers.Count];

					for (int i = 0, max = this.similarMembers.Count; i < max; ++i)
					{
						string	name = this.similarMembers[i].name;
						int		n = name.IndexOf(this.memberSearched, StringComparison.OrdinalIgnoreCase);

						name = name.Insert(n + this.memberSearched.Length, "</color>");

						this.similarMembersLabel[i] = name.Insert(n, "<color=" + TypeResult.HighlightOcurrenceColor + ">");
					}
				}

				string[]	parts = this.versions[this.activeType.versions[this.activeType.versions.Length - 1]].Split('.');

				this.typeFirstIntroducedPreprocessor = "#if UNITY_" + parts[0] + '_' + parts[1] + '_' + Utility.ParseInt(parts[2]);

				StringBuilder	buffer = Utility.GetBuffer("#if ");
				string			lastMajor = null;
				int				lastMajorCount = 0;

				for (int i = this.activeType.versions.Length - 1; i > 0; i--)
				{
					parts = this.versions[this.activeType.versions[i]].Split('.');

					if (lastMajor == null || lastMajor != parts[0])
					{
						lastMajor = parts[0];
						lastMajorCount = this.CountMajors(parts[0]);

						if (lastMajorCount == this.CountMajors(this.activeType.versions, parts[0]))
						{
							string	globalDirective = "UNITY_" + parts[0] + ' ';

							if (buffer.ToString().IndexOf(globalDirective) == -1)
								buffer.Append(globalDirective + "|| ");
							continue;
						}
					}
					else if (lastMajorCount == this.CountMajors(this.activeType.versions, parts[0]))
						continue;

					string	directive = "UNITY_" + parts[0] + '_' + parts[1] + '_' + Utility.ParseInt(parts[2]);

					if (buffer.ToString().IndexOf(directive) == -1)
						buffer.Append(directive + " || ");
				}

				if (this.activeType.versions[0] == 0)
				{
					parts = this.versions[0].Split('.');
					buffer.Append("UNITY_" + parts[0] + '_' + parts[1] + "_OR_NEWER");
				}
				else
					buffer.Length -= 4;

				this.typePresentInVersionsPreprocessor = buffer.ToString();

				this.memberFirstIntroducedPreprocessor = null;
				this.memberPresentInVersionsPreprocessor = null;

				buffer.Length = 0;
				buffer.Append("#if ");

				string	lastDirective = null;

				lastMajor = null;

				if (this.memberPresentInVersions != null)
				{
					for (int j = this.memberPresentInVersions.Length - 1; j >= 0; j--)
					{
						if (this.memberPresentInVersions[j] == false)
							continue;

						string	version = this.versions[this.activeType.versions[j]];

						parts = version.Split('.');

						string	directive = "UNITY_" + parts[0] + '_' + parts[1] + '_' + Utility.ParseInt(parts[2]);

						if (this.memberFirstIntroducedPreprocessor == null)
							this.memberFirstIntroducedPreprocessor = "#if " + directive;

						lastDirective = directive;

						if (lastMajor == null || lastMajor != parts[0])
						{
							lastMajor = parts[0];
							lastMajorCount = this.CountMajors(parts[0]);

							if (lastMajorCount == this.CountMajors(this.activeType.versions, this.memberPresentInVersions, parts[0]))
							{
								string	globalDirective = "UNITY_" + parts[0] + ' ';

								if (buffer.ToString().IndexOf(globalDirective) == -1)
									buffer.Append(globalDirective + "|| ");
								continue;
							}
						}
						else if (lastMajorCount == this.CountMajors(this.activeType.versions, this.memberPresentInVersions, parts[0]))
							continue;

						if (buffer.ToString().IndexOf(directive) == -1)
							buffer.Append(directive + " || ");
					}
				}

				if (lastDirective != null)
				{
					if (this.versions[0] == this.versions[this.activeType.versions[0]])
					{
						parts = this.versions[0].Split('.');
						buffer.Append("UNITY_" + parts[0] + '_' + parts[1] + "_OR_NEWER");
					}
					else
						buffer.Length -= 4;

					this.memberPresentInVersionsPreprocessor = Utility.ReturnBuffer(buffer);
				}
			}
		}

		private int	CountMajors(string major)
		{
			int	count = 0;

			for (int i = 0, max = this.versions.Length; i < max; ++i)
			{
				string[]	parts = this.versions[i].Split('.');

				if (parts[0] == major)
					count++;
			}

			return count;
		}

		private int CountMajors(byte[] typeVersions, string major)
		{
			int count = 0;

			for (int j = 0, max = typeVersions.Length; j < max; ++j)
			{
				string version = this.versions[typeVersions[j]];

				string[] parts = version.Split('.');

				if (parts[0] == major)
					count++;
			}

			return count;
		}

		private int	CountMajors(byte[] typeVersions, bool[] presences, string major)
		{
			int	count = 0;

			for (int j = 0, max = typeVersions.Length; j < max; ++j)
			{
				if (presences[j] == false)
					continue;

				string	version = this.versions[typeVersions[j]];

				string[]	parts = version.Split('.');

				if (parts[0] == major)
					count++;
			}

			return count;
		}
	}
}