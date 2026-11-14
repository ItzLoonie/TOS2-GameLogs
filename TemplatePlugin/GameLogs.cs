using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BMG.Pooling;
using Cinematics.Players;
using Game.Chat;
using Game.Interface;
using Game.Simulation;
using HarmonyLib;
using Home.HomeScene;
using Home.Shared;
using Server.Shared.Extensions;
using Server.Shared.Info;
using Server.Shared.Messages;
using Server.Shared.State;
using Server.Shared.State.Chat;
using Services;
using UnityEngine;

namespace TemplatePlugin;

public class GameLogs
{
	public static List<string> Logs = new List<string>();

	public static Dictionary<int, string> LastWills = new Dictionary<int, string>();

	public static List<WhoDiedAndHowPhaseMessage> DeathNotes = new List<WhoDiedAndHowPhaseMessage>();

	public static Dictionary<int, KillRecord> KillRecords = new Dictionary<int, KillRecord>();

	public static RoleDeckBuilder DeckBuilder;

	public static List<PoolItem> LogsThatArentErased = new List<PoolItem>();

	public static Dictionary<string, string> PlayerAccountNames = new Dictionary<string, string>();

	public static Role MyRole = (Role)0;

	public class Decoder
	{
		public static string DecodeNames(string x, List<PreviousGameEntry> a)
		{
			if (a != null)
			{
				foreach (PreviousGameEntry e in a)
				{
					x = x.Replace($"[[@{e.playerPosition + 1}]]", "<b>" + e.gameName + "</b>");
				}
			}
			return x;
		}
	}

	[HarmonyPatch(typeof(HomeSceneController), "Start")]
	public class ResetLogs
	{
		[HarmonyPostfix]
		public static void Postfix(HomeSceneController __instance)
		{
			Logs.Clear();
			LogsThatArentErased.Clear();
			GameMessageDecoderPatch.ProcessedMessages.Clear();
		}
	}

	[HarmonyPatch(typeof(RoleRevealCinematicPlayer), "HandleOnMyIdentityChanged")]
	public class SavePlayers
	{
		[HarmonyPostfix]
		public static void Postfix(RoleRevealCinematicPlayer __instance)
		{
			PlayerAccountNames.Clear();
			foreach (DiscussionPlayerObservation discussionPlayer in Service.Game.Sim.info.discussionPlayers)
			{
				PlayerAccountNames[discussionPlayer.Data.gameName] = discussionPlayer.Data.accountName;
			}
			MyRole = Pepper.GetMyRole();
		}
	}

	[HarmonyPatch(typeof(GameSimulation), "HandleGameResults")]
	public class GameResults
	{
		[HarmonyPostfix]
		public static void Postfix(GameSimulation __instance, GameResultsMessage message)
		{
			PooledChatController chatController = UnityEngine.Object.FindObjectOfType<PooledChatController>();
			LastWills = Service.Game.Sim.info.simulation.playerLastWills.ToDictionary((KeyValuePair<int, string> will) => will.Key, (KeyValuePair<int, string> will) => chatController.mentionPanel.mentionsProvider.DecodeText(will.Value));
			DeathNotes = Service.Game.Sim.simulation.whoDiedAndHowMessages.ToList();
			// Best-effort: a failure here must not cost the wills above.
			try
			{
				KillRecords = new Dictionary<int, KillRecord>();
				foreach (KillRecord record in __instance.killRecords.Get())
				{
					if (record.playerId != KillRecord.NONE)
					{
						KillRecords[(int)record.playerId] = record;
					}
				}
			}
			catch
			{
				KillRecords = new Dictionary<int, KillRecord>();
			}
			try
			{
				DeckBuilder = Service.Game.Sim.info.roleDeckBuilder.Data;
			}
			catch
			{
				DeckBuilder = null;
			}
		}
	}

	[HarmonyPatch(typeof(BaseChatPoolItem), "Activate")]
	public class GameMessageDecoderPatch
	{
		public static HashSet<int> ProcessedMessages = new HashSet<int>();

		private const int JailorSpeakerId = 50;
		private const int JurySpeakerId = 69;
		private const int CourtSpeakerId = 70;
		private const int PirateSpeakerId = 71;

