using System.IO;
using YGOSharp.Network.Enums;

namespace Skynet.Game
{
    public class GamePacketFactory
    {
        public static BinaryWriter Create(CtosMessage message)
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            writer.Write((byte)message);
            return writer;
        }
    }
}
