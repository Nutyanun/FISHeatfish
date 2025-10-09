using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public partial class PlayerLogin : Node
{
	// ✅ Singleton instance — เรียกใช้จากที่อื่นได้ เช่น PlayerLogin.Instance
	public static PlayerLogin Instance { get; private set; }

	// ✅ ผู้ใช้ปัจจุบัน (ตอนนี้ให้ set ได้จากภายนอก เช่น LoginGame.cs)
	public SaveData CurrentUser { get; set; }

	// ✅ Path สำหรับไฟล์ข้อมูลผู้เล่น
	private string SavePathUser = "user://players.json";   // เก็บในโฟลเดอร์ของผู้ใช้ (อ่าน/เขียนได้)
	private string SavePathRes  = "res://SceneLogin/SaveUserLogin/players.json"; // ไฟล์ต้นแบบ (อ่านได้ใน editor)

	// ✅ โครงสร้างข้อมูลผู้เล่น (ข้อมูลที่บันทึกลงไฟล์)
	public class SaveData
	{
		public string PlayerName { get; set; }   // ชื่อผู้เล่น
		public string Password { get; set; }     // รหัสผ่าน (เก็บตรง ๆ)
		public string CreatedAt { get; set; }    // วันที่สมัคร (ISO 8601)
	}

	// ✅ เริ่มทำงานเมื่อ Node พร้อม
	public override void _Ready()
	{
		Instance = this; // ตั้ง instance สำหรับ Singleton
		GD.Print("🟢 PlayerLogin ready at " + ProjectSettings.GlobalizePath(SavePathUser));
	}

	// ✅ โหลดรายชื่อผู้เล่นทั้งหมดจากไฟล์ JSON
	public List<SaveData> LoadPlayers()
	{
		string json = "";
		string pathUser = ProjectSettings.GlobalizePath(SavePathUser);
		string pathRes  = ProjectSettings.GlobalizePath(SavePathRes);

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

	// ✅ เพิ่มผู้เล่นใหม่ (สมัครสมาชิกใหม่)
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
			Password   = password,
			CreatedAt  = DateTime.UtcNow.ToString("o")
		};

		list.Add(newUser);
		string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });

		// เขียนไฟล์ (user:// และ res://)
		try
		{
			File.WriteAllText(ProjectSettings.GlobalizePath(SavePathUser), json);

			// ❗ คำแนะนำ: res:// เขียนได้เฉพาะตอนอยู่ใน Editor
			// ถ้า export เกมแล้วอาจเขียนไม่ได้
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