		[HarmonyPostfix]
		public static void Postfix(BaseChatPoolItem __instance, PoolItemData data, ItemPoolEvents events, int laneId)
		{
			if (Pepper.IsLobbyPhase())
			{
				return;
			}
			ChatLogEntryBase chatLogEntry = __instance.chatLogMessage.chatLogEntry;
			ChatItemData chatItemData = data as ChatItemData;
			int hashCode = chatLogEntry.GetHashCode();
			if (chatItemData != null)
			{
				int itemTypeKey = (int)chatItemData.chatItemType;
				switch (chatItemData.chatItemType)
				{
				case ChatItemType.TargetSelectionLogItem:
					itemTypeKey = (int)ChatItemType.TargetSelectionItem;
					break;
				case ChatItemType.FinalTrialVoteLogItem:
					itemTypeKey = (int)ChatItemType.FinalTrialVoteItem;
					break;
				case ChatItemType.JudgmentVoteLogItem:
					itemTypeKey = (int)ChatItemType.JudgmentVoteItem;
					break;
				case ChatItemType.TrialVerdictLogItem:
					itemTypeKey = (int)ChatItemType.TrialVerdictItem;
					break;
				case ChatItemType.CustomTextLogItem:
					itemTypeKey = (int)ChatItemType.CustomTextItem;
					break;
				}
				hashCode = (hashCode * 31) + itemTypeKey;
			}
			if (ProcessedMessages.Contains(hashCode))
			{
				return;
			}
			if (chatLogEntry.type == ChatType.CHAT)
			{
				ChatLogChatMessageEntry chatMessage = (ChatLogChatMessageEntry)chatLogEntry;
				string text = __instance.mentionPanel.mentionsProvider.DecodeText(chatMessage.message);
				int position = chatMessage.speakerId;
				if (position >= 0 && position <= 14)
				{
					string gameName = Service.Game.Sim.info.discussionPlayers.Find((DiscussionPlayerObservation x) => x.Data.position == position).Data.gameName;
					string rawGameName = gameName;
					gameName = gameName.Replace(" ", "-");
					if (chatMessage.speakerWasAlive)
					{
						Logs.Add("<span><br>[" + (position + 1) + "]</span>");
						Logs.Add("<span class=\"" + gameName + "\">" + rawGameName + "</span>");
						Logs.Add("<span>: " + text + "</span>");
					}
					else
					{
						Logs.Add("<span style=\"color:#689194\"><br><i>[" + (position + 1) + "]</span>");
						Logs.Add("<span style=\"color:#689194\">" + gameName + "</span>");
						Logs.Add("<span style=\"color:#689194\">: " + text + "</i></span>");
					}
				}
				else
				{
					string text2 = "";
					switch (position)
					{
					case JailorSpeakerId:
						text2 = "<b><color=#FFBE00>Jailor:</color></b> ";
						text = "<color=#FFBE00>" + text + "</color>";
						break;
					case JurySpeakerId:
						text2 = "<b>Jury:</b> ";
						break;
					case CourtSpeakerId:
						text2 = "<b><color=#C77364>Court:</color></b> ";
						text = "<color=#ffff00>" + text + "</color>";
						break;
					case PirateSpeakerId:
						text2 = "<b>Pirate:</b> ";
						break;
					}
					Logs.Add(string.Concat("<span style=\"background-color:#", "000000", "; color:#", ColorUtility.ToHtmlStringRGB(Color.white), "\"><br>", text2 + text, "</span>"));
				}
			}
			else
			{
				Logs.Add("<span class=\"sysline\" style=\"background-color:#" + ColorUtility.ToHtmlStringRGB(__instance._chatBackgroundColor) + "; color:#" + ColorUtility.ToHtmlStringRGB(__instance._chatColor) + "\"><br>" + chatItemData.decodedText + "</span>");
			}
			ProcessedMessages.Add(hashCode);
		}
	}

	[HarmonyPatch(typeof(HudLobbyPreviousGamePanel), "OnPreviousGameInfoChanged")]
	public class SaveHTMLFile
	{
		public static List<PreviousGameEntry> PreviousEntries = new List<PreviousGameEntry>();

		public static List<string> GameData = new List<string>();

