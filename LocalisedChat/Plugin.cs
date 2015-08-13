using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace LocalisedChat
{
	[ApiVersion(1, 21)]
	public class Plugin : TerrariaPlugin
	{
		public Config config = new Config();
		public Regex TagRegex = new Regex(@"(?<!\\)\[(?<tag>[a-zA-Z]{1,10})(\/(?<options>[^:]+))?:(?<text>.+?)(?<!\\)\]");

		public override string Author
		{
			get
			{
				return "White";
			}
		}

		public override string Description
		{
			get
			{
				return "Localised chat messages";
			}
		}

		public override string Name
		{
			get
			{
				return "Localised chat";
			}
		}

		public override Version Version
		{
			get
			{
				return new Version(1, 1);
			}
		}

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
				return;
			}

			if (args.Text.StartsWith(TShock.Config.CommandSilentSpecifier)
				|| args.Text.StartsWith(TShock.Config.CommandSpecifier))
			{
				return;
			}

			TSPlayer p = TShock.Players[args.Who];

			if (p == null)
			{
				return;
			}

			Match match = TagRegex.Match(args.Text);

			string text;
			Color msgColor = new Color(p.Group.R, p.Group.G, p.Group.B);

			string tag = match.Groups["tag"].Value;
			if (tag != "c" && tag != "color")
			{
				//replace achievements and colours for pop-ups
				text = TagRegex.Replace(args.Text, "");
            }
			else
			{
				text = TagRegex.Replace(args.Text, match.Groups["text"].Value ?? "");
				string options = match.Groups["options"].Value;
				int num;
				if (int.TryParse(options, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out num))
				{
					msgColor = new Color(num >> 16 & 255, num >> 8 & 255, num & 255);
                }
			}

			NetMessage.SendData((int)PacketTypes.CreateCombatText, -1, -1,
				text, (int)msgColor.PackedValue,
				p.TPlayer.position.X, p.TPlayer.position.Y + 32);

			if (config.RadiusInFeet == -1)
			{
				//Don't handle
				return;
			}

			text = String.Format(TShock.Config.ChatFormat, p.Group.Name,
				p.Group.Prefix, p.Name, p.Group.Suffix, args.Text);

			if (config.RadiusInFeet == 0)
			{
				args.Handled = true;
				return;
			}

			IEnumerable<TSPlayer> plrList = TShock.Players.Where(
					plr => plr != null &&
						 plr != p &&
						 plr.Active &&
						 Vector2.Distance(plr.TPlayer.position, p.TPlayer.position) <= (config.RadiusInFeet*8));


			p.SendMessage(text, msgColor);
			TSPlayer.Server.SendMessage(text, msgColor);

			foreach (TSPlayer plr in plrList)
			{
				plr.SendMessage(text, msgColor);
			}

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
