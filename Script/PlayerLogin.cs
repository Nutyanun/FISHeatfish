using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
// ใช้ GDict สำหรับทำงานกับ LeaderboardStore (Godot.Dictionary)
using GDict = Godot.Collections.Dictionary;

public partial class PlayerLogin : Node
{
	public static PlayerLogin Instance { get; private set; }

	// ✅ เปลี่ยนให้ set ได้จากไฟล์อื่น (เพื่อแก้ Error CS0272)
	public SaveData CurrentUser { get; set; }
	public string TodayKey { get; private set; }

	// อ่าน/เขียน “ข้อมูลผู้เล่นจริง” เฉพาะใน user:// เท่านั้น
	private string SavePathUser = "user://players.json";

	public class SaveData
	{
		public string PlayerName { get; set; }
		public string Password   { get; set; }
		public string CreatedAt  { get; set; }
	}

	private string MakeTodayKeyLocal() => DateTime.Now.ToString("yyyy-MM-dd");

	public void StampTodayKey()
	{
		TodayKey = MakeTodayKeyLocal();
		GD.Print($"🗓️ TodayKey set to {TodayKey}");
	}

	public void SetCurrentUserAndStampToday(SaveData user)
	{
		CurrentUser = user;
		StampTodayKey();
		GD.Print($"🔑 Login as {CurrentUser?.PlayerName ?? "(null)"} ; TodayKey={TodayKey}");
	}

	public override void _Ready()
	{
		Instance = this;
		TodayKey = MakeTodayKeyLocal();
		GD.Print("🟢 PlayerLogin ready at " + ProjectSettings.GlobalizePath(SavePathUser));
		GD.Print($"🗓️ Initial TodayKey = {TodayKey}");
	}

	// ===== Loader: รองรับสคีมาเก่า [] และสคีมาใหม่ { "players": { ... } } ; อ่านเฉพาะ user:// =====
	public List<SaveData> LoadPlayers()
	{
		try
		{
			string pathUser = ProjectSettings.GlobalizePath(SavePathUser);
			if (!File.Exists(pathUser)) return new List<SaveData>();

			string json = File.ReadAllText(pathUser);
			if (string.IsNullOrWhiteSpace(json)) return new List<SaveData>();

			// ตรวจอักษรตัวแรกเพื่อแยก schema
			char first = '\0';
			foreach (var ch in json) { if (!char.IsWhiteSpace(ch)) { first = ch; break; } }

			// สคีมาเก่า: เป็นลิสต์ SaveData ตรง ๆ
			if (first == '[')
			{
				return JsonSerializer.Deserialize<List<SaveData>>(json) ?? new List<SaveData>();
			}

			// สคีมาใหม่: เป็น object ที่มี "players"
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object) return new List<SaveData>();
			if (!root.TryGetProperty("players", out var playersElem) || playersElem.ValueKind != JsonValueKind.Object)
				return new List<SaveData>();

			var list = new List<SaveData>();
			foreach (var prop in playersElem.EnumerateObject())
			{
				var name = prop.Name;
				var pObj = prop.Value;

				string pwd = "";
				string reg = "";

				if (pObj.ValueKind == JsonValueKind.Object)
				{
					if (pObj.TryGetProperty("password", out var pwdElem) && pwdElem.ValueKind == JsonValueKind.String)
						pwd = pwdElem.GetString() ?? "";

					// รองรับทั้ง registered_at และ CreatedAt
					if (pObj.TryGetProperty("registered_at", out var regElem) && regElem.ValueKind == JsonValueKind.String)
						reg = regElem.GetString() ?? "";
					else if (pObj.TryGetProperty("CreatedAt", out var reg2Elem) && reg2Elem.ValueKind == JsonValueKind.String)
						reg = reg2Elem.GetString() ?? "";
				}

				if (string.IsNullOrEmpty(reg))
					reg = DateTime.UtcNow.ToString("o");

				list.Add(new SaveData {
					PlayerName = name,
					Password   = pwd,
					CreatedAt  = reg
				});
			}

			return list;
		}
		catch (Exception ex)
		{
			GD.PushError("❌ Failed to parse JSON: " + ex.Message);
			return new List<SaveData>();
		}
	}

	// ===== สมัครผู้เล่นใหม่ (บันทึกลงเอกสารรวมผ่าน LeaderboardStore; ไม่ยุ่งกับ res://) =====
	public bool SavePlayer(string name, string password)
	{
		var doc = LeaderboardStore.LoadDoc();
		doc = LeaderboardStore.EnsureRoot(doc); // ให้แน่ใจว่ามี keys พื้นฐาน เช่น "players"

		var players = (GDict)doc["players"];
		if (players.ContainsKey(name))
		{
			GD.Print("🚫 Duplicate name: " + name);
			return false;
		}

		var nowIso = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
		players[name] = new GDict {
			{ "password",      password },
			{ "registered_at", nowIso },
			{ "levels",        new GDict() } // สำหรับความคืบหน้าด่านภายหลัง
		};

		// เซฟกลับเอกสารรวม (ตำแหน่งไฟล์ถูกกำหนดใน LeaderboardStore เอง)
		LeaderboardStore.SaveDoc(doc);

		GD.Print("✅ Saved new user (new schema only): " + name);

		SetCurrentUserAndStampToday(new SaveData {
			PlayerName = name,
			Password   = password,
			CreatedAt  = nowIso
		});

		return true;
	}

	// ===== ล็อกอินผู้ใช้เดิม (ตรวจจากไฟล์ user://players.json) =====
	public bool LoginExisting(string name, string password)
	{
		var list = LoadPlayers();
		var user = list.Find(p => p.PlayerName == name && p.Password == password);
		if (user == null) return false;

		SetCurrentUserAndStampToday(user);
		return true;
	}
}
