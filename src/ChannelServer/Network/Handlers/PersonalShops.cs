﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Network.Sending;
using Aura.Channel.Util;
using Aura.Shared.Network;
using Aura.Shared.Util;
using Aura.Channel.World;
using Aura.Mabi.Const;
using Aura.Channel.World.Inventory;
using Aura.Mabi.Network;
using System;
using Aura.Channel.World.Shops;

namespace Aura.Channel.Network.Handlers
{
	public partial class ChannelServerHandlers : PacketHandlerManager<ChannelClient>
	{
		/// <summary>
		/// Sent when trying to prepare to set up a shop.
		/// </summary>
		/// <example>
		/// 001 [005000CB8F42A1E9] Long   : 22518872418722281
		/// 002 [005000CCCA71DA4D] Long   : 22518877706639949
		/// </example>
		[PacketHandler(Op.PersonalShopCheck)]
		public void PersonalShopCheck(ChannelClient client, Packet packet)
		{
			var bagEntityId = packet.GetLong();
			var licenseEntityId = packet.GetLong();

			var creature = client.GetCreatureSafe(packet.Id);
			var bag = creature.Inventory.GetItemSafe(bagEntityId);
			var license = creature.Inventory.GetItemSafe(licenseEntityId);

			// Check bag
			if (!bag.HasTag("/personal_shop_available/"))
			{
				Log.Warning("PersonalShopCheck: User '{0}' tried to use invalid item bag.", client.Account.Id);

				Send.MsgBox(creature, Localization.Get("Invalid item bag."));
				Send.PersonalShopCheckR(creature, false, 0, 0);
				return;
			}

			// Check license
			if (!license.HasTag("/personalshoplicense/"))
			{
				Log.Warning("PersonalShopCheck: User '{0}' tried to use invalid license.", client.Account.Id);

				Send.MsgBox(creature, Localization.Get("Invalid license."));
				Send.PersonalShopCheckR(creature, false, 0, 0);
				return;
			}

			// Check location
			if (!PersonalShop.CanPlace(creature, license.Data.PersonalShopLicense))
			{
				Send.MsgBox(creature, Localization.Get("Personal Shops are allowed only in designated areas."));
				Send.PersonalShopCheckR(creature, false, 0, 0);
				return;
			}

			// Create shop
			var shop = new PersonalShop(creature, bag, license);

			// Response
			Send.PersonalShopCheckR(creature, true, bagEntityId, licenseEntityId);
		}

		/// <summary>
		/// Sent when changing a shop's title.
		/// </summary>
		/// <example>
		/// 001 [................] String : test
		/// </example>
		[PacketHandler(Op.PersonalShopChangeTitle)]
		public void PersonalShopChangeTitle(ChannelClient client, Packet packet)
		{
			var title = packet.GetString();

			var creature = client.GetCreatureSafe(packet.Id);
			var shop = creature.Temp.ActivePersonalShop;

			// Check shop
			if (shop == null)
			{
				Log.Warning("PersonalShopChangeTitle: User '{0}' tried to change title of non-existent shop.", client.Account.Id);
				return;
			}

			// Check title
			if (!Math2.Between(title.Length, 0, 80))
			{
				Log.Warning("PersonalShopChangeTitle: User '{0}' tried to set invalid title '{1}'.", client.Account.Id, title);
				return;
			}

			shop.ChangeTitle(title);
		}

		/// <summary>
		/// Sent when changing a shop's description.
		/// </summary>
		/// <example>
		/// 001 [................] String : test
		/// </example>
		[PacketHandler(Op.PersonalShopChangeDescription)]
		public void PersonalShopChangeDescription(ChannelClient client, Packet packet)
		{
			var description = packet.GetString();

			var creature = client.GetCreatureSafe(packet.Id);
			var shop = creature.Temp.ActivePersonalShop;

			// Check shop
			if (shop == null)
			{
				Log.Warning("PersonalShopChangeDescription: User '{0}' tried to change description of non-existent shop.", client.Account.Id);
				return;
			}

			// Check description
			if (!Math2.Between(description.Length, 0, 80))
			{
				Log.Warning("PersonalShopChangeDescription: User '{0}' tried to set invalid description '{1}'.", client.Account.Id, description);
				return;
			}

			shop.ChangeDescription(description);
		}

