﻿using BossFramework.BInterfaces;
using BossFramework.DB;
using FreeSql.DataAnnotations;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terraria;
using TrProtocol;
using TrProtocol.Packets;
using TShockAPI;
using static BossFramework.BModels.BEventArgs;

namespace BossFramework.BModels
{
    public partial class BPlayer : UserConfigBase<BPlayer>, ISendMsg
    {
        public static readonly BPlayer Default = new(new(255) { })
        {
            Id = -1
        };
        public BPlayer() { }
        public BPlayer(TSPlayer plr)
        {
            TsPlayer = plr;
            Init();
        }
        public override void Init()
        {
            TsPlayer ??= TShock.Players.FirstOrDefault(p => p?.Account?.ID == Id);

            IsCustomWeaponMode = false;
            IsChangingWeapon = false;
        }

        #region 变量
        public Stopwatch PingChecker { get; } = new();
        [Column(IsIgnore = true)]
        public int LastPing { get; internal set; } = -1;
        [Column(IsIgnore = true)]
        public long LastPingTime { get; internal set; } = -1;
        public bool WaitingPing { get; internal set; } = false;

        public TSPlayer TsPlayer { get; internal set; }
        public Player TrPlayer => TsPlayer?.TPlayer;
        public string Name => TsPlayer?.Name ?? "unknown";
        public byte Index => (byte)(TsPlayer?.Index ?? -1);
        public bool IsRealPlayer => TsPlayer?.RealPlayer ?? false;
        public float X => TsPlayer.X;
        public float Y => TsPlayer.Y;
        public int TileX => TsPlayer.TileX;
        public int TileY => TsPlayer.TileY;

        public BaseBWeapon[] Weapons { get; internal set; }
        public BRegion CurrentRegion { get; internal set; } = BRegion.Default;
        public ProjRedirectContext ProjContext => CurrentRegion?.ProjContext;
        /// <summary>
        /// 是否处于使用自定义武器的状态, 修改需使用 <see cref="BCore.BWeaponSystem.ChangeCustomWeaponMode(BPlayer, bool?)"/>
        /// </summary>
        [Column(IsIgnore = true)]
        public bool IsCustomWeaponMode { get; internal set; } = false;
        [Column(IsIgnore = true)]
        public bool IsChangingWeapon { get; internal set; } = false;
        public NetItem ItemInHand { get; internal set; } = new(0, 0, 0);

        public List<BWeaponRelesedProj> RelesedProjs { get; } = new();



        public (short slot, BSign sign)? WatchingSign { get; internal set; }
        public short LastWatchingSignIndex { get; internal set; } = -1;

        public (short slot, BChest chest)? WatchingChest { get; set; }
        public short LastSyncChestIndex { get; internal set; } = -1;

        #region 小游戏部分
        public long Point { get; set; }
        /// <summary>
        /// 正在玩的游戏, 通过 JoinGame 加入
        /// </summary>
        public MiniGameContext PlayingGame { get; internal set; }
        #endregion

        #endregion

        #region 常用方法
        public override string ToString() => $"{Name}";
        internal bool CheckSendPacket(Packet p)
            => BNet.PacketHandler.HandleSendData(new PacketEventArgs(this, p));
        /// <summary>
        /// 向玩家发送数据包
        /// </summary>
        /// <param name="p"></param>
        public void SendPacket(Packet p)
        {
            if (!CheckSendPacket(p))
                TsPlayer?.SendRawData(p.SerializePacket());
        }
        public void SendPackets<T>(IEnumerable<T> p) where T : Packet
        {
            var packets = new List<Packet>();
            p.ForEach(packet =>
            {
                if (!CheckSendPacket(packet))
                    packets.Add(packet);
            });
            TsPlayer?.SendRawData(packets.GetPacketsByteData());
        }
        public void SendRawData(byte[] data) => TsPlayer?.SendRawData(data);
        public void SendCombatMessage(string msg, Color color = default, bool randomPosition = true)
        {
            color = color == default ? Color.White : color;
            Random random = new();
            TsPlayer!.SendData(PacketTypes.CreateCombatTextExtended, msg, (int)color.PackedValue, TsPlayer.X + (randomPosition ? random.Next(-75, 75) : 0), TsPlayer.Y + (randomPosition ? random.Next(-50, 50) : 0));
        }
        public void SendCombatMessage(string msg, Point p, Color color = default)
        {
            color = color == default ? Color.White : color;
            TsPlayer!.SendData(PacketTypes.CreateCombatTextExtended, msg, (int)color.PackedValue, p.X, p.Y);
        }
        public void SendSuccessMsg(object text)
        {
            TsPlayer?.SendMsg(text, new Color(120, 194, 96));
        }

        public void SendInfoMsg(object text)
        {
            TsPlayer?.SendMsg(text, new Color(216, 212, 82));
        }
        public void SendErrorMsg(object text)
        {
            TsPlayer?.SendMsg(text, new Color(195, 83, 83));
        }
        public void SendMsg(object text, Color color = default)
        {
            TsPlayer?.SendMsg(text, color);
        }
        public void SendMultipleMatchError(IEnumerable<object> matches)
        {
            TsPlayer?.SendErrorMessage("More than one match found -- unable to decide which is correct: ");

            var lines = PaginationTools.BuildLinesFromTerms(matches.ToArray());
            lines.ForEach(TsPlayer!.SendInfoMessage);

            TsPlayer?.SendErrorMessage("Use \"my query\" for items with spaces.");
            TsPlayer?.SendErrorMessage("Use tsi:[number] or tsn:[username] to distinguish between user IDs and usernames.");
        }
        public SyncEquipment RemoveItemPacket(int slot, bool clearServerSideItem = true)
            => new()
            {
                ItemType = 0,
                Prefix = 0,
                Stack = 0,
                ItemSlot = (short)slot,
                PlayerSlot = Index
            };
        public void RemoveItem(int slot, bool clearServerSideItem = true)
        {
            if (clearServerSideItem)
                TrPlayer.inventory[slot]?.SetDefaults();
            SendPacket(RemoveItemPacket(slot));
        }

        public bool GivePoint(int num, string from = "未知")
            => BCore.BPointSystem.ChangePoint(Index, num, from);
        public bool TakePoint(int num, string from = "未知")
            => BCore.BPointSystem.ChangePoint(Index, -num, from);
        #region 玩家状态
        /// <summary>
        /// 增加或减少玩家魔法值, 如果不足的话返回 false
        /// </summary>
        /// <param name="value">减少为 - </param>
        public bool ChangeMana(int value)
        {
            if (IsRealPlayer)
                return false;
            if (TsPlayer.TPlayer.statMana + value < 0)
                return false;
            TsPlayer.TPlayer.statMana += value;
            SendPacket(new PlayerMana()
            {
                PlayerSlot = Index,
                StatMana = (short)TsPlayer.TPlayer.statMana,
                StatManaMax = (short)TsPlayer.TPlayer.statLifeMax2
            });
            BUtils.SendPacketToAll(new ManaEffect()
            {
                PlayerSlot = Index,
                Amount = (short)(value < 0 ? -value : value)
            });
            return true;
        }

        #endregion
        #endregion
    }
}
