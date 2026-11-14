using SML;
using UnityEngine;
using UnityEngine.Events;

namespace TemplatePlugin;

[Mod.SalemMenuItem]
public class GameLogDirectory
{
	public static Mod.SalemMenuButton willPresets = new Mod.SalemMenuButton
	{
		Label = "Game Logs",
		Icon = LoadEmbeddedResources.LoadSprite("TemplatePlugin.resources.images.GameLogIcon.png", 100f, SpriteMeshType.Tight),
		OnClick = new UnityAction(Utils.OpenDirectory)
	};
}