		[HarmonyPostfix]
		public static async void Postfix(PreviousGameData previousGameData)
		{
			if (Logs.Count == 0)
			{
				return;
			}
			try
			{
				string filename = Path.Combine(Main.FolderPath, string.Format("{0}{1}-{2:yyyy-MM-dd}-{3}.html", GameTypeName(), RoleExtensions.GetRoleData(MyRole).roleName, DateTime.Now, DateTime.Now.ToString("t").Replace(":", "-")));
				string css = Embed.GetEmbeddedText("TemplatePlugin.resources.GameLogCSS.txt");
				GameData.Add($"<!--DOWNLOAD AND OPEN HTML FILE TO VIEW LOG-->\n<html>\n<head>\n<title>{GameTypeName()}</title>\n<meta charset=\"UTF-8\">\n</head>\n{css}\n<body style=\"background-color:#050505; font-family:calibri; color:#FFFFFF; font-size:22;\">\n<span style=\"color:#000000;background-color:#FFFFFF;font-size:22;\"><b>PLAYER INFO</b></span>\n<p></p>");
				GameData.Add("<span style =\"border:1px solid #a0a0a0; background-color:#a0a0a0; color:#000000\"><b>This game log generated using the GameLogs mod by TubaAntics and Curtis. You can install it through <a href=\"https://discord.gg/AdpRqzstfj\" target=\"_blank\">Salem Mod Loader</a>!</b><br></span>\r\n<span style=\"border:1px solid #a0a0a0; background-color:#a0a0a0; color:#000000\"><b>These logs are very easy to modify and should not be taken as 100% proof of events.</b></span>");
				string wfColor = ColorUtility.ToHtmlStringRGB(ExtensionMethods.ParseColor(ClientRoleExtensions.GetFactionColor(previousGameData.winningFaction)));
				string titleInner = "<span style=\"font-size:26px\"><b>Winning Faction: " + BuildRoleNameHtml(previousGameData.winningFaction.ToDisplayString(), previousGameData.winningFaction, wfColor, FancyGradients.TryGetGradient(previousGameData.winningFaction, Role.NONE)) + "</b></span>";
				string deckHtml = DeckTooltipHtml();
				GameData.Add(deckHtml != null
					? "<br><span><div class=\"tooltipdeck\">" + titleInner + "<span class=\"tooltiptext\">" + deckHtml + "</span></div></span>"
					: "<br>" + titleInner);
				foreach (PreviousGameEntry entry in previousGameData.entries)
				{
					string gameName = entry.gameName;
					int pos = entry.playerPosition + 1;
					string roleName = RoleExtensions.GetRoleData(entry.role).roleName;
					string roleColor = ColorUtility.ToHtmlStringRGB(ExtensionMethods.ParseColor(ClientRoleExtensions.GetFactionColor(entry.factionType)));
					Gradient fancyGradient = FancyGradients.TryGetGradient(entry.factionType, entry.role);
					string paintColor = fancyGradient != null ? ColorUtility.ToHtmlStringRGB(fancyGradient.colorKeys[0].color) : roleColor;
					string accountName = ((entry.accountName != "") ? entry.accountName : PlayerAccountNames[entry.gameName]);
					string ogRoleName = RoleExtensions.GetRoleData(entry.originalRole).roleName;
					string ogRoleColor = ColorUtility.ToHtmlStringRGB(ExtensionMethods.ParseColor(ClientRoleExtensions.GetFactionColor(entry.factionType)));
					Gradient ogFancyGradient = FancyGradients.TryGetGradient(entry.factionType, entry.originalRole);
					string ogPaintColor = ogFancyGradient != null ? ColorUtility.ToHtmlStringRGB(ogFancyGradient.colorKeys[0].color) : ogRoleColor;
					string lastWill = (LastWills.ContainsKey(entry.playerPosition) ? Decoder.DecodeNames(DictionaryExtensions.GetValue<int, string>((IDictionary<int, string>)LastWills, entry.playerPosition, (string)null), previousGameData.entries) : "");
					lastWill = TranslateTmpToHtml(lastWill);
					lastWill = lastWill.Replace("\r\n", "\n").Replace("\n", "<br>");
					string nameHtml = OutcomeNameHtml(gameName, entry);
					if (KillRecords.TryGetValue(entry.playerPosition, out KillRecord killRecord))
					{
						nameHtml = "<span><div class=\"tooltipdeathinfo\">" + nameHtml + "<span class=\"tooltiptext\">" + DeathInfoHtml(killRecord) + "</span></div></span>";
					}
					string roleHtml = "<span><div class=\"tooltipfaction\">" + BuildRoleNameHtml(roleName, entry.factionType, paintColor, fancyGradient) + "<span class=\"tooltiptext\">" + BuildRoleNameHtml(entry.factionType.ToDisplayString(), entry.factionType, paintColor, fancyGradient) + "</span></div></span>";
					GameData.Add($"<span><br>[{pos}] {nameHtml} - </span>{roleHtml}");
					if (entry.modifier == Role.VIP)
					{
						GameData.Add(" " + BuildRoleNameHtml("(VIP)", entry.factionType, paintColor, fancyGradient));
					}
					if (entry.modifier == Role.TOWN_TRAITOR)
					{
						GameData.Add(" " + BuildRoleNameHtml("(Town Traitor)", entry.factionType, paintColor, fancyGradient));
					}
					if (roleName != ogRoleName)
					{
						GameData.Add("<span><div class=\"tooltipprev\"> ⎗ <span class=\"tooltiptext\">" + BuildRoleNameHtml(ogRoleName, entry.factionType, ogPaintColor, ogFancyGradient) + "</span></span></div>");
					}
					if (lastWill != "")
					{
						GameData.Add("<span><div class=\"tooltipwill\"> \ud83d\udcdc <span class=\"tooltiptext\">" + lastWill + "</span></div></span>");
					}
					if (DeathNotes.Exists((WhoDiedAndHowPhaseMessage w) => w.playerNumber == entry.playerPosition && w.subphase == WhoDiedAndHowSubphase.DeathNote))
					{
						string deathnote = DeathNotes.Find((WhoDiedAndHowPhaseMessage w) => w.playerNumber == entry.playerPosition && w.subphase == WhoDiedAndHowSubphase.DeathNote).extraData.Trim();
						if (deathnote != string.Empty)
						{
							deathnote = deathnote.Replace("\r\n", "\n").Replace("\n", "<br>");
							GameData.Add("<span><div class=\"tooltipdeath\"><span style=\"color:#DD0000\"> \ud83d\udd71 </span><span class=\"tooltiptext\">" + deathnote + "</span></div></span>");
						}
					}
					GameData.Add("<span style=\"color:#a0a0a0\"><i>(Username: " + accountName + ")</i></span>");
					gameName = gameName.Replace(" ", "-");
					GameData.Add(ChatNameStyleHtml(gameName, entry.factionType, paintColor, fancyGradient));
				}
				PreviousEntries = previousGameData.entries.ToList();
				StringBuilder tobewritten = new StringBuilder();
				for (int i = 0; i < GameData.Count; i++)
				{
					tobewritten.Append(GameData[i] + Environment.NewLine);
				}
				tobewritten.Append("<div class=\"chatlog\">" + Environment.NewLine);
				foreach (string sp in Logs)
				{
					tobewritten.Append(Decoder.DecodeNames(TranslateTmpToHtml(sp), previousGameData.entries) + Environment.NewLine);
				}
				tobewritten.Append("</div>" + Environment.NewLine);
				tobewritten.Append("</body>\n</html>");
				Console.WriteLine(string.IsNullOrEmpty(tobewritten.ToString()));
				await File.WriteAllTextAsync(filename, tobewritten.ToString());
				Logs.Clear();
				LastWills.Clear();
				DeathNotes.Clear();
				KillRecords.Clear();
				DeckBuilder = null;
				LogsThatArentErased.Clear();
				GameData.Clear();
				PlayerAccountNames.Clear();
				GameMessageDecoderPatch.ProcessedMessages.Clear();
			}
			catch (Exception ex)
			{
				// Don't throw out of an async-void postfix; keep the recorded
				// logs, drop only the scratch buffer so a retry can't duplicate it.
				Console.WriteLine("[GameLogs] Failed to save game log: " + ex);
				GameData.Clear();
			}
		}

