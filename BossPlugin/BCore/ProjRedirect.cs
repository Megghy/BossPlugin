﻿using BossPlugin.BModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrProtocol.Packets;

namespace BossPlugin.BCore
{
    /// <summary>
    /// 弹幕重定向
    /// </summary>
    public static class ProjRedirect
    {
        public static bool OnProjCreate(BPlayer plr, SyncProjectile proj)
        {
            if (plr.Player.CurrentRegion is { } region)
            {
                BInfo.OnlinePlayers.Where(p => p != plr && p.Player.CurrentRegion == region)
                    .ForEach(p => p.SendPacket(proj));
                return true;
            }
            else
                return false;
        }
    }
}
