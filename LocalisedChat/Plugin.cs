using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace LocalisedChat
{
	[ApiVersion(2, 1)]
	public class Plugin : TerrariaPlugin
	{
		public Config config = new Config();
		public Regex TagRegex = new Regex(@"(?<!\\)\[(?<tag>[a-zA-Z]{1,10})(\/(?<options>[^:]+))?:(?<text>.+?)(?<!\\)\]");

        public override string Author => "Quicm";

        public override string Description => "Localised chat messages";

        public override string Name => "Localised chat";


        public override Version Version => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;


        public Plugin(Main game) : base(game)
		{
			Order = 0;
		}

		public override void Initialize()
		{
			string path = Path.Combine(TShock.SavePath, "LocalChat.json");
			if (!File.Exists(path))
			{
				config.Write(path);
			}
			config.Read(path);

			ServerApi.Hooks.ServerChat.Register(this, OnChat, 5);
			//Hooks into the /reload command, so that when /reload is used the config file is reloaded
			GeneralHooks.ReloadEvent += OnReload;
		}

		private void OnReload(ReloadEventArgs e)
		{
			string path = Path.Combine(TShock.SavePath, "LocalChat.json");
			if (!File.Exists(path))
			{
				config.Write(path);
			}
			config.Read(path);
		}

		private void OnChat(ServerChatEventArgs args)
		{
			if (args.Handled)
			{
				//Do nothing if the event is already handled
				return;
			}

			if (args.Text.StartsWith(TShock.Config.CommandSilentSpecifier)
				|| args.Text.StartsWith(TShock.Config.CommandSpecifier))
			{
				//Do nothing if the text is a command
				return;
			}

			//Grab the player who sent the text
			TSPlayer p = TShock.Players[args.Who];

			if (p == null)
			{
				//Do nothing if the player doesn't actually exist
				return;
			}

			//Do a regex match to see if the text contains any chat tags
			Match match = TagRegex.Match(args.Text);

			//Declare a new string to be our altered text
			string text;
			//This is the colour the pop-up text and chat will be sent in. Default is the player's group text colour
			Color msgColor = new Color(p.Group.R, p.Group.G, p.Group.B);

			string tag = match.Groups["tag"].Value;
			if (tag != "c" && tag != "color")
			{
				//replace achievements and colours for pop-ups
				text = TagRegex.Replace(args.Text, "");
            }
			else
			{
				//Remove the tag (but leave any text contained in the tag)
				text = TagRegex.Replace(args.Text, match.Groups["text"].Value ?? "");
				//Attempt to parse a colour out of the tag
				string options = match.Groups["options"].Value;
				int num;
				if (int.TryParse(options, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out num))
				{
					msgColor = new Color(num >> 16 & 255, num >> 8 & 255, num & 255);
                }
			}

			//Send pop-up text of the chat message
			NetMessage.SendData((int)PacketTypes.CreateCombatText, -1, -1,
				Terraria.Localization.NetworkText.FromLiteral(text), (int)msgColor.PackedValue,
				p.TPlayer.position.X, p.TPlayer.position.Y + 32);

			if (config.RadiusInFeet == -1)
			{
				//-1 means everyone should receive a chat message, so return
				return;
			}

			//Re-format the text for normal chat
			text = String.Format(TShock.Config.ChatFormat, p.Group.Name,
				p.Group.Prefix, p.Name, p.Group.Suffix, args.Text);

			if (config.RadiusInFeet == 0)
			{
				//0 means no one should receive a chat message, so handle the event and return
				args.Handled = true;
				return;
			}

			//Get an enumerable object of all players in the config-defined chat range
			IEnumerable<TSPlayer> plrList = TShock.Players.Where(
					plr => plr != null &&
						 plr != p &&
						 plr.Active &&
						 Vector2.Distance(plr.TPlayer.position, p.TPlayer.position) <= (config.RadiusInFeet*8));

			//Send the message to the player who sent the message
			//Sounds silly, but it's necessary
			p.SendMessage(text, msgColor);
			//Send the message to the server (console)
			TSPlayer.Server.SendMessage(text, msgColor);

			foreach (TSPlayer plr in plrList)
			{
				//Send the message to every player who was inside the config-defined chat range
				plr.SendMessage(text, msgColor);
			}

			//Mark the event as handled, so that (hopefully) no other plugins will mess with the chat
			args.Handled = true;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
				GeneralHooks.ReloadEvent -= OnReload;
			}

			base.Dispose(disposing);
		}
	}
}