		private static readonly Regex TmpStyleOpen = new Regex("<style=\"?(?<n>[A-Za-z0-9_]+)\"?>");
		private static readonly Regex TmpColorOpen = new Regex("<color=\"?(?<c>#[0-9A-Fa-f]{6,8}|[a-zA-Z]+)\"?>");
		private static readonly Regex TmpShortColorOpen = new Regex("<#(?<c>[0-9A-Fa-f]{6})([0-9A-Fa-f]{2})?>");
		private static readonly Regex TmpTagStrip = new Regex("</?(link|font|size|gradient)[^>]*>|<sprite[^>]*>");
		private static readonly Regex TmpRoleMention = new Regex("<link=\"r(?<role>\\d+)(?:,(?<faction>\\d+))?\">(?<inner>.*?)</link>");
		private static readonly Regex TmpKeywordMention = new Regex("<link=\"k(?<id>\\d+)\">(?<inner>.*?)</link>");
		private static readonly Regex TmpAchievementMention = new Regex("<link=\"~(?<id>\\d+)\">(?<inner>.*?)</link>");
		private static readonly Regex TmpPlayerMention = new Regex("<link=\"[pa](?<pos>\\d+)\">(?<inner>.*?)</link>");
		private static readonly Regex TmpJudgeColor = new Regex("<color=#C77364>", RegexOptions.IgnoreCase);
		private static readonly Regex TmpAnyTag = new Regex("<[^>]*>");

