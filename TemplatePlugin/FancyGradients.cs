using System;
using System.Reflection;
using SML;
using Server.Shared.State;
using UnityEngine;

namespace TemplatePlugin;

/// <summary>
/// Fancy UI gradients via reflection.
/// </summary>
public static class FancyGradients
{
	private static MethodInfo getChangedGradient;
	private static bool lookupDone;

	public static Gradient TryGetGradient(FactionType faction, Role role)
	{
		if (!ModStates.IsEnabled("alchlcsystm.fancy.ui"))
		{
			return null;
		}
		try
		{
			if (!lookupDone)
			{
				lookupDone = true;
				Type gradientsType = Type.GetType("FancyUI.Gradients, FancyUI");
				if (gradientsType == null)
				{
					foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
					{
						gradientsType = assembly.GetType("FancyUI.Gradients");
						if (gradientsType != null)
						{
							break;
						}
					}
				}
				if (gradientsType != null)
				{
					getChangedGradient = gradientsType.GetMethod("GetChangedGradient", BindingFlags.Public | BindingFlags.Static);
				}
			}
			if (getChangedGradient == null)
			{
				return null;
			}
			Gradient gradient = getChangedGradient.Invoke(null, new object[] { faction, role }) as Gradient;
			return (gradient != null && gradient.colorKeys.Length >= 2) ? gradient : null;
		}
		catch
		{
			return null;
		}
	}

	public static string TryGetKeywordStyle()
	{
		return TryGetTwoColorStyle("KeywordStart", "KeywordEnd");
	}

	public static string TryGetMentionStyle()
	{
		return TryGetTwoColorStyle("MentionStart", "MentionEnd");
	}

	public static string TryGetAchievementStyle()
	{
		return TryGetTwoColorStyle("AchievementStart", "AchievementEnd");
	}

	private static string TryGetTwoColorStyle(string startField, string endField)
	{
		if (!ModStates.IsEnabled("alchlcsystm.fancy.ui"))
		{
			return null;
		}
		try
		{
			Type fancyType = Type.GetType("FancyUI.Fancy, FancyUI");
			if (fancyType == null)
			{
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					fancyType = assembly.GetType("FancyUI.Fancy");
					if (fancyType != null)
					{
						break;
					}
				}
			}
			if (fancyType == null)
			{
				return null;
			}
			string start = ReadColorOption(fancyType, startField);
			string end = ReadColorOption(fancyType, endField);
			if (start == null || end == null)
			{
				return null;
			}
			return "background: linear-gradient(to bottom, " + start + ", " + end + ");-webkit-background-clip: text;-webkit-text-fill-color: transparent;";
		}
		catch
		{
			return null;
		}
	}

	private static string ReadColorOption(Type fancyType, string fieldName)
	{
		FieldInfo field = fancyType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
		object option = (field != null) ? field.GetValue(null) : null;
		PropertyInfo valueProp = (option != null) ? option.GetType().GetProperty("Value") : null;
		string value = (valueProp != null) ? valueProp.GetValue(option) as string : null;
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		return value.StartsWith("#") ? value : "#" + value;
	}

	public static string ToCssStyle(Gradient gradient)
	{
		if (gradient == null || gradient.colorKeys.Length < 2)
		{
			return null;
		}
		string stops = "";
		for (int i = 0; i < gradient.colorKeys.Length; i++)
		{
			GradientColorKey key = gradient.colorKeys[i];
			if (i > 0)
			{
				stops += ", ";
			}
			stops += "#" + ColorUtility.ToHtmlStringRGB(key.color) + " " + (key.time * 100f).ToString("0") + "%";
		}
		return "background: linear-gradient(to bottom, " + stops + ");-webkit-background-clip: text;-webkit-text-fill-color: transparent;";
	}
}
