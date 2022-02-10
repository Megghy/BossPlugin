﻿using BossFramework.BInterfaces;
using BossFramework.BModels;
using TrProtocol.Packets;

namespace BossFramework.BNet.PacketHandlers
{
    public class PlayerUpdateHandler : PacketHandlerBase<PlayerControls>
    {
        public override bool OnGetPacket(BPlayer plr, PlayerControls packet)
        {
            return false;
        }

        public override bool OnSendPacket(BPlayer plr, PlayerControls packet)
        {
            return false;
        }
    }
}
