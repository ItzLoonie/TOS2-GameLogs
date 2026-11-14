using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TemplatePlugin;

public class LoadEmbeddedResources
{
	public static Sprite LoadSprite(string filePath, float pixelsPerUnit = 100f, SpriteMeshType spriteType = SpriteMeshType.Tight)
	{
		Texture2D texture = LoadTexture(filePath);
		return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0f, 0f), pixelsPerUnit, 0u, spriteType);
	}

	public static Texture2D LoadTexture(string filePath)
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		Stream manifestResourceStream = executingAssembly.GetManifestResourceStream(filePath);
		Texture2D texture = new Texture2D(2, 2);
		if (ImageConversion.LoadImage(texture, ReadFully(manifestResourceStream)))
		{
			return texture;
		}
		Console.WriteLine("File does not exist!");
		return null;
	}

	public static byte[] ReadFully(Stream input)
	{
		using MemoryStream memoryStream = new MemoryStream();
		input.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}
}