		/// <summary>
		/// Sent when changing a shop's description.
		/// </summary>
		/// <example>
		/// 001 [................] String : test
		/// </example>
		[PacketHandler(Op.PersonalShopTakeDown)]
		public void PersonalShopTakeDown(ChannelClient client, Packet packet)
		{
			var byDistance = packet.GetBool();

			var creature = client.GetCreatureSafe(packet.Id);
			var shop = creature.Temp.ActivePersonalShop;

			// Check shop
			if (shop == null)
			{
				Log.Warning("PersonalShopTakeDown: User '{0}' tried to take down non-existent shop.", client.Account.Id);
				Send.PersonalShopTakeDownR(creature, false);
				return;
			}

			// Notice if the shop was closed due to being too far from it.
			if (byDistance)
				Send.MsgBox(creature, Localization.Get("The Personal Shop has been closed."));

			shop.TakeDown();

			Send.PersonalShopTakeDownR(creature, true);
		}

		/// <summary>
		/// Sent when setting up the shop, after setting the item prices.
		/// </summary>
		/// <example>
		/// 001 [................] String : title
		/// 002 [................] String : description
		/// </example>
		[PacketHandler(Op.PersonalShopSetUp)]
		public void PersonalShopSetUp(ChannelClient client, Packet packet)
		{
			var title = packet.GetString();
			var description = packet.GetString();

			var creature = client.GetCreatureSafe(packet.Id);
			var shop = creature.Temp.ActivePersonalShop;

			// Check shop
			if (shop == null)
			{
				Log.Warning("PersonalShopSetUp: User '{0}' tried to set up non-existent shop.", client.Account.Id);
				Send.PersonalShopSetUpR_Fail(creature);
				return;
			}

			var success = shop.SetUp(title, description);

			// Shouldn't happen on an unmodified client.
			if (!success)
				Send.MsgBox(creature, Localization.Get("Failed to set up shop."));

			Send.PersonalShopSetUpR(creature, shop.Prop);
		}

		/// <summary>
		/// Sent when changign the price of an item.
		/// </summary>
		/// <example>
		/// 001 [0050000000000AD5] Long   : 22517998136855253
		/// 002 [........000001F4] Int    : 500
		/// </example>
		[PacketHandler(Op.PersonalShopSetPrice)]
		public void PersonalShopSetPrice(ChannelClient client, Packet packet)
		{
			var itemEntityId = packet.GetLong();
			var price = packet.GetInt();

			var creature = client.GetCreatureSafe(packet.Id);
			var shop = creature.Temp.ActivePersonalShop;

			// Check shop
			if (shop == null)
			{
				Log.Warning("PersonalShopSetPrice: User '{0}' tried to set price for an item in a non-existent shop.", client.Account.Id);
				Send.PersonalShopTakeDownR(creature, false);
				return;
			}

			var success = shop.SetPrice(itemEntityId, price);
			if (!success)
				Log.Warning("PersonalShopSetPrice: User '{0}' tried to set price for an invalid item.", client.Account.Id);

			Send.PersonalShopSetPriceR(creature, success);
		}

		/// <summary>
		/// Sent when instructing an already spawned pet to protect the
		/// personal shop.
		/// </summary>
		/// <example>
		/// No parameters.
		/// </example>
		[PacketHandler(Op.PersonalShopPetProtectRequest)]
		public void PersonalShopPetProtectRequest(ChannelClient client, Packet packet)
		{
			var creature = client.GetCreatureSafe(packet.Id);
			var pet = creature.Pet;
			var shop = creature.Temp.ActivePersonalShop;

			// Check shop
			if (shop == null)
			{
				Log.Warning("PersonalShopPetProtectRequest: User '{0}' tried to set protection pet a non-existent shop.", client.Account.Id);
				Send.PersonalShopTakeDownR(creature, false);
				return;
			}

			// Check pet
			if (pet == null)
			{
				Log.Warning("PersonalShopPetProtectRequest: User '{0}' tried to set a non-existent pet to protect the shop.", client.Account.Id);
				Send.PersonalShopTakeDownR(creature, false);
				return;
			}

			Send.MsgBox(creature, Localization.Get("This feature is not available yet."));
			Send.PersonalShopPetProtectRequestR(creature, false);
		}
	}
}
