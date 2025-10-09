using Godot;                       
using System.Collections.Generic;  
using System.IO;                   
using System.Text.Json;            

public partial class PlayerLogin : Node
{
	// 👉 เพิ่ม Instance สำหรับให้เรียกใช้งานจาก class อื่น
	public static PlayerLogin Instance { get; private set; }

	// path หลักที่ใช้จริง (user data)
	private string SavePathUser = "user://players.json";

	// path สำรองเพื่อให้เห็นไฟล์ใน FileSystem ของ Editor
	private string SavePathRes = "res://SceneLogin/SaveUserLogin/players.json";

	// คลาสย่อยสำหรับเก็บข้อมูลผู้เล่น 1 คน
	public class SaveData
	{
		public string PlayerName { get; set; } // ชื่อผู้เล่น
	}

	public override void _Ready()
	{
		// ตั้งค่า Instance (Singleton)
		Instance = this;

		GD.Print("  User save path: " + ProjectSettings.GlobalizePath(SavePathUser));
	}

	// โหลดรายชื่อผู้เล่น
	public List<SaveData> LoadPlayers()
	{
		string pathUser = ProjectSettings.GlobalizePath(SavePathUser);
		string pathRes  = ProjectSettings.GlobalizePath(SavePathRes);

		string json = "";

		if (File.Exists(pathUser))
		{
			GD.Print("Loading players from USER path: " + pathUser);
			json = File.ReadAllText(pathUser);
		}
		else if (File.Exists(pathRes))
		{
			GD.Print("Loading players from RES path: " + pathRes);
			json = File.ReadAllText(pathRes);
		}
		else
		{
			GD.Print("No players.json found, returning empty list");
			return new List<SaveData>();
		}

		return JsonSerializer.Deserialize<List<SaveData>>(json) ?? new List<SaveData>();
	}

	// บันทึกชื่อผู้เล่น
	public bool SavePlayer(string name)
	{
		var list = LoadPlayers();

		if (list.Exists(p => p.PlayerName == name))
		{
			GD.Print("Save failed, duplicate name: " + name);
			return false;
		}

		list.Add(new SaveData { PlayerName = name });

		string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });

		// Save ลง user://
		string pathUser = ProjectSettings.GlobalizePath(SavePathUser);
		File.WriteAllText(pathUser, json);
		GD.Print("Saved to USER path: " + pathUser);

		// Save ลง res:// (เพื่อดูไฟล์ใน editor)
		string pathRes = ProjectSettings.GlobalizePath(SavePathRes);
		File.WriteAllText(pathRes, json);
		GD.Print("Saved to RES path: " + pathRes);

		return true;
	}
}
