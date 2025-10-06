using Godot;
using System;
using System.Text.Json;

public partial class GameProgress : Node
{
	public static int CurrentLevelIndex = 1;   // ใช้ปลดล็อกเลเวล
	public static int CurrentPlayingLevel = 1; // ใช้จำว่าเพิ่งเล่นเลเวลไหนอยู่
	public static int LastLevelScore = 0;  // คะแนนล่าสุดที่เพิ่งเล่นจบ

	private static readonly string SavePath = "user://savegame.json";

	public override void _Ready()
	{
		Load();
	}

	public static void Advance()
	{
		CurrentLevelIndex++;
		Save();
	}

	public static void Reset()
	{
		CurrentLevelIndex = 0;
		Save();
	}

	public static void Save()
	{
		var json = JsonSerializer.Serialize(new SaveData
		{
			currentLevel = CurrentLevelIndex
		});
		using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		f.StoreString(json);
	}

	public static void Load()
	{
		if (!FileAccess.FileExists(SavePath)) return;
		using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		try
		{
			var data = JsonSerializer.Deserialize<SaveData>(f.GetAsText());
			if (data != null) CurrentLevelIndex = data.currentLevel;
		}
		catch (Exception e) { GD.PrintErr("Load error: " + e.Message); }
	}

	private class SaveData
	{
		public int currentLevel { get; set; }
	}
}
