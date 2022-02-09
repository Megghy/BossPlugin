﻿using BossFramework.BInterfaces;
using BossFramework.BModels;
using System;
using TrProtocol.Packets;

public class testweapon : BaseBWeapon
{
    public override string Name => "test";
    public override int ItemID => 757;
    public override int Prefix => 0;
    public override int Stack => 1;

    public override int? Damage => 1000;

    public long lastUse = 0;
    public override void OnUseItem(BPlayer plr, long gameTime)
    {
        if (gameTime - lastUse > 60) //距离上一次使用超过1秒
        {
            CreateProj(plr, 168, plr.TrPlayer.position, new Microsoft.Xna.Framework.Vector2(1, 1));
            plr.SendInfoEX("思考");
            lastUse = gameTime;
        }
    }
    public override bool OnShootProj(BPlayer plr, SyncProjectile proj, Microsoft.Xna.Framework.Vector2 velocity, bool isDefaultProj)
    {
        if (isDefaultProj)
        {
            CreateProj(plr, 168, plr.TrPlayer.position, velocity, 1);
            return true;
        }
        return false;
    }
    public override void OnHit(BPlayer from, BPlayer target, int damage, byte direction, byte coolDown)
    {
        Console.WriteLine($"{Name} - {from} 击中 {target}, {damage}, {direction}");
    }
    public override void OnProjHit(BPlayer from, BPlayer target, SyncProjectile proj, int damage, byte direction, byte coolDown)
    {
        Console.WriteLine($"{Name} - {from} 弹幕击中 {target}");
    }
}
