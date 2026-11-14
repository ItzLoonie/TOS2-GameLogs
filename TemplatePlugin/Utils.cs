using System;
using System.Diagnostics;
using Home.Shared;
using Server.Shared.Extensions;
using Services;
using UnityEngine;

namespace TemplatePlugin;

public class Utils
{
	public static bool IsRoleKnown(int pos)
	{
		Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(pos, out var value);
		if (value == null)
		{
			return false;
		}
		return true;
	}

	public static Color GetPlayerRoleColor(int pos)
	{
		Color white = Color.white;
		Service.Game.Sim.simulation.knownRolesAndFactions.Data.TryGetValue(pos, out var value);
		if (value == null)
		{
			return white;
		}
		return ExtensionMethods.ParseColor(ClientRoleExtensions.GetFactionColor(RoleExtensions.GetRoleData(value.Item1).factionType));
	}

	public static void OpenDirectory()
	{
		string text = Main.FolderPath;
		Console.WriteLine(text);
		if (IsWindows())
		{
			Console.WriteLine("Opening dir on windows");
			string text2 = "\"" + text + "\"";
			text2 = text2.Replace("/", "\\");
			Process.Start("explorer.exe", text2);
		}
		else if (IsMacOrUnix())
		{
			Console.WriteLine("Opening dir on Mac");
			Process.Start("open", "\"" + text + "\"");
		}
	}

	private static bool IsWindows()
	{
		PlatformID platform = Environment.OSVersion.Platform;
		return platform == PlatformID.WinCE || platform == PlatformID.Win32Windows || platform == PlatformID.Win32S || platform == PlatformID.Win32NT;
	}

	private static bool IsMacOrUnix()
	{
		PlatformID platform = Environment.OSVersion.Platform;
		return platform == PlatformID.MacOSX || platform == PlatformID.Unix;
	}
}
