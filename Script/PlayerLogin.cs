using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public partial class PlayerLogin : Node
{
	// ✅ Instance สำหรับเรียกใช้แบบ Singleton
	public static PlayerLogin Instance { get; private set; }

	// ✅ ผู้ใช้ปัจจุบัน (จะเซตหลังล็อกอินสำเร็จ)
	public SaveData CurrentUser { get; private set; }

	// ✅ Path หลักในการบันทึกไฟล์
	private string SavePathUser = "user://players.json";
	private string SavePathRes = "res://SceneLogin/SaveUserLogin/players.json";

	// ✅ โครงสร้างข้อมูลผู้เล่น
	public class SaveData
	{
		public string PlayerName { get; set; }
		public string Password { get; set; }  // เก็บรหัสเป็นข้อความตรง ๆ (ตามที่ต้องการ)
		public string CreatedAt { get; set; } // วันสมัคร
	}

	public override void _Ready()
	{
		Instance = this;
		GD.Print("🟢 PlayerLogin ready at " + ProjectSettings.GlobalizePath(SavePathUser));
	}

	// ✅ โหลดรายชื่อผู้เล่นทั้งหมด
	public List<SaveData> LoadPlayers()
	{
		string json = "";
		string pathUser = ProjectSettings.GlobalizePath(SavePathUser);
		string pathRes = ProjectSettings.GlobalizePath(SavePathRes);

		if (File.Exists(pathUser))
		{
			GD.Print("📂 Loading players from USER path");
			json = File.ReadAllText(pathUser);
		}
		else if (File.Exists(pathRes))
		{
			GD.Print("📂 Loading players from RES path");
			json = File.ReadAllText(pathRes);
		}
		else
		{
			GD.Print("⚠️ No players.json found.");
			return new List<SaveData>();
		}

		try
		{
			return JsonSerializer.Deserialize<List<SaveData>>(json) ?? new List<SaveData>();
		}
		catch (Exception ex)
		{
			GD.PushError("❌ Failed to parse JSON: " + ex.Message);
			return new List<SaveData>();
		}
	}

	// ✅ บันทึกผู้เล่นใหม่ (ชื่อ + รหัส)
	public bool SavePlayer(string name, string password)
	{
		var list = LoadPlayers();

		// ตรวจชื่อซ้ำ
		if (list.Exists(p => p.PlayerName == name))
		{
			GD.Print("🚫 Duplicate name: " + name);
			return false;
		}

		var newUser = new SaveData
		{
			PlayerName = name,
			Password = password,              // เก็บรหัสเป็นข้อความตรง ๆ
			CreatedAt = DateTime.UtcNow.ToString("o") // วันที่สมัคร (ISO 8601)
		};

		list.Add(newUser);
		string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });

		// เขียนไฟล์
		try
		{
			File.WriteAllText(ProjectSettings.GlobalizePath(SavePathUser), json);
			File.WriteAllText(ProjectSettings.GlobalizePath(SavePathRes), json);
			GD.Print("✅ Saved new user: " + name);
		}
		catch (Exception ex)
		{
			GD.PushError("❌ Failed to save player file: " + ex.Message);
			return false;
		}

		// ตั้งให้เป็นผู้ใช้ปัจจุบัน
		CurrentUser = newUser;
		return true;
	}
}
