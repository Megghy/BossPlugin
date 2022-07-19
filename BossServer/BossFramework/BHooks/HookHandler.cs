﻿using BossFramework.BAttributes;
using TerrariaApi.Server;
using TShockAPI.Hooks;

namespace BossFramework.BHooks
{
    public static class HookHandler
    {
        [AutoInit("挂载所有Hook")]
        private static void HandleHooks()
        {
            OTAPI.Hooks.NetMessage.PlayerAnnounce += HookHandlers.AnnounceHandler.OnAnnounce;
            OTAPI.Hooks.Netplay.CreateTcpListener += HookHandlers.SocketCreateHandler.OnSocketCreate;
            GeneralHooks.ReloadEvent += HookHandlers.ReloadHandler.OnReload;
            AccountHooks.AccountCreate += HookHandlers.CreateAccountHandler.OnCreateAccount;

            ServerApi.Hooks.ServerConnect.Register(BossPlugin.Instance, HookHandlers.PlayerConnectHandler.OnConnect, int.MinValue);
            ServerApi.Hooks.NetGreetPlayer.Register(BossPlugin.Instance, HookHandlers.PlayerGreetHandler.OnGreetPlayer, int.MinValue);
            ServerApi.Hooks.ServerLeave.Register(BossPlugin.Instance, HookHandlers.PlayerLeaveHandler.OnPlayerLeave);
            ServerApi.Hooks.GamePostInitialize.Register(BossPlugin.Instance, HookHandlers.PostInitializeHandler.OnGamePostInitialize, int.MinValue);
            ServerApi.Hooks.GameUpdate.Register(BossPlugin.Instance, HookHandlers.GameUpdateHandler.OnGameUpdate);
            ServerApi.Hooks.NetGetData.Register(BossPlugin.Instance, BNet.PacketHandler.OnGetData, int.MaxValue);
            ServerApi.Hooks.NetSendBytes.Register(BossPlugin.Instance, BNet.PacketHandler.OnSendData, int.MaxValue);
        }
    }
}
