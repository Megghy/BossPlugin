﻿namespace TrProtocol.Packets
{
    public struct MenuSunMoon : IPacket
    {
        public MessageID Type => MessageID.MenuSunMoon;
        public bool DayTime { get; set; }
        public int Time { get; set; }
        public short Sun { get; set; }
        public short Moon { get; set; }
    }
}