		private static string TranslateTmpToHtml(string text)
		{
			if (string.IsNullOrEmpty(text) || !text.Contains("<"))
			{
				return text;
			}
			if (!Service.Home.UserService.Settings.MentionsUseColorsEnabled)
			{
				return text;
			}
			text = TmpRoleMention.Replace(text, (Match m) =>
			{
				int roleId;
				if (!int.TryParse(m.Groups["role"].Value, out roleId))
				{
					return m.Value;
				}
				Role role = (Role)roleId;
				FactionType faction = role.GetFaction();
				int factionId;
				if (m.Groups["faction"].Success && int.TryParse(m.Groups["faction"].Value, out factionId) && !(faction = (FactionType)factionId).IsInvalid())
				{
					// faction from the mention token
				}
				else
				{
					faction = role.GetFaction();
				}
				Gradient fancy = FancyGradients.TryGetGradient(faction, role);
				if (fancy == null)
				{
					return m.Value;
				}
				string name = TmpAnyTag.Replace(m.Groups["inner"].Value, "");
				return "<b><span style=\"" + FancyGradients.ToCssStyle(fancy) + "\">" + name + "</span></b>";
			});
			text = TmpKeywordMention.Replace(text, (Match m) =>
			{
				string inner = m.Groups["inner"].Value;
				if (!inner.Contains("<gradient=") && !inner.Contains("<#"))
				{
					return m.Value;
				}
				string keywordStyle = FancyGradients.TryGetKeywordStyle();
				if (keywordStyle == null)
				{
					return m.Value;
				}
				string name = TmpAnyTag.Replace(inner, "");
				return "<b><span style=\"" + keywordStyle + "\">" + name + "</span></b>";
			});
			text = TmpAchievementMention.Replace(text, (Match m) =>
			{
				string inner = m.Groups["inner"].Value;
				if (!inner.Contains("<gradient=") && !inner.Contains("<#"))
				{
					return m.Value;
				}
				string achievementStyle = FancyGradients.TryGetAchievementStyle();
				if (achievementStyle == null)
				{
					return m.Value;
				}
				string name = TmpAnyTag.Replace(inner, "");
				return "<b><span style=\"" + achievementStyle + "\">" + name + "</span></b>";
			});
			text = TmpPlayerMention.Replace(text, (Match m) =>
			{
				string inner = m.Groups["inner"].Value;
				if (!inner.Contains("<gradient=") && !inner.Contains("<#"))
				{
					return m.Value;
				}
				string mentionStyle = FancyGradients.TryGetMentionStyle();
				if (mentionStyle == null)
				{
					return m.Value;
				}
				string name = TmpAnyTag.Replace(inner, "");
				return "<b><span style=\"" + mentionStyle + "\">" + name + "</span></b>";
			});
			text = TmpStyleOpen.Replace(text, (Match m) =>
			{
				string name = m.Groups["n"].Value;
				if (name == "VampireColor")
				{
					return "<span style=\"" + GradientStyleFor(FactionType.VAMPIRE) + "\">";
				}
				if (name == "CursedSoulColor")
				{
					return "<span style=\"" + GradientStyleFor(FactionType.CURSED_SOUL) + "\">";
				}
				return "<span>";
			});
			text = text.Replace("</style>", "</span>");
			Gradient judgeGradient = FancyGradients.TryGetGradient((FactionType)38, Role.NONE);
			if (judgeGradient != null)
			{
				text = TmpJudgeColor.Replace(text, "<span style=\"" + FancyGradients.ToCssStyle(judgeGradient) + "\">");
			}
			text = TmpColorOpen.Replace(text, "<span style=\"color:${c}\">");
			text = TmpShortColorOpen.Replace(text, "<span style=\"color:#${c}\">");
			text = text.Replace("</color>", "</span>");
			text = TmpTagStrip.Replace(text, "");
			return text;
		}

