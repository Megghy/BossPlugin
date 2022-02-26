﻿namespace TrProtocol.Packets
{
    public struct TEHatRackItemSync : IPacket, IPlayerSlot
    {
        public MessageID Type => MessageID.TEHatRackItemSync;
        public byte PlayerSlot { get; set; }
        public int TileEntityID { get; set; }
        public byte ItemIndex { get; set; }
        public ushort ItemID { get; set; }
        public ushort Stack { get; set; }
        public byte Prefix { get; set; }
    }
}