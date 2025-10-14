using Godot;
using System;
using System.Text.Json;
using System.Collections.Generic;

public partial class GameProgress : Node
{
	public static int CurrentLevelIndex = 1;   // ใช้ปลดล็อกเลเวล
	public static int CurrentPlayingLevel = 1; // ใช้จำว่าเพิ่งเล่นเลเวลไหนอยู่
	public static int LastLevelScore = 0;  // คะแนนล่าสุดที่เพิ่งเล่นจบ
	public static int LastBonusScore { get; set; } = 0;
	public static int LastTotalScore { get; set; } = 0;
	public static int LastHighScore { get; set; } = 0;
	// 🟢 บอกว่าผู้เล่นผ่านด่านล่าสุดแล้วหรือยัง
	public static bool IsLevelCleared { get; set; } = false;


	private static readonly string SavePath = "user://savegame.json";
	
	// เก็บจำนวนปลาที่กินได้แยกตามชนิด
	public static Dictionary<string, int> FishCountByType = new();

	// เมธอดล้างข้อมูลปลา (เรียกตอนเริ่มด่านใหม่)
	public static void ResetFishCount()
	{
		FishCountByType.Clear();
	}

	// ตัวอย่าง: เพิ่มปลาเข้า dict
	public static void AddFishCount(string fishType)
	{
		if (!FishCountByType.ContainsKey(fishType))
			FishCountByType[fishType] = 0;
		FishCountByType[fishType]++;
	}

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
