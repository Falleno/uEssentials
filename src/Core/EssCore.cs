/*
 *  This file is part of uEssentials project.
 *      https://uessentials.github.io/
 *
 *  Copyright (C) 2015-2016  Leonardosc
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License along
 *  with this program; if not, write to the Free Software Foundation, Inc.,
 *  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Essentials.Api;
using Essentials.Api.Event;
using Essentials.Api.Logging;
using Essentials.Api.Module;
using Essentials.Api.Task;
using Essentials.Api.Unturned;
using Essentials.Commands;
using Essentials.Common;
using Essentials.Configuration;
using Essentials.Core.Command;
using Essentials.Core.Event;
using Essentials.I18n;
using Rocket.Core;
using Rocket.Core.Plugins;
using Essentials.Common.Reflect;
using Essentials.Compatibility;
using Essentials.Core.Permission;
using Essentials.Event.Handling;
using Essentials.NativeModules;
using Essentials.Updater;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using Rocket.Core.Commands;
using Essentials.Compatibility.Hooks;
using Essentials.Economy;
using Environment = Rocket.Core.Environment;

namespace Essentials.Core
{
    public sealed class EssCore : RocketPlugin
    {
        internal const string                         ROCKET_VERSION              = "4.9.4.0";
        internal const string                         UNTURNED_VERSION            = "3.15.0.2";
        internal const string                         PLUGIN_VERSION              = "1.2.2.3";
        
        internal static EssCore                       Instance                    { get; set; }
        
        internal Optional<IEconomyProvider>           EconomyProvider             { get; set; }
        internal ModuleManager                        ModuleManager               { get; set; }
        internal CommandManager                       CommandManager              { get; set; }
        internal IEventManager                        EventManager                { get; set; }
        internal HookManager                          HookManager                 { get; set; }
        internal IUpdater                             Updater                     { get; set; }

        internal CommandsConfig                       CommandsConfig              { get; set; }
        internal TextCommands                         TextCommands                { get; set; }
        internal EssConfig                            Config                      { get; set; }
        internal EssLogger                            Logger                      { get; set; }

        internal HashSet<UPlayer>                     ConnectedPlayers            { get; set; }

        private string _folder;
        private string _translationFolder;
        private string _dataFolder;
        private string _modulesFolder;

        internal string Folder => MkDirIfNotExists( _folder );
        internal string TranslationFolder => MkDirIfNotExists( _translationFolder );
        internal string DataFolder => MkDirIfNotExists( _dataFolder );
        internal string ModulesFolder => MkDirIfNotExists( _modulesFolder );


        protected override void Load()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                Instance = this;
                R.Permissions = new EssentialsPermissionsProvider();

                Provider.onServerDisconnected += PlayerDisconnectCallback;
                Provider.onServerConnected += PlayerConnectCallback;

                Logger = new EssLogger( "[uEssentials] " );
                ConnectedPlayers = new HashSet<UPlayer>();

                Logger.Log( "Enabling uEssentials.", ConsoleColor.Green );

                if ( Provider.Players.Count > 0 )
                {
                    Provider.Players.ForEach( p => {
                        ConnectedPlayers.Add( new UPlayer( UnturnedPlayer.FromSteamPlayer( p ) ) );
                    } );
                }

                _folder = Environment.PluginsDirectory + "/uEssentials/";
                _translationFolder = Folder + "translations/";
                _dataFolder = Folder + "data/";
                _modulesFolder = Folder + "modules/";

                new [] { _folder, _translationFolder, _dataFolder, _modulesFolder }
                    .WhereNot( System.IO.Directory.Exists )
                    .ForEach( d => System.IO.Directory.CreateDirectory(d) );

                var configPath = $"{Folder}config.json";

                Config = new EssConfig();
                Config.Load( configPath );

                if ( Config.WebConfig.Enabled )
                {
                    Config = new EssWebConfig();
                    Config.Load( configPath );
                }

                CommandsConfig = new CommandsConfig();
                CommandsConfig.Load( $"{Folder}commands.json" );

                Updater = new GithubUpdater();
                EventManager = new EventManager();
                CommandManager = new CommandManager();
                ModuleManager = new ModuleManager();
                HookManager = new HookManager();

                EssLang.Load();

                Logger.Log( "Plugin version: ", ConsoleColor.Green, suffix: "" );

                #if EXPERIMENTAL
                Logger.Log( $"{PLUGIN_VERSION} (Experimental)", ConsoleColor.White, "" );
                #else
                Logger.Log( PLUGIN_VERSION, ConsoleColor.White, "" );
                #endif

                Logger.Log( "Recommended Rocket version: ", ConsoleColor.Green, suffix: "" );
                Logger.Log( ROCKET_VERSION, ConsoleColor.White, "" );
                Logger.Log( "Recommended Unturned version: ", ConsoleColor.Green, suffix: "" );
                Logger.Log( UNTURNED_VERSION, ConsoleColor.White, "" );
                Logger.Log( "Author: ", ConsoleColor.Green, suffix: "" );
                Logger.Log( "leonardosc", ConsoleColor.White, "" );
                Logger.Log( "Wiki: ", ConsoleColor.Green, suffix: "" );
                Logger.Log( "uessentials.github.io", ConsoleColor.White, "" );

                EventManager.RegisterAll( GetType().Assembly );

                if ( !Config.EnableJoinLeaveMessage )
                {
                    EventManager.Unregister<EssentialsEventHandler>( "JoinMessage" );
                    EventManager.Unregister<EssentialsEventHandler>( "LeaveMessage" );
                }

                CommandManager.RegisterAll( "Essentials.Commands" );

                HookManager.RegisterAll();
                HookManager.LoadAll();
                
                if ( Config.Economy.UseXp )
                {
                    EconomyProvider = Optional<IEconomyProvider>.Of( new ExpEconomyProvider() );
                }
                else if ( HookManager.GetActiveByType<UconomyHook>().IsPresent )
                {
                    EconomyProvider = Optional<IEconomyProvider>.Of( 
                            HookManager.GetActiveByType<UconomyHook>().Value );
                }
                else
                {
                    EconomyProvider = Optional<IEconomyProvider>.Empty();
                }

                /*
                    Load native modules
                */
                (
                    from type in Assembly.GetTypes()
                    where typeof(NativeModule).IsAssignableFrom( type )
                    where !type.IsAbstract
                    let mAttr = (ModuleInfo) type.GetCustomAttributes( typeof(ModuleInfo), false )[0]
                    where Config.EnabledSystems.Any( s => s.Equals(mAttr.Name, StringComparison.OrdinalIgnoreCase) )
                    select type
                ).ForEach( type => {
                    ModuleManager.LoadModule( (NativeModule) Activator.CreateInstance( type ) );
                } );

                Logger.LogInfo( $"Loaded {CommandManager.Commands.Count()} commands" );

                Logger.LogInfo( "Loading modules..." );
                ModuleManager.LoadAll( ModulesFolder );
                Logger.LogInfo( $"Loaded {ModuleManager.RunningModules.Count( t => !(t is NativeModule) )} modules" );

                if ( Config.AutoAnnouncer.Enabled )
                {
                    Config.AutoAnnouncer.Start();
                }

                if ( Config.AutoCommands.Enabled )
                {
                    Config.AutoCommands.Start();
                }

                if ( !Config.Updater.AlertOnJoin )
                {
                    EventManager.Unregister<EssentialsEventHandler>( "UpdateAlert" );
                }

                if ( Config.ServerFrameRate != -1 )
                {
                    var frameRate = Config.ServerFrameRate;

                    if ( Config.ServerFrameRate < -1 )
                    {
                        frameRate = -1; // Set to default
                    }

                    UnityEngine.Application.targetFrameRate = frameRate;
                }

                if ( Config.DisabledCommands.Count != 0 )
                {
                    Config.DisabledCommands.ForEach( cmdName => {
                        var command = CommandManager.GetByName( cmdName );

                        if ( command == null || command is CommandEssentials )
                        {
                            Logger.LogWarning( $"There is no command named '{cmdName}' to disable." );
                        }
                        else
                        {
                            CommandManager.Unregister( command );
                            Logger.LogInfo( $"Disabled command: '{command.Name}'" );
                        }
                    } );
                }
                
                if ( Config.EnableTextCommands )
                {
                    var textCommandsFile = $"{Folder}textcommands.json";

                    TextCommands = new TextCommands();
                    TextCommands.Load( textCommandsFile );

                    TextCommands.Commands.ForEach( txtCommand => {
                        CommandManager.Register( new TextCommand(txtCommand) ); 
                    });
                }
                
                if ( !Config.EnableDeathMessages )
                {
                    EventManager.Unregister<EssentialsEventHandler>( "DeathMessages" );
                }

                #if EXPERIMENTAL
                Logger.LogWarning( "THIS IS AN EXPERIMENTAL BUILD, CAN BE BUGGY." );
                #endif
                
                TryAddComponent<Tasks.TaskExecutor>();

                Tasks.New( t => {
                    File.Delete( $"{Folder}uEssentials.en.translation.xml" );
                    File.Delete( $"{Folder}uEssentials.configuration.xml" );
                } ).Delay( 100 ).Go();

                Tasks.New( t => {
                    UnregisterRocketCommands( true ); // Second check, silently.
                }).Delay( 3000 ).Go();

                CommandWindow.ConsoleInput.onInputText += ReloadCallback;
                UnregisterRocketCommands(); // First check.
                Logger.Log( $"Enabled ({stopwatch.ElapsedMilliseconds} ms)", ConsoleColor.Green );
            }
            catch (Exception e)
            {
                var msg = new List<string>() {
                    "An error occurred while enabling uEssentials.",
                    "If this error is not related with wrong configuration please report",  
                    "immediatly here https://github.com/uEssentials/uEssentials/issues",
                    "Error: " + e.ToString()
                };
                
                if ( !Provider.Version.EqualsIgnoreCase( UNTURNED_VERSION ) ) 
                {
                    msg.Add( "I detected that you are using an different version of the recommended, " + 
                             "please update your uEssentials." );
                    msg.Add( "If you are using the latest uEssentials release, please wait for update." );
                }
                
                if ( Logger == null )
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    msg.ForEach( Console.WriteLine );
                    Console.BackgroundColor = ConsoleColor.White;
                }
                else
                {
                    msg.ForEach( Logger.LogError );
                }
            }

            #if !DEV
              CheckUpdates();
              Analytics.Metrics.Init();
            #else
              CommandWindow.ConsoleOutput.title = "Unturned Server";
            #endif
        }

        protected override void Unload()
        {
            CommandWindow.ConsoleInput.onInputText -= ReloadCallback;
            Provider.onServerDisconnected -= PlayerDisconnectCallback;
            Provider.onServerConnected -= PlayerConnectCallback;

            TryRemoveComponent<Tasks.TaskExecutor>();

            var executingAssembly = GetType().Assembly;
            
            HookManager.UnloadAll();
            HookManager.UnregisterAll();
            CommandManager.UnregisterAll( executingAssembly );
            EventManager.UnregisterAll( executingAssembly ); 
            ModuleManager.UnloadAll();

            Tasks.CancelAll();
        }

        private static void ReloadCallback( string command )
        {
            if ( !command.StartsWith( "rocket reload", true, CultureInfo.InvariantCulture ) )
            {
                return;
            }

            Console.WriteLine();
            UEssentials.Logger.LogError( "Rocket reload cause many issues, consider restart the server" );
            UEssentials.Logger.LogError( "Or use '/essentials reload' to reload essentials correctly." );
            Console.WriteLine();
        }

        private void PlayerConnectCallback( CSteamID id )
        {
            ConnectedPlayers.Add( new UPlayer( UnturnedPlayer.FromCSteamID( id ) ) );
        }

        private void PlayerDisconnectCallback( CSteamID id )
        {
            ConnectedPlayers.RemoveWhere( connectedPlayer => connectedPlayer.CSteamId == id );
        }
        
        private void CheckUpdates()
        {
            if ( !Config.Updater.CheckUpdates )
            {
                return;
            }

            var worker = new BackgroundWorker();

            worker.DoWork += ( sender, args ) => 
            {
                Logger.LogInfo( "Checking updates." );

                var isUpdated = Updater.IsUpdated();
                var lastResult = Updater.LastResult;

                if ( isUpdated )
                {
                    Logger.LogInfo( "Plugin is up-to-date!" );
                    return;
                }

                Logger.LogInfo( $"New version avalaible: {lastResult.LatestVersion}" );

                if ( !lastResult.AdditionalData.IsNullOrEmpty() )
                {
                    Newtonsoft.Json.Linq.JToken changesStr;
                    if ( Newtonsoft.Json.Linq.JObject.Parse( lastResult.AdditionalData ).TryGetValue( "changes", out changesStr ) )
                    {
                        Logger.LogInfo( "========================= [ Changes ] =========================" );

                        changesStr.ToString().Split( '\n' ).ForEach( msg => {
                            Logger.Log( "", ConsoleColor.Green, suffix: "" );
                            Logger.Log( "  " + msg, ConsoleColor.White, "" );
                        } );

                        Logger.LogInfo( "" );
                        Logger.Log( "", ConsoleColor.Green, suffix: "" );
                        Logger.Log( "  " + $"See more in: https://github.com/uEssentials/uEssentials/releases/tag/{lastResult.LatestVersion}", ConsoleColor.White, "" );

                        Logger.LogInfo( "===============================================================" );
                    }
                }

                if ( Config.Updater.DownloadLatest )
                {
                    Updater.DownloadLatestRelease( $"{Folder}/updates/" );
                }
            };

            worker.RunWorkerCompleted += ( sender, args ) => {
                if ( args.Error != null )
                {
                    Logger.LogError( $"Could not update, try again later. ({args.Error.Message})" );
                    Logger.LogError( "Try downloading manually here: https://github.com/uEssentials/uEssentials/releases" );
                }
            };

            worker.RunWorkerAsync();
        }
        
        private void UnregisterRocketCommands( bool silent = false )
        {
            var _rocketCommands = AccessorFactory.AccessField<List<RocketCommandManager.RegisteredRocketCommand>>( R.Commands, "commands" ).Value;
            
            /* All names & aliases of uEssentials command */
            var essCommands = _rocketCommands
                               .Where( c => c.Command is CommandAdapter )
                               .Select( c => c.Name.ToLowerInvariant() )
                               .ToList();
            
            _rocketCommands.RemoveAll( c => {
                if ( essCommands.Contains( c.Name.ToLowerInvariant() ) && !(c.Command is CommandAdapter) )
                {
                    if ( !silent )
                        Logger.LogInfo( $"Overriding Rocket command ({c.Name.ToLowerInvariant()})" );
                    return true;
                }
                return false;
            });
        }

        private string MkDirIfNotExists( string dir )
        {
            if ( !System.IO.Directory.Exists( dir ) )
                System.IO.Directory.CreateDirectory( dir );
            return dir;
        }
    }
}
