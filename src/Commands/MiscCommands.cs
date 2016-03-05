﻿/*
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
using System.Linq;
using Essentials.Api.Command;
using Essentials.Api.Command.Source;
using Essentials.Api.Task;
using Essentials.Api.Unturned;
using Essentials.Common;
using static Essentials.Common.Util.ItemUtil;
using Essentials.Core.Command;
using Essentials.Core.Components.Player;
using Essentials.I18n;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace Essentials.Commands
{
    public class MiscCommands
    {
        private static readonly ICommandArgument One = new CommandArgument( 0, "1" );
        public static readonly List<string> Spies = new List<string>();

        [CommandInfo(
            Name = "ascend",
            Aliases = new []{"asc"},
            Usage = "[amount]",
            Description = "Ascend X \"meters\".",
            AllowedSource = AllowedSource.PLAYER
        )]
        void AscendCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            if ( args.IsEmpty )
            {
                ShowUsage( src, cmd );
            }
            else if ( !args[0].IsFloat )
            {
                EssLang.INVALID_NUMBER.SendTo( src, args[0] );
            }
            else if ( args[0].ToFloat <= 0 )
            {
                EssLang.MUST_POSITIVE.SendTo( src, args[0] );
            }
            else
            {
                var player = src.ToPlayer();
                var pos = new Vector3(player.Position.x, player.Position.y, player.Position.z);
                var num = args[0].ToFloat;

                pos.y += num;

                player.Teleport( pos );
                player.SendMessage( $"You ascended {num} \"meters\"" );
            }
        }

        [CommandInfo(
            Name = "descend",
            Aliases = new[] {"desc"},
            Usage = "[amount]",
            Description = "Descend X \"meters\".",
            AllowedSource = AllowedSource.PLAYER
        )]
        void DescendCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            if ( args.IsEmpty )
            {
                ShowUsage( src, cmd );
            }
            else if ( !args[0].IsFloat )
            {
                EssLang.INVALID_NUMBER.SendTo( src, args[0] );
            }
            else if ( args[0].ToFloat <= 0 )
            {
                EssLang.MUST_POSITIVE.SendTo( src );
            }
            else
            {
                var player = src.ToPlayer();
                var pos = new Vector3( player.Position.x, player.Position.y, player.Position.z );
                var num = args[0].ToFloat;

                pos.y -= num;

                player.Teleport( pos );
                player.SendMessage( $"You descended {num} \"meters\"" );
            }
        }

        [CommandInfo(
            Name = "clear",
            Description = "Clear things",
            Usage = "i = items, v = vehicles, ev = empty vehicles"
        )]
        void ClearCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            if ( args.IsEmpty )
            {
                ShowUsage( src, cmd );
                return;
            }

            var joinedArgs = args.Join( 0 );

            Func<string, bool> hasArg = arg =>
            {
                return joinedArgs.IndexOf( arg, 0, StringComparison.InvariantCultureIgnoreCase ) != -1 ||
                        (joinedArgs.Contains( "a" ) || joinedArgs.Contains( "A" ));
            };

            /*
                TODO: Options
                    -i = items
                    -v = vehicles
                    -z = zombies
                    -b = barricades
                    -s = structures
                    -a = ALL
                
                /clear -i -z -v = items, zombies, vehicles
            */

            if ( hasArg( "i" ) )
            {
                ItemManager.askClearAllItems();
                EssLang.CLEAR_ITEMS.SendTo( src );
            }

            if ( hasArg( "v" ) )
            {
                UWorld.Vehicles.ForEach( v => {
                    for ( byte i = 0; i < v.passengers.Length; i++ )
                    {
                        if ( v.passengers[i] == null ||
                            v.passengers[i].player == null ) continue;

                        var seat = i;
                        Vector3 point;
                        byte angle;

                        v.getExit( seat, out point, out angle);
                        VehicleManager.sendExitVehicle(v, seat, (point), angle, false);

                        v.passengers[i].player = null;
                    }
                } );

                Tasks.New( t => VehicleManager.askVehicleDestroyAll() ).Delay( 200 ).Go();
                EssLang.CLEAR_VEHICLES.SendTo( src );
            }
        }

        [CommandInfo(
            Name = "item",
            Usage = "[item] <amount> or [player|* = all] [item] [amount]",
            Aliases = new []{ "i" }
        )]
        void ItemCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            switch (args.Length)
            {
                /*
                    /i [item]
                 */
                case 1:
                    if ( src.IsConsole )
                    {
                        goto usage;
                    }
                    GiveItem( src, src.ToPlayer(), args[0], One );
                    return;
                
                /*
                    /i [item] [amount]
                    /i [player] [item]
                    /i all [item]
                 */
                case 2:
                    if ( args[1].IsInt )
                    {
                        if ( src.IsConsole )
                        {
                            goto usage;
                        }
                        GiveItem( src, src.ToPlayer(), args[0], args[1] );
                    }
                    else if ( args[0].Is( "*" ) )
                    {
                        GiveItem( src, null, args[1], One, true );
                    }
                    else if ( !args[0].IsValidPlayerName )
                    {
                        EssLang.PLAYER_NOT_FOUND.SendTo( src, args[0] );
                    }
                    else
                    {
                        GiveItem( src, UPlayer.From( args[0].ToString() ), args[1], One );
                    }
                    return;
                
                /*
                    /i [player] [item] [amount]
                    /i all [item] [amount]
                 */
                case 3:
                    if ( args[0].Is( "*" ) )
                    {
                        GiveItem( src, null, args[1], args[2], true );   
                    }
                    else if ( !args[0].IsValidPlayerName )
                    {
                        EssLang.PLAYER_NOT_FOUND.SendTo( src, args[0] );
                    }
                    else
                    {
                        GiveItem( src, UPlayer.From( args[0].ToString() ), args[1], args[2] );
                    }
                    return;

                default:
                    goto usage;
            }

            usage:
            ShowUsage( src, cmd );
        }

        [CommandInfo(
            Name = "iteminfo",
            Aliases = new [] {"ii"},
            Description = "See informations about an item.",
            Usage = "<item_id>"
        )]
        void ItemInfoCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            if ( src.IsConsole && args.Length != 1 )
            {
                ShowUsage( src, cmd );
                return;
            }

            ItemAsset asset;

            if ( args.Length == 0 )
            {
                var equipament = src.ToPlayer().Equipment;

                if ( equipament.HoldingItemID == 0 )
                {
                    EssLang.EMPTY_HANDS.SendTo( src );
                }

                asset = equipament.asset;
            }
            else
            {
                if ( !args[0].IsUshort ||
                    (asset = Assets.find( EAssetType.ITEM, args[0].ToUshort ) as ItemAsset) == null)
                {
                    EssLang.INVALID_ITEM_ID.SendTo( src, args[0] );
                    return;
                }
            }

            var color       = Color.yellow;
            var name        = WrapMessage( src, asset.name );
            var description = WrapMessage( src, asset.Description );
            var type        = WrapMessage( src, asset.ItemType.ToString() );
            var isPro       = WrapMessage( src, asset.isPro.ToString() );


            src.SendMessage( $"Name: {name}", color );
            src.SendMessage( $"Description: {description}", color );
            src.SendMessage( $"Id: {asset.id}", color );
            src.SendMessage( $"Type: {type}", color );
            src.SendMessage( $"IsPro: {isPro}", color );
        }

        [CommandInfo(
            Name = "itemfeatures",
            Aliases = new []{ "if" },
            Usage = "[autoreload | autorepair] [on|off]",
            Description = "Item features",
            AllowedSource = AllowedSource.PLAYER
        )]
        void ItemFeaturesCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            if ( args.Length != 2 )
            {
                goto usage;
            }

            bool toggleValue;

            if ( args[1].IsOneOf( new[] { "1", "on", "true" } ) )
            {
                toggleValue = true;
            }
            else if ( args[1].IsOneOf( new[] { "0", "off", "false" } ) )
            {
                toggleValue = false;
            }
            else
            {
                goto usage;
            }

            var player = src.ToPlayer();
            var component = player.GetComponent<ItemFeatures>() ?? player.AddComponent<ItemFeatures>();

            switch (args[0].ToLowerString)
            {
                case "autoreload":
                    if ( toggleValue )
                    {
                        component.AutoReload = true;
                        EssLang.AUTO_RELOAD_ENABLED.SendTo( src );
                    }
                    else
                    {
                        component.AutoReload = false;
                        EssLang.AUTO_RELOAD_DISABLED.SendTo( src );
                    }
                    return;

                case "autorepair":
                    if ( toggleValue )
                    {
                        component.AutoRepair = true;
                        EssLang.AUTO_REPAIR_ENABLED.SendTo( src );
                    }
                    else
                    {
                        component.AutoRepair = false;
                        EssLang.AUTO_REPAIR_DISABLED.SendTo( src );
                    }
                    return;
                
                default:
                    goto usage;
            }
            
            usage:
            ShowUsage( src, cmd );
        }

        [CommandInfo(
            Name = "vehiclefeatures",
            Aliases = new []{ "vehfeatures", "vf" },
            Usage = "[autorefuel | autorepair] [on|off]",
            Description = "Vehicle features",
            AllowedSource = AllowedSource.PLAYER
        )]
        void VehicleFeaturesCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            if ( args.Length != 2 )
            {
                goto usage;
            }

            bool toggleValue;

            if ( args[1].IsOneOf( new[] { "1", "on", "true" } ) )
            {
                toggleValue = true;
            }
            else if ( args[1].IsOneOf( new[] { "0", "off", "false" } ) )
            {
                toggleValue = false;
            }
            else
            {
                goto usage;
            }
            
            var player = src.ToPlayer();
            var component = player.GetComponent<PlayerVehicleFeatures>() ?? player.AddComponent<PlayerVehicleFeatures>();

            switch (args[0].ToLowerString)
            {
                case "autorefuel":
                    if ( toggleValue )
                    {
                        component.AutoRefuel = true;
                        EssLang.AUTO_REFUEL_ENABLED.SendTo( src );
                    }
                    else
                    {
                        component.AutoRefuel = false;
                        EssLang.AUTO_REFUEL_DISABLED.SendTo( src );
                    }
                    return;

                case "autorepair":
                    if ( toggleValue )
                    {
                        component.AutoRepair = true;
                        EssLang.AUTO_REPAIR_ENABLED.SendTo( src );
                    }
                    else
                    {
                        component.AutoRepair = false;
                        EssLang.AUTO_REPAIR_DISABLED.SendTo( src );
                    }
                    return;
                
                default:
                    goto usage;
            }
            
            usage:
            ShowUsage( src, cmd );
        }

        [CommandInfo(
            Name = "spy",
            Description = "Toggle spy mode",
            AllowedSource = AllowedSource.PLAYER
        )]
        void SpyCommand( ICommandSource src, ICommandArgs args )
        {
            var displayName = src.DisplayName;

            if ( Spies.Contains( displayName ) )
            {
                Spies.Remove( displayName );
                EssLang.SPY_MODE_OFF.SendTo( src );
            }
            else
            {
                Spies.Add( displayName );
                EssLang.SPY_MODE_ON.SendTo( src );
            }
        }

        [CommandInfo(
            Name = "suicide",
            Description = "Kill yourself",
            AllowedSource = AllowedSource.PLAYER
        )]
        void SuicideCommand( ICommandSource src, ICommandArgs args )
        {
            src.ToPlayer().Suicide();
        }

        [CommandInfo(
            Name = "position",
            Aliases = new[] {"pos", "coords"},
            Description = "View your/another player position.",
            Usage = "<player>"
        )]
        void PositionCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            if ( args.Length == 0 )
            {
                if ( src.IsConsole )
                {
                    ShowUsage( src, cmd );
                }
                else
                {
                    var player = src.ToPlayer();

                    EssLang.POSITION.SendTo( player, 
                                            player.Position.x,
                                            player.Position.y, 
                                            player.Position.z );
                }
            }
            else
            {
                var found = UPlayer.TryGet( args[0], p => {
                    EssLang.POSITION_OTHER.SendTo( src, p.DisplayName, 
                                                   p.Position.x, p.Position.y, p.Position.z );
                } );

                if ( !found )
                {
                    EssLang.PLAYER_NOT_FOUND.SendTo( src, args[0] );
                } 
            }   
        }

        [CommandInfo(
            Name = "online",
            Description = "View the number of online players"
        )]
        void OnlineCommand( ICommandSource src, ICommandArgs args, ICommand cmd )
        {
            EssLang.ONLINE_PLAYERS.SendTo( src, UServer.Players.Count(), UServer.MaxPlayers );
        }

        [CommandInfo(
            Name = "respawnitems",
            Description = "Respawn all items."
        )]
        void RespawnItemsCommand( ICommandSource src, ICommandArgs args )
        {
            for ( byte b = 0; b < Regions.WORLD_SIZE; b += 1 )
            {
                for ( byte b2 = 0; b2 < Regions.WORLD_SIZE; b2 += 1 )
                {
                    if ( LevelItems.spawns[b, b2].Count <= 0 ) continue;

                    for ( var i = 0; i < LevelItems.spawns[b, b2].Count; i++ )
                    {
                        var itemSpawnpoint = LevelItems.spawns[b, b2][i];
                        var item = LevelItems.getItem( itemSpawnpoint );

                        if ( item == 0 ) continue;

                        var item2 = new Item( item, true );
                        ItemManager.dropItem( item2, itemSpawnpoint.point, false, false, false );
                    }
                }
            }

            EssLang.RESPAWNED_ITEMS.SendTo( src );
        }

        [CommandInfo(
            Name = "respawnvehicles",
            Description = "Respawn all vehicles."
        )]
        void RespawnVehiclesCommand( ICommandSource src, ICommandArgs args )
        {
            var spawns = LevelVehicles.spawns;
            for ( var j = 0; j < spawns.Count; j++ )
            {
                var vehicleSpawnpoint = spawns[j];
                var vehicle = LevelVehicles.getVehicle( vehicleSpawnpoint );

                if ( vehicle == 0 ) continue;

                var point = vehicleSpawnpoint.point;
                point.y += 1f;
                VehicleManager.spawnVehicle( vehicle, point, Quaternion.Euler( 0f, vehicleSpawnpoint.angle, 0f ) );
            }

            EssLang.RESPAWNED_VEHICLES.SendTo( src );
        }

        [CommandInfo(
            Name = "shutdown",
            Aliases = new[] {"stop"},
            Description = "Shutdown server",
            Usage = "<reason>"
        )]
        void ShutdownCommand( ICommandSource src, ICommandArgs args )
        {
            if ( !args.IsEmpty )
            {
                Commander.execute( CSteamID.Nil, "kickall " + args.Join(0) );
            }
            
            Provider.shutdown();
        }


        # region HELPER METHODS

        private static void ShowUsage( ICommandSource src, ICommand cmd )
        {
            src.SendMessage( $"Use /{cmd.Name} {cmd.Usage}" );
        }

        private static string WrapMessage( ICommandSource src, string str )
        {
            if ( str == null )
                return "null";

            if ( str.Length < 90 || src.IsConsole )
		        return str;
		
	        return str.Substring(0, 90 - 3) + "...";
        }

        private static void GiveItem( ICommandSource src, UPlayer target, ICommandArgument itemArg, 
                                      ICommandArgument amountArg, bool allPlayers = false )
        {
            var optAsset = GetItem( itemArg.ToString() );

            if ( optAsset.IsAbsent )
            {
                EssLang.ITEM_NOT_FOUND.SendTo( src, itemArg );
                return;
            }

            ushort amt = 1;

            if ( amountArg != null )
            {
                if ( !amountArg.IsShort )
                {
                    EssLang.INVALID_NUMBER.SendTo( src, amountArg );
                }
                else if ( amountArg.ToUshort <= 0 )
                {
                    EssLang.MUST_POSITIVE.SendTo( src );
                }
                else
                {
                    amt = amountArg.ToUshort;
                    goto give;
                }
                return;
            }

            give:
            var asset = optAsset.Value;
            var playersToReceive = new List<UPlayer>();
            var item = new Item( asset.id, true );

            if ( asset is ItemFuelAsset )
            {
                item.Metadata[0] = 1;
            }

            if ( allPlayers )
            {
                UServer.Players.ForEach( playersToReceive.Add );
                EssLang.GIVEN_ITEM_ALL.SendTo( src, amt, asset.Name, asset.Id );
            }
            else
            {
                playersToReceive.Add( target );
                
                if ( !src.IsConsole && src.ToPlayer() == target ) 
                {
                    goto give2;
                }

                EssLang.GIVEN_ITEM.SendTo( src, amt, asset.Name, asset.Id, target.CharacterName );
            }

            give2:
            playersToReceive.ForEach( p =>
            {
                var success = p.GiveItem( item, amt, true );

                EssLang.RECEIVED_ITEM.SendTo( p, amt, asset.Name, asset.Id );

                if ( !success )
                {
                    EssLang.INVENTORY_FULL.SendTo( p );
                }
            } );
        }

        #endregion
    }
}
