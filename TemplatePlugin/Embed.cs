using System.IO;
using System.Reflection;

namespace TemplatePlugin;

public class Embed
{
	public static string GetEmbeddedText(string filePath)
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		using StreamReader streamReader = new StreamReader(executingAssembly.GetManifestResourceStream(filePath));
		return streamReader.ReadToEnd();
	}
}
