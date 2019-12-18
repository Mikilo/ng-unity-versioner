using System;
using System.IO;

namespace NGUnityVersioner
{
	public static class CSharpExtension
	{
		public static void	WriteInt24(this BinaryWriter writer, int value)
		{
			writer.Write((Byte)(value & 0xFF));
			writer.Write((Byte)((value >> 8) & 0xFF));
			writer.Write((Byte)((value >> 16) & 0xFF));
		}

		public static int	ReadInt24(this BinaryReader reader)
		{
			return reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadByte() << 16);
		}
	}
}