		private static string OutcomeNameHtml(string gameName, PreviousGameEntry entry)
		{
			if (entry.won)
			{
				return "<span style=\"color:#" + (entry.alive ? "FFBE00" : "8C6800") + "\">" + gameName + "</span>";
			}
			if (!entry.alive)
			{
				return "<span style=\"color:#8C8C8C\">" + gameName + "</span>";
			}
			return gameName;
		}

		// BTOS2 casual name instead of just "100"
		private static string GameTypeName()
		{
			var gameType = Pepper.GetCurrentGameMode().gameType;
			return (int)gameType == 100 ? "CasualBTOS2" : gameType.ToString();
		}

		private static string DeathInfoHtml(KillRecord record)
		{
			bool leftTown = record.killedByReasons.Contains(KilledByReason.LEAVING_TOWN);
			string headerKey = leftTown
				? (record.isDay ? "GUI_GAME_LEFT_TOWN_DAY_X" : "GUI_GAME_LEFT_TOWN_NIGHT_X")
				: (record.isDay ? "GUI_GAME_DIED_DAY_X" : "GUI_GAME_DIED_NIGHT_X");
			string header = Service.Home.LocalizationService.GetLocalizedString(headerKey);
			header = (string.IsNullOrEmpty(header) || header.StartsWith("GUI_GAME_"))
				? (leftTown ? "Left town " : "Died ") + (record.isDay ? "day " : "night ") + record.dayNumber
				: header.Replace("%x%", record.dayNumber.ToString());
			List<string> lines = new List<string> { header };
			List<KilledByReason> reasons = record.killedByReasons.FindAll((KilledByReason r) => r != KilledByReason.NONE);
			if (reasons.Count == 0)
			{
				reasons.Add(KilledByReason.NONE);
			}
			for (int i = 0; i < reasons.Count; i++)
			{
				KilledByReason reason = reasons[i];
				string text = Service.Home.LocalizationService.GetLocalizedString((i == 0 ? "GUI_GAME_KILLED_BY_MENTION_" : "GUI_GAME_ALSO_KILLED_BY_MENTION_") + (int)reason);
				if (string.IsNullOrEmpty(text) || text.StartsWith("GUI_GAME_"))
				{
					text = reason.ToString();
				}
				text = Regex.Replace(text, "\\[\\[#(\\d+)\\]\\]", (Match m) =>
				{
					Role mentionedRole = (Role)int.Parse(m.Groups[1].Value);
					FactionType mentionedFaction = mentionedRole.GetFaction();
					string mentionedColor = ColorUtility.ToHtmlStringRGB(ExtensionMethods.ParseColor(ClientRoleExtensions.GetFactionColor(mentionedFaction)));
					return BuildRoleNameHtml(RoleExtensions.GetRoleData(mentionedRole).roleName, mentionedFaction, mentionedColor, FancyGradients.TryGetGradient(mentionedFaction, mentionedRole), false);
				});
				lines.Add(text);
			}
			return string.Join("<br>", lines);
		}

