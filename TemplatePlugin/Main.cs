using System;
using System.IO;
using SML;
using UnityEngine;

namespace TemplatePlugin;

[Mod.SalemMod]
public class Main
{
	public static readonly string FolderPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SalemModLoader", "ModFolders", "GameLogs");

	public void Start()
	{
		Console.WriteLine("check for directory");

		if (!Directory.Exists(FolderPath))
		{
			Directory.CreateDirectory(FolderPath);
		}
	}
}
