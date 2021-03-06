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

using System.Collections.Generic;
using Rocket.API;
using Rocket.API.Serialisation;
using Rocket.Core;
using Rocket.Core.Permissions;
using Essentials.Common;

namespace Essentials.Core.Permission
{
    internal class EssentialsPermissionsProvider : IRocketPermissionsProvider
    {
        private readonly IRocketPermissionsProvider _defaultProvider;

        internal EssentialsPermissionsProvider()
        {
            _defaultProvider = R.Instance.GetComponent<RocketPermissionsManager>();
        }

        internal EssentialsPermissionsProvider( IRocketPermissionsProvider _defaultProvider )
        {
            this._defaultProvider = _defaultProvider;
        }

        public bool HasPermission( IRocketPlayer player, string perm, bool defaultReturnValue = false )
        {
            if ( Check( player, perm, defaultReturnValue ) )
            {
                return true;
            }
            
            return _defaultProvider.HasPermission( player, perm.ToLowerInvariant(), defaultReturnValue );
        }

        public bool HasPermission( IRocketPlayer player, IRocketCommand command, out uint? cooldownLeft, bool defaultReturnValue = false )
        {
            if ( command == null || command.Name.EqualsIgnoreCase( "essentials" ) )
            {
                cooldownLeft = null;
                return true;
            }
            
            var perm = command.Permissions.Count > 0 ? command.Permissions[0] : null;

            if ( perm != null && Check( player, perm, defaultReturnValue ) )
            {
                cooldownLeft = 0;
                return true;
            }
            
            return _defaultProvider.HasPermission( player, command, out cooldownLeft, defaultReturnValue );
        }

        public bool HasPermission( IRocketPlayer player, List<string> requestedPermissions, out uint? cooldownLeft, bool defaultReturnValue = false )
        {
            return _defaultProvider.HasPermission( player, requestedPermissions, out cooldownLeft, defaultReturnValue );
        }

        public List<RocketPermissionsGroup> GetGroups( IRocketPlayer player, bool includeParentGroups )
        {
            return _defaultProvider.GetGroups( player, includeParentGroups );
        }

        public List<Rocket.API.Serialisation.Permission> GetPermissions( IRocketPlayer player )
        {
            return _defaultProvider.GetPermissions( player );
        }

        public void Reload()
        {
            _defaultProvider.Reload();
        }
        
        private bool Check( IRocketPlayer player, string perm, bool defaultReturnValue )
        {
            perm = perm.ToLowerInvariant();
            
            if ( _defaultProvider.HasPermission( player, "*", defaultReturnValue ) )
            {
                return true;
            }

            if ( _defaultProvider.HasPermission( player, $"!{perm}", defaultReturnValue ) )
            {
                return false;
            }
            
            return false;
        }

        public RocketPermissionsProviderResult AddPlayerToGroup( string groupId, IRocketPlayer player )
        {
            return _defaultProvider.AddPlayerToGroup( groupId, player );
        }

        public RocketPermissionsProviderResult RemovePlayerFromGroup( string groupId, IRocketPlayer player )
        {
            return _defaultProvider.RemovePlayerFromGroup( groupId, player );
        }

        public RocketPermissionsGroup GetGroup( string groupId )
        {
            return _defaultProvider.GetGroup( groupId );
        }

        public RocketPermissionsProviderResult AddGroup( RocketPermissionsGroup group )
        {
            return _defaultProvider.AddGroup( group );
        }

        public RocketPermissionsProviderResult SaveGroup( RocketPermissionsGroup group )
        {
            return _defaultProvider.SaveGroup( group );
        }

        public RocketPermissionsProviderResult DeleteGroup( string groupId )
        {
            return _defaultProvider.DeleteGroup( groupId );
        }
    }
}
