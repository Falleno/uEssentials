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

using System.Linq;
using Essentials.Api.Command;
using Essentials.Api.Command.Source;
using Essentials.Api.Unturned;
using Essentials.I18n;
using SDG.Unturned;
using UnityEngine;

namespace Essentials.Commands
{
    [CommandInfo(
        Name = "tp",
        Usage = "[player|place|x y z] or [player] [player|place|x y z]",
        Description = "Teleportation command"
    )]
    public class CommandTp : EssCommand
    {
        public override CommandResult OnExecute( ICommandSource src, ICommandArgs args )
        {
            if ( src.IsConsole && (args.Length == 1 || args.Length == 3) )
            {
                return CommandResult.ShowUsage();
            }

            switch ( args.Length )
            {
                /*
                    /tp player  -> sender to player
                    /tp place   -> sender to place
                */
                case 1:
                    var data            = FindPlaceOrPlayer( args[0].ToString() );
                    var dataFound       = (bool) data[0];
                    var dataPosition    = (Vector3) data[1];
                    var dataName        = (string) data[2];

                    if ( !dataFound )
                    {
                        return CommandResult.Lang( EssLang.FAILED_FIND_PLACE_OR_PLAYER, args[0] );
                    }

                    src.ToPlayer().Teleport( dataPosition );
                    EssLang.TELEPORTED.SendTo( src, dataName );
                    break;

                /*
                    /tp player other   -> player to other
                    /tp player place   -> player to place
                */
                case 2:
                    var target = UPlayer.From( args[0].ToString() );

                    if ( target == null)
                    {
                        return CommandResult.Lang( EssLang.PLAYER_NOT_FOUND, args[0] );
                    }

                    data            = FindPlaceOrPlayer( args[1].ToString() );
                    dataFound       = (bool) data[0];
                    dataPosition    = (Vector3) data[1];
                    dataName        = (string) data[2];

                    if ( !dataFound )
                    {
                        return CommandResult.Lang( EssLang.FAILED_FIND_PLACE_OR_PLAYER, args[0] );
                    }

                    target.Teleport( dataPosition );
                    EssLang.TELEPORTED.SendTo( target, dataName );
                    EssLang.TELEPORTED_SENDER.SendTo( src, target, dataName );
                    break;

                /*
                    /tp x y z          -> sender to x,y,z
                */
                case 3:
                    var location = args.GetVector3( 0 );

                    if ( location.HasValue )
                    {
                        src.ToPlayer().Teleport( location.Value );
                        EssLang.TELEPORTED.SendTo( src, location );
                    }
                    else
                    {
                        return CommandResult.Lang( EssLang.INVALID_COORDS, args[0], args[1], args[2] );
                    }
                    break;

                /*
                    /tp player x y z   -> player to x, y, z
                */
                case 4:
                    target = UPlayer.From( args[0].ToString() );

                    if ( target == null )
                    {
                        return CommandResult.Lang( EssLang.PLAYER_NOT_FOUND, args[0] );
                    }

                    location = args.GetVector3( 1 );

                    if ( location.HasValue )
                    {
                        target.Teleport( location.Value );
                        EssLang.TELEPORTED.SendTo( target, location );
                        EssLang.TELEPORTED_SENDER.SendTo( src, target, location );
                    }
                    else
                    {
                        return CommandResult.Lang( EssLang.INVALID_COORDS, args[1], args[2], args[3] );
                    }
                    break;

                default:
                    return CommandResult.ShowUsage();
            }

            return CommandResult.Success();
        }

        /*
            0 - found
            1 - location
            2 - place/player name
        */
        private static object[] FindPlaceOrPlayer( string arg )
        {
                var position            = Vector3.zero;
                var placeOrPlayerName   = string.Empty;

                var found = UPlayer.TryGet( arg, p =>
                {
                    position = p.Position;
                    placeOrPlayerName = p.CharacterName;
                } );

                if ( !found )
                {
                    LocationNode node;
                    found = TryFindPlace( arg, out node );

                    if ( found )
                    {
                        placeOrPlayerName = node.Name;
                        position = node.Position + new Vector3( 0, 1, 0 );
                    }
                }

                return new object[] {found, position, placeOrPlayerName};
        }

        private static bool TryFindPlace( string name, out LocationNode outNode  )
        {
            outNode = null;

            var locationNode = (
                from node in LevelNodes.Nodes
                where node.type == ENodeType.LOCATION
                let locNode = node as LocationNode
                where locNode.Name.ToLower().Contains( name.ToLower() )
                select locNode
            ).FirstOrDefault();

            if ( locationNode == null )
                return false;

            outNode = locationNode;
            return true;
        }
    }
}
