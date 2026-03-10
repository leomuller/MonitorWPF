using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Encodings.Web; // <-- Make sure this is here

public class ProcessAlertFiles
{
		

	public static void MakeMasterFile()
	{
		string folderPath = "C:\\DevLeo\\PR2025\\MonitorWpf1\\SampleDataTools";

		// 1. Get all JSON files in the directory
		var files = Directory.GetFiles(folderPath, "*.*");

		// 2. Use LINQ to extract unique "data" values across all files
		var uniqueTowns = files
			.SelectMany(file =>
			{
				string json = File.ReadAllText(file);
				using var doc = JsonDocument.Parse(json);
				return doc.RootElement.EnumerateArray()
					.Select(item => item.GetProperty("data").GetString())
					.ToList();
			})
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Distinct() // This is the magic "unique" filter
			.OrderBy(name => name) // Keeps it organized
			.ToList();

		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			// This allows Hebrew characters to stay as Hebrew in the file 
			// instead of becoming \u05E4...
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		// 3. Save as your clean master list
		string outputJson = JsonSerializer.Serialize(uniqueTowns, options);
		File.WriteAllText(System.IO.Path.Combine(folderPath, "MasterTownList.json"), outputJson);
	}
}