		private static string DeckTooltipHtml()
		{
			if (DeckBuilder == null)
			{
				return null;
			}
			List<string> columns = new List<string>();
			List<string> roles = new List<string>();
			foreach (RoleDeckSlot slot in DeckBuilder.GetSortedRoleDeckSlots())
			{
				roles.Add(DeckSlotHtml(slot));
			}
			if (roles.Count > 0)
			{
				columns.Add(DeckColumnHtml("Roles", roles));
			}
			List<string> modifiers = new List<string>();
			foreach (RoleDeckSlot slot in DeckBuilder.GetSortedModifierDeckSlots())
			{
				modifiers.Add(DeckModifierHtml(slot));
			}
			if (modifiers.Count > 0)
			{
				columns.Add(DeckColumnHtml("Modifiers", modifiers));
			}
			List<string> bans = new List<string>();
			foreach (RoleDeckSlot slot in DeckBuilder.GetSortedBannedDeckSlots())
			{
				bans.Add(DeckSlotHtml(slot));
			}
			if (bans.Count > 0)
			{
				columns.Add(DeckColumnHtml("Bans", bans));
			}
			return columns.Count > 0 ? "<div class=\"deck-columns\">" + string.Join("", columns) + "</div>" : null;
		}

		private static string DeckColumnHtml(string title, List<string> lines)
		{
			return "<div class=\"deck-col\"><div class=\"deck-col-title\">" + title + "</div>" + string.Join("<br>", lines) + "</div>";
		}

		private static string DeckSlotHtml(RoleDeckSlot slot)
		{
			string html = DeckRoleHtml(slot.Role1, slot.Faction1);
			if (slot.Role2 != Role.NONE)
			{
				html += " / " + DeckRoleHtml(slot.Role2, slot.Faction2);
			}
			return html;
		}

		private static string DeckModifierHtml(RoleDeckSlot slot)
		{
			string html = DeckModifierRoleHtml(slot.Role1);
			if (slot.Role2 != Role.NONE)
			{
				html += " / " + DeckModifierRoleHtml(slot.Role2);
			}
			return html;
		}

		private static string DeckModifierRoleHtml(Role role)
		{
			string name = role.ToDisplayString();
			if (string.IsNullOrEmpty(name) || name.StartsWith("GUI_"))
			{
				name = RoleExtensions.GetRoleData(role).roleName;
			}
			return "<span style=\"color:#FFFFFF\">" + name + "</span>";
		}

		private static string DeckRoleHtml(Role role, FactionType faction)
		{
			string name = TmpAnyTag.Replace(role.ToColorizedDisplayString(faction), "");
			if (string.IsNullOrEmpty(name) || name.StartsWith("GUI_"))
			{
				name = role.IsBucket() ? role.GetRoleBucket().roleName : RoleExtensions.GetRoleData(role).roleName;
			}
			string color = ColorUtility.ToHtmlStringRGB(ExtensionMethods.ParseColor(ClientRoleExtensions.GetFactionColor(faction)));
			return BuildRoleNameHtml(name, faction, color, FancyGradients.TryGetGradient(faction, role), false);
		}

		private static string ChatNameStyleHtml(string dashedName, FactionType faction, string paintColor, Gradient fancyGradient)
		{
			string gradientStyle = FancyGradients.ToCssStyle(fancyGradient) ?? GradientStyleFor(faction);
			if (gradientStyle != null)
			{
				return "<style>span." + dashedName + "{" + gradientStyle + ";text-shadow: none; filter: drop-shadow(0 0 2px #000000);}</style>";
			}
			return "<style>span." + dashedName + "{color: #" + paintColor + ";}</style>";
		}

		private static string BuildRoleNameHtml(string roleName, FactionType faction, string roleColor, Gradient fancyGradient = null, bool padded = true)
		{
			string gradientStyle = FancyGradients.ToCssStyle(fancyGradient) ?? GradientStyleFor(faction);
			if (gradientStyle != null)
			{
				return "<span style=\"" + gradientStyle + "\">" + roleName + "</span>";
			}
			return "<span style=\"color:#" + roleColor + "\">" + (padded ? " " + roleName + " " : roleName) + "</span>";
		}

		private static string GradientStyleFor(FactionType faction)
		{
			if (faction.IsGradientColor())
			{
				string color1 = ClientRoleExtensions.GetFactionColor(faction).TrimStart('#');
				string color2 = ClientRoleExtensions.GetSecondFactionColor(faction).TrimStart('#');
				return "background: linear-gradient(to bottom, #" + color1 + ", #" + color2 + ");-webkit-background-clip: text;-webkit-text-fill-color: transparent;";
			}
			return null;
		}
	}
}
