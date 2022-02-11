/*
TShock, a server mod for Terraria
Copyright (C) 2011-2019 Pryaxis & TShock Contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using MaxMind;
using Microsoft.Xna.Framework;
using MySqlConnector;
using Newtonsoft.Json;
using Rests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Utilities;
using TerrariaApi.Server;
using TShockAPI.CLI;
using TShockAPI.Configuration;
using TShockAPI.DB;
using TShockAPI.Hooks;
using TShockAPI.Localization;
using TShockAPI.Sockets;

namespace TShockAPI
{
    /// <summary>
    /// This is the TShock main class. TShock is a plugin on the TerrariaServerAPI, so it extends the base TerrariaPlugin.
    /// TShock also complies with the API versioning system, and defines its required API version here.
    /// </summary>
    [ApiVersion(2, 1)]
    public class TShock : TerrariaPlugin
    {
        /// <summary>VersionNum - The version number the TerrariaAPI will return back to the API. We just use the Assembly info.</summary>
        public static readonly Version VersionNum = Assembly.GetExecutingAssembly().GetName().Version;
        /// <summary>VersionCodename - The version codename is displayed when the server starts. Inspired by software codenames conventions.</summary>
        public static readonly string VersionCodename = "Herrscher of Logic";

        /// <summary>SavePath - This is the path TShock saves its data in. This path is relative to the TerrariaServer.exe (not in ServerPlugins).</summary>
        public static string SavePath = "tshock";
        /// <summary>LogFormatDefault - This is the default log file naming format. Actually, this is the only log format, because it never gets set again.</summary>
        private const string LogFormatDefault = "yyyy-MM-dd_HH-mm-ss";
        //TODO: Set the log path in the config file.
        /// <summary>LogFormat - This is the log format, which is never set again.</summary>
        private static string LogFormat = LogFormatDefault;
        /// <summary>LogPathDefault - The default log path.</summary>
        private const string LogPathDefault = "tshock/logs";
        /// <summary>This is the log path, which is initially set to the default log path, and then to the config file log path later.</summary>
        private static string LogPath = LogPathDefault;
        /// <summary>LogClear - Determines whether or not the log file should be cleared on initialization.</summary>
        private static bool LogClear;

        /// <summary>Will be set to true once Utils.StopServer() is called.</summary>
        public static bool ShuttingDown;

        /// <summary>Players - Contains all TSPlayer objects for accessing TSPlayers currently on the server</summary>
        public static TSPlayer[] Players = new TSPlayer[Main.maxPlayers];
        /// <summary>Bans - Static reference to the ban manager for accessing bans &amp; related functions.</summary>
        public static BanManager Bans;
        /// <summary>Warps - Static reference to the warp manager for accessing the warp system.</summary>
        public static WarpManager Warps;
        /// <summary>Regions - Static reference to the region manager for accessing the region system.</summary>
        public static RegionManager Regions;
        /// <summary>Backups - Static reference to the backup manager for accessing the backup system.</summary>
        public static BackupManager Backups;
        /// <summary>Groups - Static reference to the group manager for accessing the group system.</summary>
        public static GroupManager Groups;
        /// <summary>Users - Static reference to the user manager for accessing the user database system.</summary>
        public static UserAccountManager UserAccounts;
        /// <summary>ProjectileBans - Static reference to the projectile ban system.</summary>
        public static ProjectileManagager ProjectileBans;
        /// <summary>TileBans - Static reference to the tile ban system.</summary>
        public static TileManager TileBans;
        /// <summary>RememberedPos - Static reference to the remembered position manager.</summary>
        public static RememberedPosManager RememberedPos;
        /// <summary>CharacterDB - Static reference to the SSC character manager.</summary>
        public static CharacterManager CharacterDB;
        /// <summary>Contains the information about what research has been performed in Journey mode.</summary>
        public static ResearchDatastore ResearchDatastore;
        /// <summary>Config - Static reference to the config system, for accessing values set in users' config files.</summary>
        public static TShockConfig Config { get; set; }
        /// <summary>ServerSideCharacterConfig - Static reference to the server side character config, for accessing values set by users to modify SSC.</summary>
        public static ServerSideConfig ServerSideCharacterConfig;
        /// <summary>DB - Static reference to the database.</summary>
        public static IDbConnection DB;
        /// <summary>OverridePort - Determines if TShock should override the server port.</summary>
        public static bool OverridePort;
        /// <summary>Geo - Static reference to the GeoIP system which determines the location of an IP address.</summary>
        public static GeoIPCountry Geo;
        /// <summary>RestApi - Static reference to the Rest API authentication manager.</summary>
        public static SecureRest RestApi;
        /// <summary>RestManager - Static reference to the Rest API manager.</summary>
        public static RestManager RestManager;
        /// <summary>Utils - Static reference to the utilities class, which contains a variety of utility functions.</summary>
        public static Utils Utils = Utils.Instance;
        /// <summary>UpdateManager - Static reference to the update checker, which checks for updates and notifies server admins of updates.</summary>
        public static UpdateManager UpdateManager;
        /// <summary>Log - Static reference to the log system, which outputs to either SQL or a text file, depending on user config.</summary>
        public static ILog Log;
        /// <summary>instance - Static reference to the TerrariaPlugin instance.</summary>
        public static TerrariaPlugin instance;
        /// <summary>
        /// Static reference to a <see cref="CommandLineParser"/> used for simple command-line parsing
        /// </summary>
        public static CommandLineParser CliParser { get; } = new CommandLineParser();
        /// <summary>
        /// Used for implementing REST Tokens prior to the REST system starting up.
        /// </summary>
        public static Dictionary<string, SecureRest.TokenData> RESTStartupTokens = new Dictionary<string, SecureRest.TokenData>();

        /// <summary>The TShock anti-cheat/anti-exploit system.</summary>
        internal Bouncer Bouncer;

        /// <summary>The TShock item ban system.</summary>
        public static ItemBans ItemBans;

        /// <summary>
        /// TShock's Region subsystem.
        /// </summary>
        internal RegionHandler RegionSystem;

        /// <summary>
        /// Called after TShock is initialized. Useful for plugins that needs hooks before tshock but also depend on tshock being loaded.
        /// </summary>
        public static event Action Initialized;

        /// <summary>Version - The version required by the TerrariaAPI to be passed back for checking &amp; loading the plugin.</summary>
        /// <value>value - The version number specified in the Assembly, based on the VersionNum variable set in this class.</value>
        public override Version Version
        {
            get { return VersionNum; }
        }

        /// <summary>Name - The plugin name.</summary>
        /// <value>value - "TShock"</value>
        public override string Name
        {
            get { return "TShock"; }
        }

        /// <summary>Author - The author of the plugin.</summary>
        /// <value>value - "The TShock Team"</value>
        public override string Author
        {
            get { return "The TShock Team"; }
        }

        /// <summary>Description - The plugin description.</summary>
        /// <value>value - "The administration modification of the future."</value>
        public override string Description
        {
            get { return "The administration modification of the future."; }
        }

        /// <summary>TShock - The constructor for the TShock plugin.</summary>
        /// <param name="game">game - The Terraria main game.</param>
        public TShock(Main game)
            : base(game)
        {
            Config = new TShockConfig();
            ServerSideCharacterConfig = new ServerSideConfig();
            ServerSideCharacterConfig.Settings.StartingInventory.Add(new NetItem(-15, 1, 0));
            ServerSideCharacterConfig.Settings.StartingInventory.Add(new NetItem(-13, 1, 0));
            ServerSideCharacterConfig.Settings.StartingInventory.Add(new NetItem(-16, 1, 0));
            Order = 0;
            instance = this;
        }


        static Dictionary<string, IntPtr> _nativeCache = new Dictionary<string, IntPtr>();
        static IntPtr ResolveNativeDep(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (_nativeCache.TryGetValue(libraryName, out IntPtr cached))
                return cached;

            IEnumerable<string> matches = Enumerable.Empty<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var osx = Path.Combine(Environment.CurrentDirectory, "runtimes", "osx-x64");
                if (Directory.Exists(osx))
                    matches = Directory.GetFiles(osx, "*" + libraryName + "*", SearchOption.AllDirectories);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lib64 = Path.Combine(Environment.CurrentDirectory, "runtimes", "linux-x64");
                if (Directory.Exists(lib64))
                    matches = Directory.GetFiles(lib64, "*" + libraryName + "*", SearchOption.AllDirectories);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var x64 = Path.Combine(Environment.CurrentDirectory, "runtimes", "win-x64");
                if (Directory.Exists(x64))
                    matches = Directory.GetFiles(x64, "*" + libraryName + "*", SearchOption.AllDirectories);
            }

            if (matches.Count() == 0)
            {
                matches = Directory.GetFiles(Environment.CurrentDirectory, "*" + libraryName + "*");
            }

            Debug.WriteLine($"Looking for `{libraryName}` with {matches.Count()} match(es)");

            var handle = IntPtr.Zero;

            if (matches.Count() == 1)
            {
                var match = matches.Single();
                handle = NativeLibrary.Load(match);
            }

            // cache either way. if zero, no point calling IO if we've checked this assembly before.
            _nativeCache.Add(libraryName, handle);

            return handle;
        }

        /// <summary>Initialize - Called by the TerrariaServerAPI during initialization.</summary>
        public override void Initialize()
        {
            string logFilename;
            string logPathSetupWarning;

            OTAPI.Hooks.Netplay.CreateTcpListener += (sender, args) =>
            {
                args.Result = new LinuxTcpSocket();
            };
            OTAPI.Hooks.NetMessage.PlayerAnnounce += (sender, args) =>
            {
                //TShock handles this
                args.Result = OTAPI.Hooks.NetMessage.PlayerAnnounceResult.Default;
            };
            // if sqlite.interop cannot be found, try and search the runtimes folder. this usually happens when debugging tsapi
            // since it does not have the dependency installed directly
            NativeLibrary.SetDllImportResolver(typeof(SQLiteConnection).Assembly, ResolveNativeDep);

            Main.SettingsUnlock_WorldEvil = true;

            TerrariaApi.Reporting.CrashReporter.HeapshotRequesting += CrashReporter_HeapshotRequesting;

            Console.CancelKeyPress += new ConsoleCancelEventHandler(ConsoleCancelHandler);

            try
            {
                CliParser.Reset();
                HandleCommandLine(Environment.GetCommandLineArgs());

                if (!Directory.Exists(SavePath))
                    Directory.CreateDirectory(SavePath);

                TShockConfig.OnConfigRead += OnConfigRead;
                FileTools.SetupConfig();

                Main.ServerSideCharacter = ServerSideCharacterConfig.Settings.Enabled;

                //TSAPI previously would do this automatically, but the vanilla server wont
                if (Netplay.ServerIP == null)
                    Netplay.ServerIP = IPAddress.Any;

                DateTime now = DateTime.Now;
                // Log path was not already set by the command line parameter?
                if (LogPath == LogPathDefault)
                    LogPath = Config.Settings.LogPath;
                try
                {
                    logFilename = Path.Combine(LogPath, now.ToString(LogFormat) + ".log");
                    if (!Directory.Exists(LogPath))
                        Directory.CreateDirectory(LogPath);
                }
                catch (Exception ex)
                {
                    logPathSetupWarning =
                        "无法应用设定的日志路径，将使用默认值。 详情:\n" + ex;

                    ServerApi.LogWriter.PluginWriteLine(this, logPathSetupWarning, TraceLevel.Error);

                    // Problem with the log path or format use the default
                    logFilename = Path.Combine(LogPathDefault, now.ToString(LogFormatDefault) + ".log");
                }

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            }
            catch (Exception ex)
            {
                // Will be handled by the server api and written to its crashlog.txt.
                throw new Exception("致命的TShock初始化异常，详细信息请参见内部日志", ex);
            }

            // Further exceptions are written to TShock's log from now on.
            try
            {
                if (Config.Settings.StorageType.ToLower() == "sqlite")
                {
                    string sql = Path.Combine(SavePath, Config.Settings.SqliteDBPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(sql));
                    DB = new SQLiteConnection(string.Format("uri=file://{0},Version=3", sql));
                }
                else if (Config.Settings.StorageType.ToLower() == "mysql")
                {
                    try
                    {
                        var hostport = Config.Settings.MySqlHost.Split(':');
                        DB = new MySqlConnection();
                        DB.ConnectionString =
                            String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                                hostport[0],
                                hostport.Length > 1 ? hostport[1] : "3306",
                                Config.Settings.MySqlDbName,
                                Config.Settings.MySqlUsername,
                                Config.Settings.MySqlPassword
                                );
                    }
                    catch (MySqlException ex)
                    {
                        ServerApi.LogWriter.PluginWriteLine(this, ex.ToString(), TraceLevel.Error);
                        throw new Exception("MySql没有正确安装");
                    }
                }
                else
                {
                    throw new Exception("Invalid storage type");
                }

                if (Config.Settings.UseSqlLogs)
                    Log = new SqlLog(DB, logFilename, LogClear);
                else
                    Log = new TextLog(logFilename, LogClear);

                if (File.Exists(Path.Combine(SavePath, "tshock.pid")))
                {
                    Log.ConsoleInfo(
                        "检测到Tshock被强行关闭，请务必使用exit命令来关闭服务器");
                    File.Delete(Path.Combine(SavePath, "tshock.pid"));
                }
                File.WriteAllText(Path.Combine(SavePath, "tshock.pid"),
                    Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));

                CliParser.Reset();
                HandleCommandLinePostConfigLoad(Environment.GetCommandLineArgs());

                Backups = new BackupManager(Path.Combine(SavePath, "backups"));
                Backups.KeepFor = Config.Settings.BackupKeepFor;
                Backups.Interval = Config.Settings.BackupInterval;
                Bans = new BanManager(DB);
                Warps = new WarpManager(DB);
                Regions = new RegionManager(DB);
                UserAccounts = new UserAccountManager(DB);
                Groups = new GroupManager(DB);
                ProjectileBans = new ProjectileManagager(DB);
                TileBans = new TileManager(DB);
                RememberedPos = new RememberedPosManager(DB);
                CharacterDB = new CharacterManager(DB);
                ResearchDatastore = new ResearchDatastore(DB);
                RestApi = new SecureRest(Netplay.ServerIP, Config.Settings.RestApiPort);
                RestManager = new RestManager(RestApi);
                RestManager.RegisterRestfulCommands();
                Bouncer = new Bouncer();
                RegionSystem = new RegionHandler(Regions);
                ItemBans = new ItemBans(this, DB);

                var geoippath = "GeoIP.dat";
                if (Config.Settings.EnableGeoIP && File.Exists(geoippath))
                    Geo = new GeoIPCountry(geoippath);

                Log.ConsoleInfo("TShock {0} ({1}) 正在运行", Version, VersionCodename);

                ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit);
                ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
                ServerApi.Hooks.GameHardmodeTileUpdate.Register(this, OnHardUpdate);
                ServerApi.Hooks.GameStatueSpawn.Register(this, OnStatueSpawn);
                ServerApi.Hooks.ServerConnect.Register(this, OnConnect);
                ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
                ServerApi.Hooks.ServerChat.Register(this, OnChat);
                ServerApi.Hooks.ServerCommand.Register(this, ServerHooks_OnCommand);
                ServerApi.Hooks.NetGetData.Register(this, OnGetData);
                ServerApi.Hooks.NetSendData.Register(this, NetHooks_SendData);
                ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
                ServerApi.Hooks.NpcStrike.Register(this, NpcHooks_OnStrikeNpc);
                ServerApi.Hooks.ProjectileSetDefaults.Register(this, OnProjectileSetDefaults);
                ServerApi.Hooks.WorldStartHardMode.Register(this, OnStartHardMode);
                ServerApi.Hooks.WorldSave.Register(this, SaveManager.Instance.OnSaveWorld);
                ServerApi.Hooks.WorldChristmasCheck.Register(this, OnXmasCheck);
                ServerApi.Hooks.WorldHalloweenCheck.Register(this, OnHalloweenCheck);
                ServerApi.Hooks.NetNameCollision.Register(this, NetHooks_NameCollision);
                ServerApi.Hooks.ItemForceIntoChest.Register(this, OnItemForceIntoChest);
                ServerApi.Hooks.WorldGrassSpread.Register(this, OnWorldGrassSpread);
                PlayerHooks.PlayerPreLogin += OnPlayerPreLogin;
                PlayerHooks.PlayerPostLogin += OnPlayerLogin;
                AccountHooks.AccountDelete += OnAccountDelete;
                AccountHooks.AccountCreate += OnAccountCreate;

                GetDataHandlers.InitGetDataHandler();
                Commands.InitCommands();

                EnglishLanguage.Initialize();

                if (Config.Settings.RestApiEnabled)
                    RestApi.Start();

                Log.ConsoleInfo("自动保存 " + (Config.Settings.AutoSave ? "启用" : "禁用"));
                Log.ConsoleInfo("备份 " + (Backups.Interval > 0 ? "启用" : "禁用"));

                Initialized?.Invoke();

                Log.ConsoleInfo("欢迎使用TShock_Terraria服务器");
                Log.ConsoleInfo("TShock是免费软件");
                Log.ConsoleInfo("您可以根据GNU GPLv3的条款对其进行修改和分发");

            }
            catch (Exception ex)
            {
                Log.ConsoleError("致命启动异常");
                Log.ConsoleError(ex.ToString());
                Environment.Exit(1);
            }
        }

        protected void CrashReporter_HeapshotRequesting(object sender, EventArgs e)
        {
            foreach (TSPlayer player in TShock.Players)
            {
                player.Account = null;
            }
        }

        /// <summary>Dispose - Called when disposing.</summary>
        /// <param name="disposing">disposing - If set, disposes of all hooks and other systems.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // NOTE: order is important here
                if (Geo != null)
                {
                    Geo.Dispose();
                }
                SaveManager.Instance.Dispose();

                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.GameHardmodeTileUpdate.Deregister(this, OnHardUpdate);
                ServerApi.Hooks.GameStatueSpawn.Deregister(this, OnStatueSpawn);
                ServerApi.Hooks.ServerConnect.Deregister(this, OnConnect);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                ServerApi.Hooks.ServerCommand.Deregister(this, ServerHooks_OnCommand);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.NetSendData.Deregister(this, NetHooks_SendData);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
                ServerApi.Hooks.NpcStrike.Deregister(this, NpcHooks_OnStrikeNpc);
                ServerApi.Hooks.ProjectileSetDefaults.Deregister(this, OnProjectileSetDefaults);
                ServerApi.Hooks.WorldStartHardMode.Deregister(this, OnStartHardMode);
                ServerApi.Hooks.WorldSave.Deregister(this, SaveManager.Instance.OnSaveWorld);
                ServerApi.Hooks.WorldChristmasCheck.Deregister(this, OnXmasCheck);
                ServerApi.Hooks.WorldHalloweenCheck.Deregister(this, OnHalloweenCheck);
                ServerApi.Hooks.NetNameCollision.Deregister(this, NetHooks_NameCollision);
                ServerApi.Hooks.ItemForceIntoChest.Deregister(this, OnItemForceIntoChest);
                ServerApi.Hooks.WorldGrassSpread.Deregister(this, OnWorldGrassSpread);
                PlayerHooks.PlayerPostLogin -= OnPlayerLogin;

                if (File.Exists(Path.Combine(SavePath, "tshock.pid")))
                {
                    File.Delete(Path.Combine(SavePath, "tshock.pid"));
                }

                RestApi.Dispose();
                Log.Dispose();

                RegionSystem.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>OnPlayerLogin - Fires the PlayerLogin hook to listening plugins.</summary>
        /// <param name="args">args - The PlayerPostLoginEventArgs object.</param>
        private void OnPlayerLogin(PlayerPostLoginEventArgs args)
        {
            List<string> KnownIps = new();
            if (!string.IsNullOrWhiteSpace(args.Player.Account.KnownIps))
            {
                KnownIps = JsonConvert.DeserializeObject<List<string>>(args.Player.Account.KnownIps);
            }

            if (KnownIps.Count == 0)
            {
                KnownIps.Add(args.Player.IP);
            }
            else
            {
                bool last = KnownIps.Last() == args.Player.IP;
                if (!last)
                {
                    if (KnownIps.Count == 100)
                    {
                        KnownIps.RemoveAt(0);
                    }

                    KnownIps.Add(args.Player.IP);
                }
            }

            args.Player.Account.KnownIps = JsonConvert.SerializeObject(KnownIps, Formatting.Indented);
            UserAccounts.UpdateLogin(args.Player.Account);

            Bans.CheckBan(args.Player);
        }

        /// <summary>OnAccountDelete - Internal hook fired on account delete.</summary>
        /// <param name="args">args - The AccountDeleteEventArgs object.</param>
        private void OnAccountDelete(AccountDeleteEventArgs args)
        {
            CharacterDB.RemovePlayer(args.Account.ID);
        }

        /// <summary>OnAccountCreate - Internal hook fired on account creation.</summary>
        /// <param name="args">args - The AccountCreateEventArgs object.</param>
        private void OnAccountCreate(Hooks.AccountCreateEventArgs args)
        {
            CharacterDB.SeedInitialData(UserAccounts.GetUserAccount(args.Account));
        }

        /// <summary>OnPlayerPreLogin - Internal hook fired when on player pre login.</summary>
        /// <param name="args">args - The PlayerPreLoginEventArgs object.</param>
        private void OnPlayerPreLogin(Hooks.PlayerPreLoginEventArgs args)
        {
            if (args.Player.IsLoggedIn)
                args.Player.SaveServerCharacter();
        }

        /// <summary>NetHooks_NameCollision - Internal hook fired when a name collision happens.</summary>
        /// <param name="args">args - The NameCollisionEventArgs object.</param>
        private void NetHooks_NameCollision(NameCollisionEventArgs args)
        {
            if (args.Handled)
            {
                return;
            }

            string ip = Utils.GetRealIP(Netplay.Clients[args.Who].Socket.GetRemoteAddress().ToString());

            var player = Players.First(p => p != null && p.Name == args.Name && p.Index != args.Who);
            if (player != null)
            {
                if (player.IP == ip)
                {
                    Netplay.Clients[player.Index].PendingTermination = true;
                    args.Handled = true;
                    return;
                }
                if (player.IsLoggedIn)
                {
                    var ips = JsonConvert.DeserializeObject<List<string>>(player.Account.KnownIps);
                    if (ips.Contains(ip))
                    {
                        Netplay.Clients[player.Index].PendingTermination = true;
                        args.Handled = true;
                    }
                }
            }
        }

        /// <summary>OnItemForceIntoChest - Internal hook fired when a player quick stacks items into a chest.</summary>
        /// <param name="args">The <see cref="ForceItemIntoChestEventArgs"/> object.</param>
        private void OnItemForceIntoChest(ForceItemIntoChestEventArgs args)
        {
            if (args.Handled)
            {
                return;
            }

            if (args.Player == null)
            {
                args.Handled = true;
                return;
            }

            TSPlayer tsplr = Players[args.Player.whoAmI];
            if (tsplr == null)
            {
                args.Handled = true;
                return;
            }

            if (args.Chest != null)
            {
                if (Config.Settings.RegionProtectChests && !Regions.CanBuild((int)args.WorldPosition.X, (int)args.WorldPosition.Y, tsplr))
                {
                    args.Handled = true;
                    return;
                }

                if (!tsplr.IsInRange(args.Chest.x, args.Chest.y))
                {
                    args.Handled = true;
                    return;
                }
            }
        }

        /// <summary>OnXmasCheck - Internal hook fired when the XMasCheck happens.</summary>
        /// <param name="args">args - The ChristmasCheckEventArgs object.</param>
        private void OnXmasCheck(ChristmasCheckEventArgs args)
        {
            if (args.Handled)
                return;

            if (Config.Settings.ForceXmas)
            {
                args.Xmas = true;
                args.Handled = true;
            }
        }

        /// <summary>OnHalloweenCheck - Internal hook fired when the HalloweenCheck happens.</summary>
        /// <param name="args">args - The HalloweenCheckEventArgs object.</param>
        private void OnHalloweenCheck(HalloweenCheckEventArgs args)
        {
            if (args.Handled)
                return;

            if (Config.Settings.ForceHalloween)
            {
                args.Halloween = true;
                args.Handled = true;
            }
        }

        /// <summary>
        /// Handles exceptions that we didn't catch earlier in the code, or in Terraria.
        /// </summary>
        /// <param name="sender">sender - The object that sent the exception.</param>
        /// <param name="e">e - The UnhandledExceptionEventArgs object.</param>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error(e.ExceptionObject.ToString());

            if (e.ExceptionObject.ToString().Contains("Terraria.Netplay.ListenForClients") ||
                e.ExceptionObject.ToString().Contains("Terraria.Netplay.ServerLoop"))
            {
                var sb = new List<string>();
                for (int i = 0; i < Netplay.Clients.Length; i++)
                {
                    if (Netplay.Clients[i] == null)
                    {
                        sb.Add("Client[" + i + "]");
                    }
                    else if (Netplay.Clients[i].Socket == null)
                    {
                        sb.Add("Tcp[" + i + "]");
                    }
                }
                Log.Error(string.Join(", ", sb));
            }

            if (e.IsTerminating)
            {
                if (Main.worldPathName != null && Config.Settings.SaveWorldOnCrash)
                {
                    Main.ActiveWorldFileData._path += ".crash";
                    SaveManager.Instance.SaveWorld();
                }
            }
        }

        private bool tryingToShutdown = false;

        /// <summary> ConsoleCancelHandler - Handles when Ctrl + C is sent to the server for a safe shutdown. </summary>
        /// <param name="sender">The sender</param>
        /// <param name="args">The ConsoleCancelEventArgs associated with the event.</param>
        private void ConsoleCancelHandler(object sender, ConsoleCancelEventArgs args)
        {
            if (tryingToShutdown)
            {
                System.Environment.Exit(1);
                return;
            }
            // Cancel the default behavior
            args.Cancel = true;

            tryingToShutdown = true;

            Log.ConsoleInfo("Shutting down safely. To force shutdown, send SIGINT (CTRL + C) again.");

            // Perform a safe shutdown
            TShock.Utils.StopServer(true, "服务器控制台中断!");
        }

        /// <summary>HandleCommandLine - Handles the command line parameters passed to the server.</summary>
        /// <param name="parms">parms - The array of arguments passed in through the command line.</param>
        private void HandleCommandLine(string[] parms)
        {
            string path = null;

            //Generic method for doing a path sanity check
            Action<string> pathChecker = (p) =>
            {
                if (!string.IsNullOrWhiteSpace(p) && p.IndexOfAny(Path.GetInvalidPathChars()) == -1)
                {
                    path = p;
                }
            };

            //Prepare the parser with all the flags available
            CliParser
                .AddFlag("-configpath", pathChecker)
                    //The .After Action is run after the pathChecker Action
                    .After(() =>
                    {
                        SavePath = path ?? "tshock";
                        if (path != null)
                        {
                            ServerApi.LogWriter.PluginWriteLine(this, "配置路径已设置为" + path, TraceLevel.Info);
                        }
                    })

                .AddFlag("-worldselectpath", pathChecker)
                    .After(() =>
                    {
                        if (path != null)
                        {
                            Main.WorldPath = path;
                            ServerApi.LogWriter.PluginWriteLine(this, "地图路径已设置为" + path, TraceLevel.Info);
                        }
                    })

                .AddFlag("-logpath", pathChecker)
                    .After(() =>
                    {
                        if (path != null)
                        {
                            LogPath = path;
                            ServerApi.LogWriter.PluginWriteLine(this, "日志路径已设置为 " + path, TraceLevel.Info);
                        }
                    })

                .AddFlag("-logformat", (format) =>
                {
                    if (!string.IsNullOrWhiteSpace(format)) { LogFormat = format; }
                })

                .AddFlag("-config", (cfg) =>
                {
                    if (!string.IsNullOrWhiteSpace(cfg))
                    {
                        ServerApi.LogWriter.PluginWriteLine(this, string.Format("加载配置文件: {0}", cfg), TraceLevel.Verbose);
                        Main.instance.LoadDedConfig(cfg);
                    }
                })

                .AddFlag("-port", (p) =>
                {
                    int port;
                    if (int.TryParse(p, out port))
                    {
                        Netplay.ListenPort = port;
                        ServerApi.LogWriter.PluginWriteLine(this, string.Format("侦听端口: {0}.", port), TraceLevel.Verbose);
                    }
                })

                .AddFlag("-worldname", (world) =>
                {
                    if (!string.IsNullOrWhiteSpace(world))
                    {
                        Main.instance.SetWorldName(world);
                        ServerApi.LogWriter.PluginWriteLine(this, string.Format("地图名称将被覆盖为: {0}", world), TraceLevel.Verbose);
                    }
                })

                .AddFlag("-ip", (ip) =>
                {
                    IPAddress addr;
                    if (IPAddress.TryParse(ip, out addr))
                    {
                        Netplay.ServerIP = addr;
                        ServerApi.LogWriter.PluginWriteLine(this, string.Format("Listening on IP {0}.", addr), TraceLevel.Verbose);
                    }
                    else
                    {
                        // The server should not start up if this argument is invalid.
                        throw new InvalidOperationException("命令行参数值无效 \"-ip\".");
                    }
                })

                .AddFlag("-autocreate", (size) =>
                {
                    if (!string.IsNullOrWhiteSpace(size))
                    {
                        Main.instance.autoCreate(size);
                    }
                })


                //Flags without arguments
                .AddFlag("-logclear", () => LogClear = true)
                .AddFlag("-autoshutdown", () => Main.instance.EnableAutoShutdown())
                .AddFlag("-dump", () => Utils.Dump());

            CliParser.ParseFromSource(parms);
        }

        /// <summary>HandleCommandLinePostConfigLoad - Handles additional command line options after the config file is read.</summary>
        /// <param name="parms">parms - The array of arguments passed in through the command line.</param>
        public static void HandleCommandLinePostConfigLoad(string[] parms)
        {
            FlagSet portSet = new FlagSet("-port");
            FlagSet playerSet = new FlagSet("-maxplayers", "-players");
            FlagSet restTokenSet = new FlagSet("--rest-token", "-rest-token");
            FlagSet restEnableSet = new FlagSet("--rest-enabled", "-rest-enabled");
            FlagSet restPortSet = new FlagSet("--rest-port", "-rest-port");

            CliParser
                .AddFlags(portSet, (p) =>
                {
                    int port;
                    if (int.TryParse(p, out port))
                    {
                        Netplay.ListenPort = port;
                        Config.Settings.ServerPort = port;
                        OverridePort = true;
                        Log.ConsoleInfo("Port overridden by startup argument. Set to " + port);
                    }
                })
                .AddFlags(restTokenSet, (token) =>
                {
                    RESTStartupTokens.Add(token, new SecureRest.TokenData { Username = "null", UserGroupName = "superadmin" });
                    Console.WriteLine("启动参数将覆盖REST Token");
                })
                .AddFlags(restEnableSet, (e) =>
                {
                    bool enabled;
                    if (bool.TryParse(e, out enabled))
                    {
                        Config.Settings.RestApiEnabled = enabled;
                        Console.WriteLine("启动参数将覆盖REST开关");
                    }
                })
                .AddFlags(restPortSet, (p) =>
                {
                    int restPort;
                    if (int.TryParse(p, out restPort))
                    {
                        Config.Settings.RestApiPort = restPort;
                        Console.WriteLine("启动参数将覆盖REST端口");
                    }
                })
                .AddFlags(playerSet, (p) =>
                {
                    int slots;
                    if (int.TryParse(p, out slots))
                    {
                        Config.Settings.MaxSlots = slots;
                        Console.WriteLine("启动参数将覆盖最大用户数");
                    }
                });

            CliParser.ParseFromSource(parms);
        }

        /// <summary>SetupToken - The auth token used by the setup system to grant temporary superadmin access to new admins.</summary>
        public static int SetupToken = -1;
        private string _cliPassword = null;

        /// <summary>OnPostInit - Fired when the server loads a map, to perform world specific operations.</summary>
        /// <param name="args">args - The EventArgs object.</param>
        private void OnPostInit(EventArgs args)
        {
            Utils.SetConsoleTitle(false);

            //This is to prevent a bug where a CLI-defined password causes packets to be
            //sent in an unexpected order, resulting in clients being unable to connect
            if (!string.IsNullOrEmpty(Netplay.ServerPassword))
            {
                //CLI defined password overrides a config password
                if (!string.IsNullOrEmpty(Config.Settings.ServerPassword))
                {
                    Log.ConsoleError("!!! 在config.json中的密码被控制台输入覆盖");
                }

                if (!Config.Settings.DisableUUIDLogin)
                {
                    Log.ConsoleError("!!! UUID 登录已开启. 如果玩家UUID匹配账户, 服务器密码将会被跳过.");
                    Log.ConsoleError("!!! >如果想关闭此功能，在配置文件中设置 DisableUUIDLogin 为 true，然后 /reload.");
                }

                if (!Config.Settings.DisableLoginBeforeJoin)
                {
                    Log.ConsoleError("!!! 已开启在加入之前登录. 存在的账户可以登录 & 服务器的密码将会被跳过.");
                    Log.ConsoleError("!!! >如果想关闭此功能，在配置文件中设置 DisableLoginBeforeJoin 为 true，然后 /reload.");
                }

                _cliPassword = Netplay.ServerPassword;
                Netplay.ServerPassword = "";
                Config.Settings.ServerPassword = _cliPassword;
            }
            else
            {
                if (!string.IsNullOrEmpty(Config.Settings.ServerPassword))
                {
                    Log.ConsoleInfo("已使用配置文件中设置的服务器密码");
                }
            }

            if (!Config.Settings.DisableLoginBeforeJoin)
            {
                Log.ConsoleInfo("已开启在加入之前登录.在连接时，玩家可能输入玩家密码，而非服务器密码");
            }

            if (!Config.Settings.DisableUUIDLogin)
            {
                Log.ConsoleInfo("使用UUID登录已开启. 用户通过UUID自动登录.");
                Log.ConsoleInfo("恶意服务器很容易窃取用户的UUID。如果运行公共服务器，可以考虑关闭此选项。");
            }

            // Disable the auth system if "setup.lock" is present or a user account already exists
            if (File.Exists(Path.Combine(SavePath, "setup.lock")) || (UserAccounts.GetUserAccounts().Count() > 0))
            {
                SetupToken = 0;

                if (File.Exists(Path.Combine(SavePath, "setup-code.txt")))
                {
                    Log.ConsoleInfo("在用户数据库中检测到一个帐户，但是setup-code.txt仍然存在");
                    Log.ConsoleInfo("TShock现在将禁用初始设置系统，并删除setup-code.txt");
                    File.Delete(Path.Combine(SavePath, "setup-code.txt"));
                }

                if (!File.Exists(Path.Combine(SavePath, "setup.lock")))
                {
                    // This avoids unnecessary database work, which can get ridiculously high on old servers as all users need to be fetched
                    File.Create(Path.Combine(SavePath, "setup.lock"));
                }
            }
            else if (!File.Exists(Path.Combine(SavePath, "setup-code.txt")))
            {
                var r = new Random((int)DateTime.Now.ToBinary());
                SetupToken = r.Next(100000, 10000000);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("你需要设置服务器超级管理员，请加入游戏并输入 {0}setup {1}", Commands.Specifier, SetupToken);
                Console.WriteLine("在成功认证超级管理员之前，本提示将一直显示. ({0}setup)", Commands.Specifier);
                Console.ResetColor();
                File.WriteAllText(Path.Combine(SavePath, "setup-code.txt"), SetupToken.ToString());
            }
            else
            {
                SetupToken = Convert.ToInt32(File.ReadAllText(Path.Combine(SavePath, "setup-code.txt")));
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("注意: setup-code.txt 仍然存在，并且文件中的代码是有效的");
                Console.WriteLine("你需要设置服务器超级管理员，请加入游戏并输入 {0}setup {1}", Commands.Specifier, SetupToken);
                Console.WriteLine("在成功认证超级管理员之前，本提示将一直显示. ({0}setup)", Commands.Specifier);
                Console.ResetColor();
            }

            Regions.Reload();
            Warps.ReloadWarps();

            Utils.ComputeMaxStyles();
            Utils.FixChestStacks();

            if (Config.Settings.UseServerName)
            {
                Main.worldName = Config.Settings.ServerName;
            }

            UpdateManager = new UpdateManager();
        }

        /// <summary>LastCheck - Used to keep track of the last check for basically all time based checks.</summary>
        private DateTime LastCheck = DateTime.UtcNow;

        /// <summary>LastSave - Used to keep track of SSC save intervals.</summary>
        private DateTime LastSave = DateTime.UtcNow;

        /// <summary>OnUpdate - Called when ever the server ticks.</summary>
        /// <param name="args">args - EventArgs args</param>
        private void OnUpdate(EventArgs args)
        {
            // This forces Terraria to actually continue to update
            // even if there are no clients connected
            if (ServerApi.ForceUpdate)
            {
                Netplay.HasClients = true;
            }

            if (Backups.IsBackupTime)
                Backups.Backup();
            //call these every second, not every update
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 1)
            {
                OnSecondUpdate();
                LastCheck = DateTime.UtcNow;
            }

            if (Main.ServerSideCharacter && (DateTime.UtcNow - LastSave).TotalMinutes >= ServerSideCharacterConfig.Settings.ServerSideCharacterSave)
            {
                foreach (TSPlayer player in Players)
                {
                    // prevent null point exceptions
                    if (player != null && player.IsLoggedIn && !player.IsDisabledPendingTrashRemoval)
                    {

                        CharacterDB.InsertPlayerData(player);
                    }
                }
                LastSave = DateTime.UtcNow;
            }
        }

        /// <summary>OnSecondUpdate - Called effectively every second for all time based checks.</summary>
        private void OnSecondUpdate()
        {
            DisableFlags flags = Config.Settings.DisableSecondUpdateLogs ? DisableFlags.WriteToConsole : DisableFlags.WriteToLogAndConsole;

            if (Config.Settings.ForceTime != "normal")
            {
                switch (Config.Settings.ForceTime)
                {
                    case "day":
                        TSPlayer.Server.SetTime(true, 27000.0);
                        break;
                    case "night":
                        TSPlayer.Server.SetTime(false, 16200.0);
                        break;
                }
            }

            foreach (TSPlayer player in Players)
            {
                if (player != null && player.Active)
                {
                    if (player.TilesDestroyed != null)
                    {
                        if (player.TileKillThreshold >= Config.Settings.TileKillThreshold)
                        {
                            player.Disable("达到物块破坏阈值.", flags);
                            TSPlayer.Server.RevertTiles(player.TilesDestroyed);
                            player.TilesDestroyed.Clear();
                        }
                    }
                    if (player.TileKillThreshold > 0)
                    {
                        player.TileKillThreshold = 0;
                        //We don't want to revert the entire map in case of a disable.
                        lock (player.TilesDestroyed)
                            player.TilesDestroyed.Clear();
                    }

                    if (player.TilesCreated != null)
                    {
                        if (player.TilePlaceThreshold >= Config.Settings.TilePlaceThreshold)
                        {
                            player.Disable("达到放置阈值", flags);
                            lock (player.TilesCreated)
                            {
                                TSPlayer.Server.RevertTiles(player.TilesCreated);
                                player.TilesCreated.Clear();
                            }
                        }
                    }
                    if (player.TilePlaceThreshold > 0)
                    {
                        player.TilePlaceThreshold = 0;
                    }

                    if (player.RecentFuse > 0)
                        player.RecentFuse--;

                    if ((Main.ServerSideCharacter) && (player.TPlayer.SpawnX > 0) && (player.sX != player.TPlayer.SpawnX))
                    {
                        player.sX = player.TPlayer.SpawnX;
                        player.sY = player.TPlayer.SpawnY;
                    }

                    if ((Main.ServerSideCharacter) && (player.sX > 0) && (player.sY > 0) && (player.TPlayer.SpawnX < 0))
                    {
                        player.TPlayer.SpawnX = player.sX;
                        player.TPlayer.SpawnY = player.sY;
                    }

                    if (player.RPPending > 0)
                    {
                        if (player.RPPending == 1)
                        {
                            var pos = RememberedPos.GetLeavePos(player.Name, player.IP);
                            player.Teleport(pos.X * 16, pos.Y * 16);
                            player.RPPending = 0;
                        }
                        else
                        {
                            player.RPPending--;
                        }
                    }

                    if (player.TileLiquidThreshold >= Config.Settings.TileLiquidThreshold)
                    {
                        player.Disable("达到液体放置阈值", flags);
                    }
                    if (player.TileLiquidThreshold > 0)
                    {
                        player.TileLiquidThreshold = 0;
                    }

                    if (player.ProjectileThreshold >= Config.Settings.ProjectileThreshold)
                    {
                        player.Disable("达到射弹阈值", flags);
                    }
                    if (player.ProjectileThreshold > 0)
                    {
                        player.ProjectileThreshold = 0;
                    }

                    if (player.PaintThreshold >= Config.Settings.TilePaintThreshold)
                    {
                        player.Disable("达到油漆极限", flags);
                    }
                    if (player.PaintThreshold > 0)
                    {
                        player.PaintThreshold = 0;
                    }

                    if (player.HealOtherThreshold >= TShock.Config.Settings.HealOtherThreshold)
                    {
                        player.Disable("达到治愈用户阈值", flags);
                    }
                    if (player.HealOtherThreshold > 0)
                    {
                        player.HealOtherThreshold = 0;
                    }

                    if (player.RespawnTimer > 0 && --player.RespawnTimer == 0 && player.Difficulty != 2)
                    {
                        player.Spawn(PlayerSpawnContext.ReviveFromDeath);
                    }

                    if (!Main.ServerSideCharacter || (Main.ServerSideCharacter && player.IsLoggedIn))
                    {
                        if (!player.HasPermission(Permissions.ignorestackhackdetection))
                        {
                            player.IsDisabledForStackDetection = player.HasHackedItemStacks(shouldWarnPlayer: true);
                        }

                        if (player.IsBeingDisabled())
                        {
                            player.Disable(flags: flags);
                        }
                    }
                }
            }

            Bouncer.OnSecondUpdate();
            Utils.SetConsoleTitle(false);
        }

        /// <summary>OnHardUpdate - Fired when a hardmode tile update event happens.</summary>
        /// <param name="args">args - The HardmodeTileUpdateEventArgs object.</param>
        private void OnHardUpdate(HardmodeTileUpdateEventArgs args)
        {
            if (args.Handled)
                return;

            if (!OnCreep(args.Type))
            {
                args.Handled = true;
            }
        }

        /// <summary>OnWorldGrassSpread - Fired when grass is attempting to spread.</summary>
        /// <param name="args">args - The GrassSpreadEventArgs object.</param>
        private void OnWorldGrassSpread(GrassSpreadEventArgs args)
        {
            if (args.Handled)
                return;

            if (!OnCreep(args.Grass))
            {
                args.Handled = true;
            }
        }

        /// <summary>
        /// Checks if the tile type is allowed to creep
        /// </summary>
        /// <param name="tileType">Tile id</param>
        /// <returns>True if allowed, otherwise false</returns>
        private bool OnCreep(int tileType)
        {
            if (!Config.Settings.AllowCrimsonCreep && (tileType == TileID.Dirt || tileType == TileID.CrimsonGrass
                || TileID.Sets.Crimson[tileType]))
            {
                return false;
            }

            if (!Config.Settings.AllowCorruptionCreep && (tileType == TileID.Dirt || tileType == TileID.CorruptThorns
                || TileID.Sets.Corrupt[tileType]))
            {
                return false;
            }

            if (!Config.Settings.AllowHallowCreep && (TileID.Sets.Hallow[tileType]))
            {
                return false;
            }

            return true;
        }

        /// <summary>OnStatueSpawn - Fired when a statue spawns.</summary>
        /// <param name="args">args - The StatueSpawnEventArgs object.</param>
        private void OnStatueSpawn(StatueSpawnEventArgs args)
        {
            if (args.Within200 < Config.Settings.StatueSpawn200 && args.Within600 < Config.Settings.StatueSpawn600 && args.WorldWide < Config.Settings.StatueSpawnWorld)
            {
                args.Handled = true;
            }
            else
            {
                args.Handled = false;
            }
        }

        /// <summary>OnConnect - Fired when a player connects to the server.</summary>
        /// <param name="args">args - The ConnectEventArgs object.</param>
        private void OnConnect(ConnectEventArgs args)
        {
            if (ShuttingDown)
            {
                NetMessage.SendData((int)PacketTypes.Disconnect, args.Who, -1, NetworkText.FromLiteral("服务器正在关闭..."));
                args.Handled = true;
                return;
            }

            var player = new TSPlayer(args.Who);

            if (Utils.GetActivePlayerCount() + 1 > Config.Settings.MaxSlots + Config.Settings.ReservedSlots)
            {
                player.Kick(Config.Settings.ServerFullNoReservedReason, true, true, null, false);
                args.Handled = true;
                return;
            }

            if (!FileTools.OnWhitelist(player.IP))
            {
                player.Kick(Config.Settings.WhitelistKickReason, true, true, null, false);
                args.Handled = true;
                return;
            }

            if (Geo != null)
            {
                var code = Geo.TryGetCountryCode(IPAddress.Parse(player.IP));
                player.Country = code == null ? "N/A" : GeoIPCountry.GetCountryNameByCode(code);
                if (code == "A1")
                {
                    if (Config.Settings.KickProxyUsers)
                    {
                        player.Kick("不允许通过代理连接", true, true, null, false);
                        args.Handled = true;
                        return;
                    }
                }
            }
            Players[args.Who] = player;
        }

        /// <summary>OnJoin - Internal hook called when a player joins. This is called after OnConnect.</summary>
        /// <param name="args">args - The JoinEventArgs object.</param>
        private void OnJoin(JoinEventArgs args)
        {
            var player = Players[args.Who];
            if (player == null)
            {
                args.Handled = true;
                return;
            }

            if (Config.Settings.KickEmptyUUID && String.IsNullOrWhiteSpace(player.UUID))
            {
                player.Kick("您的客户发送了一个空白的UUID. 请重新配置或使用其他客户端", true, true, null, false);
                args.Handled = true;
                return;
            }

            Bans.CheckBan(player);
        }

        /// <summary>OnLeave - Called when a player leaves the server.</summary>
        /// <param name="args">args - The LeaveEventArgs object.</param>
        private void OnLeave(LeaveEventArgs args)
        {
            if (args.Who >= Players.Length || args.Who < 0)
            {
                //Something not right has happened
                return;
            }

            var tsplr = Players[args.Who];
            if (tsplr == null)
            {
                return;
            }

            Players[args.Who] = null;

            //Reset toggle creative powers to default, preventing potential power transfer & desync on another user occupying this slot later.

            foreach (var kv in CreativePowerManager.Instance._powersById)
            {
                var power = kv.Value;

                //No need to reset sliders - those are reset manually by the game, most likely an oversight that toggles don't receive this treatment.

                if (power is CreativePowers.APerPlayerTogglePower toggle)
                {
                    if (toggle._perPlayerIsEnabled[args.Who] == toggle._defaultToggleState)
                        continue;

                    toggle.SetEnabledState(args.Who, toggle._defaultToggleState);
                }
            }

            if (tsplr.ReceivedInfo)
            {
                if (!tsplr.SilentKickInProgress && tsplr.State >= 3)
                    Utils.Broadcast(tsplr.Name + " has left.", Color.Yellow);
                Log.Info("{0} 已断开连接.", tsplr.Name);

                if (tsplr.IsLoggedIn && !tsplr.IsDisabledPendingTrashRemoval && Main.ServerSideCharacter && (!tsplr.Dead || tsplr.TPlayer.difficulty != 2))
                {
                    tsplr.PlayerData.CopyCharacter(tsplr);
                    CharacterDB.InsertPlayerData(tsplr);
                }

                if (Config.Settings.RememberLeavePos && !tsplr.LoginHarassed)
                {
                    RememberedPos.InsertLeavePos(tsplr.Name, tsplr.IP, (int)(tsplr.X / 16), (int)(tsplr.Y / 16));
                }

                if (tsplr.tempGroupTimer != null)
                {
                    tsplr.tempGroupTimer.Stop();
                }
            }

            // Fire the OnPlayerLogout hook too, if the player was logged in and they have a TSPlayer object.
            if (tsplr.IsLoggedIn)
            {
                Hooks.PlayerHooks.OnPlayerLogout(tsplr);
            }

            // The last player will leave after this hook is executed.
            if (Utils.GetActivePlayerCount() == 1)
            {
                if (Config.Settings.SaveWorldOnLastPlayerExit)
                    SaveManager.Instance.SaveWorld();
                Utils.SetConsoleTitle(true);
            }
        }

        /// <summary>OnChat - Fired when a player chats. Used for handling chat and commands.</summary>
        /// <param name="args">args - The ServerChatEventArgs object.</param>
        private void OnChat(ServerChatEventArgs args)
        {
            if (args.Handled)
                return;

            var tsplr = Players[args.Who];
            if (tsplr == null)
            {
                args.Handled = true;
                return;
            }

            if (args.Text.Length > 500)
            {
                tsplr.Kick("尝试通过长聊天数据包进行攻击", true);
                args.Handled = true;
                return;
            }

            string text = args.Text;

            // Terraria now has chat commands on the client side.
            // These commands remove the commands prefix (e.g. /me /playing) and send the command id instead
            // In order for us to keep legacy code we must reverse this and get the prefix using the command id
            foreach (var item in Terraria.UI.Chat.ChatManager.Commands._localizedCommands)
            {
                if (item.Value._name == args.CommandId._name)
                {
                    if (!String.IsNullOrEmpty(text))
                    {
                        text = item.Key.Value + ' ' + text;
                    }
                    else
                    {
                        text = item.Key.Value;
                    }
                    break;
                }
            }

            if ((text.StartsWith(Config.Settings.CommandSpecifier) || text.StartsWith(Config.Settings.CommandSilentSpecifier))
                && !string.IsNullOrWhiteSpace(text.Substring(1)))
            {
                try
                {
                    args.Handled = true;
                    if (!Commands.HandleCommand(tsplr, text))
                    {
                        // This is required in case anyone makes HandleCommand return false again
                        tsplr.SendErrorMessage("无法解析命令，请联系管理员以获取帮助");
                        Log.ConsoleError("Unable to parse command '{0}' from player {1}.", text, tsplr.Name);
                    }
                }
                catch (Exception ex)
                {
                    Log.ConsoleError("执行命令时发生异常");
                    Log.Error(ex.ToString());
                }
            }
            else
            {
                if (!tsplr.HasPermission(Permissions.canchat))
                {
                    args.Handled = true;
                }
                else if (tsplr.mute)
                {
                    tsplr.SendErrorMessage("你已被禁言");
                    args.Handled = true;
                }
                else if (!TShock.Config.Settings.EnableChatAboveHeads)
                {
                    text = String.Format(Config.Settings.ChatFormat, tsplr.Group.Name, tsplr.Group.Prefix, tsplr.Name, tsplr.Group.Suffix,
                                             args.Text);

                    //Invoke the PlayerChat hook. If this hook event is handled then we need to prevent sending the chat message
                    bool cancelChat = PlayerHooks.OnPlayerChat(tsplr, args.Text, ref text);
                    args.Handled = true;

                    if (cancelChat)
                    {
                        return;
                    }

                    Utils.Broadcast(text, tsplr.Group.R, tsplr.Group.G, tsplr.Group.B);
                }
                else
                {
                    Player ply = Main.player[args.Who];
                    string name = ply.name;
                    ply.name = String.Format(Config.Settings.ChatAboveHeadsFormat, tsplr.Group.Name, tsplr.Group.Prefix, tsplr.Name, tsplr.Group.Suffix);
                    //Update the player's name to format text nicely. This needs to be done because Terraria automatically formats messages against our will
                    NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, NetworkText.FromLiteral(ply.name), args.Who, 0, 0, 0, 0);

                    //Give that poor player their name back :'c
                    ply.name = name;

                    bool cancelChat = PlayerHooks.OnPlayerChat(tsplr, args.Text, ref text);
                    if (cancelChat)
                    {
                        args.Handled = true;
                        return;
                    }

                    //This netpacket is used to send chat text from the server to clients, in this case on behalf of a client
                    Terraria.Net.NetPacket packet = Terraria.GameContent.NetModules.NetTextModule.SerializeServerMessage(
                        NetworkText.FromLiteral(text), new Color(tsplr.Group.R, tsplr.Group.G, tsplr.Group.B), (byte)args.Who
                    );
                    //Broadcast to everyone except the player who sent the message.
                    //This is so that we can send them the same nicely formatted message that everyone else gets
                    Terraria.Net.NetManager.Instance.Broadcast(packet, args.Who);

                    //Reset their name
                    NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, NetworkText.FromLiteral(name), args.Who, 0, 0, 0, 0);

                    string msg = String.Format("<{0}> {1}",
                        String.Format(Config.Settings.ChatAboveHeadsFormat, tsplr.Group.Name, tsplr.Group.Prefix, tsplr.Name, tsplr.Group.Suffix),
                        text
                    );

                    //Send the original sender their nicely formatted message, and do all the loggy things
                    tsplr.SendMessage(msg, tsplr.Group.R, tsplr.Group.G, tsplr.Group.B);
                    TSPlayer.Server.SendMessage(msg, tsplr.Group.R, tsplr.Group.G, tsplr.Group.B);
                    Log.Info("Broadcast: {0}", msg);
                    args.Handled = true;
                }
            }
        }

        /// <summary>
        /// Called when a command is issued from the server console.
        /// </summary>
        /// <param name="args">The CommandEventArgs object</param>
        private void ServerHooks_OnCommand(CommandEventArgs args)
        {
            if (args.Handled)
                return;

            if (string.IsNullOrWhiteSpace(args.Command))
            {
                args.Handled = true;
                return;
            }

            // Damn you ThreadStatic and Redigit
            if (Main.rand == null)
            {
                Main.rand = new UnifiedRandom();
            }

            if (args.Command == "autosave")
            {
                Main.autoSave = Config.Settings.AutoSave = !Config.Settings.AutoSave;
                Log.ConsoleInfo("自动保存 " + (Config.Settings.AutoSave ? "启用" : "禁用"));
            }
            else if (args.Command.StartsWith(Commands.Specifier) || args.Command.StartsWith(Commands.SilentSpecifier))
            {
                Commands.HandleCommand(TSPlayer.Server, args.Command);
            }
            else
            {
                Commands.HandleCommand(TSPlayer.Server, "/" + args.Command);
            }
            args.Handled = true;
        }

        /// <summary>OnGetData - Called when the server gets raw data packets.</summary>
        /// <param name="e">e - The GetDataEventArgs object.</param>
        private void OnGetData(GetDataEventArgs e)
        {
            if (e.Handled)
                return;

            PacketTypes type = e.MsgID;

            var player = Players[e.Msg.whoAmI];
            if (player == null || !player.ConnectionAlive)
            {
                e.Handled = true;
                return;
            }

            if (player.RequiresPassword && type != PacketTypes.PasswordSend)
            {
                e.Handled = true;
                return;
            }

            if ((player.State < 10 || player.Dead) && (int)type > 12 && (int)type != 16 && (int)type != 42 && (int)type != 50 &&
                (int)type != 38 && (int)type != 21 && (int)type != 22)
            {
                e.Handled = true;
                return;
            }

            int length = e.Length - 1;
            if (length < 0)
            {
                length = 0;
            }
            using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length - 1))
            {
                // Exceptions are already handled
                e.Handled = GetDataHandlers.HandlerGetData(type, player, data);
            }
        }

        /// <summary>OnGreetPlayer - Fired when a player is greeted by the server. Handles things like the MOTD, join messages, etc.</summary>
        /// <param name="args">args - The GreetPlayerEventArgs object.</param>
        private void OnGreetPlayer(GreetPlayerEventArgs args)
        {
            var player = Players[args.Who];
            if (player == null)
            {
                args.Handled = true;
                return;
            }

            player.LoginMS = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (Config.Settings.EnableGeoIP && TShock.Geo != null)
            {
                Log.Info("{0} ({1}) 属于 '{2}' 组，来自 '{3}' 已进入. ({4}/{5})", player.Name, player.IP,
                                       player.Group.Name, player.Country, TShock.Utils.GetActivePlayerCount(),
                                       TShock.Config.Settings.MaxSlots);
                if (!player.SilentJoinInProgress)
                    Utils.Broadcast(string.Format("{0} ({1}) 已进入.", player.Name, player.Country), Color.Yellow);
            }
            else
            {
                Log.Info("{0} ({1}) 属于 '{2}' 已进入. ({3}/{4})", player.Name, player.IP,
                                       player.Group.Name, TShock.Utils.GetActivePlayerCount(), TShock.Config.Settings.MaxSlots);
                if (!player.SilentJoinInProgress)
                    Utils.Broadcast(player.Name + " 已进入.", Color.Yellow);
            }

            if (Config.Settings.DisplayIPToAdmins)
                Utils.SendLogs(string.Format("{0} 已进入. IP: {1}", player.Name, player.IP), Color.Blue);

            player.SendFileTextAsMessage(FileTools.MotdPath);

            string pvpMode = Config.Settings.PvPMode.ToLowerInvariant();
            if (pvpMode == "always")
            {
                player.TPlayer.hostile = true;
                player.SendData(PacketTypes.TogglePvp, "", player.Index);
                TSPlayer.All.SendData(PacketTypes.TogglePvp, "", player.Index);
            }

            if (!player.IsLoggedIn)
            {
                if (Main.ServerSideCharacter)
                {
                    player.IsDisabledForSSC = true;
                    player.SendErrorMessage(String.Format("SSC已启用! 请输入 {0}register 或 {0}login 注册登录后才能开始游戏！", Commands.Specifier));
                    player.LoginHarassed = true;
                }
                else if (Config.Settings.RequireLogin)
                {
                    player.SendErrorMessage("请输入 {0}register 或 {0}login 注册登录后才能开始游戏！", Commands.Specifier);
                    player.LoginHarassed = true;
                }
            }

            player.LastNetPosition = new Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f);

            if (Config.Settings.RememberLeavePos && (RememberedPos.GetLeavePos(player.Name, player.IP) != Vector2.Zero) && !player.LoginHarassed)
            {
                player.RPPending = 3;
                player.SendInfoMessage("你将被传送到您最后的记录位置");
            }

            args.Handled = true;
        }

        /// <summary>NpcHooks_OnStrikeNpc - Fired when an NPC strike packet happens.</summary>
        /// <param name="e">e - The NpcStrikeEventArgs object.</param>
        private void NpcHooks_OnStrikeNpc(NpcStrikeEventArgs e)
        {
            if (Config.Settings.InfiniteInvasion)
            {
                if (Main.invasionSize < 10)
                {
                    Main.invasionSize = 20000000;
                }
            }
        }

        /// <summary>OnProjectileSetDefaults - Called when a projectile sets the default attributes for itself.</summary>
        /// <param name="e">e - The SetDefaultsEventArgs object parameterized with Projectile and int.</param>
        private void OnProjectileSetDefaults(SetDefaultsEventArgs<Projectile, int> e)
        {
            //tombstone fix.
            if (e.Info == ProjectileID.Tombstone || (e.Info >= ProjectileID.GraveMarker && e.Info <= ProjectileID.Obelisk) || (e.Info >= ProjectileID.RichGravestone1 && e.Info <= ProjectileID.RichGravestone5))
                if (Config.Settings.DisableTombstones)
                    e.Object.SetDefaults(0);
            if (e.Info == ProjectileID.HappyBomb)
                if (Config.Settings.DisableClownBombs)
                    e.Object.SetDefaults(0);
            if (e.Info == ProjectileID.SnowBallHostile)
                if (Config.Settings.DisableSnowBalls)
                    e.Object.SetDefaults(0);
            if (e.Info == ProjectileID.BombSkeletronPrime)
                if (Config.Settings.DisablePrimeBombs)
                    e.Object.SetDefaults(0);
        }

        /// <summary>NetHooks_SendData - Fired when the server sends data.</summary>
        /// <param name="e">e - The SendDataEventArgs object.</param>
        private void NetHooks_SendData(SendDataEventArgs e)
        {
            if (e.MsgId == PacketTypes.PlayerHp)
            {
                if (Main.player[(byte)e.number].statLife <= 0)
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (e.MsgId == PacketTypes.ProjectileNew)
            {
                if (e.number >= 0 && e.number < Main.projectile.Length)
                {
                    var projectile = Main.projectile[e.number];
                    if (projectile.active && projectile.owner >= 0 &&
                        (GetDataHandlers.projectileCreatesLiquid.ContainsKey(projectile.type) || GetDataHandlers.projectileCreatesTile.ContainsKey(projectile.type)))
                    {
                        var player = Players[projectile.owner];
                        if (player != null)
                        {
                            if (player.RecentlyCreatedProjectiles.Any(p => p.Index == e.number && p.Killed))
                            {
                                player.RecentlyCreatedProjectiles.RemoveAll(p => p.Index == e.number && p.Killed);
                            }

                            if (!player.RecentlyCreatedProjectiles.Any(p => p.Index == e.number))
                            {
                                player.RecentlyCreatedProjectiles.Add(new GetDataHandlers.ProjectileStruct()
                                {
                                    Index = e.number,
                                    Type = (short)projectile.type,
                                    CreatedAt = DateTime.Now
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>OnStartHardMode - Fired when hard mode is started.</summary>
        /// <param name="e">e - The HandledEventArgs object.</param>
        private void OnStartHardMode(HandledEventArgs e)
        {
            if (Config.Settings.DisableHardmode)
                e.Handled = true;
        }

        /// <summary>OnConfigRead - Fired when the config file has been read.</summary>
        /// <param name="file">file - The config file object.</param>
        public void OnConfigRead(ConfigFile<TShockSettings> file)
        {
            NPC.defaultMaxSpawns = file.Settings.DefaultMaximumSpawns;
            NPC.defaultSpawnRate = file.Settings.DefaultSpawnRate;

            Main.autoSave = file.Settings.AutoSave;
            if (Backups != null)
            {
                Backups.KeepFor = file.Settings.BackupKeepFor;
                Backups.Interval = file.Settings.BackupInterval;
            }
            if (!OverridePort)
            {
                Netplay.ListenPort = file.Settings.ServerPort;
            }

            if (file.Settings.MaxSlots > Main.maxPlayers - file.Settings.ReservedSlots)
                file.Settings.MaxSlots = Main.maxPlayers - file.Settings.ReservedSlots;
            Main.maxNetPlayers = file.Settings.MaxSlots + file.Settings.ReservedSlots;

            Netplay.ServerPassword = "";
            if (!string.IsNullOrEmpty(_cliPassword))
            {
                //This prevents a config reload from removing/updating a CLI-defined password
                file.Settings.ServerPassword = _cliPassword;
            }

            Netplay.SpamCheck = false;
        }
    }
}
