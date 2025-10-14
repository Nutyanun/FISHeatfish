using Godot;                                      // ใช้ API ของ Godot (Node, GD, ProjectSettings ฯลฯ)
using System;                                     // .NET base (DateTime, Exception)
using System.Collections.Generic;                 // List<T>
using System.IO;                                  // File, File.Exists, File.ReadAllText
using System.Text.Json;                           // System.Text.Json สำหรับ parse JSON (สคีมาใหม่/เก่า)
// ใช้ GDict สำหรับทำงานกับ LeaderboardStore (Godot.Dictionary)
using GDict = Godot.Collections.Dictionary;       // alias ให้พิมพ์สั้นขึ้น

public partial class PlayerLogin : Node           // คลาสซิงเกิลตัน (Autoload) เก็บสถานะผู้เล่น/ล็อกอิน
{
	public static PlayerLogin Instance { get; private set; } // ตัวชี้ global ของซิงเกิลตัน
	
	public string CurrentPlayerName { get; private set; } = "Guest";

	// ✅ เปลี่ยนให้ set ได้จากไฟล์อื่น (เพื่อแก้ Error CS0272)
	public SaveData CurrentUser { get; set; }      // ผู้ใช้ที่ล็อกอินอยู่ปัจจุบัน
	public string TodayKey { get; internal set; }   // คีย์วันที่วันนี้ (yyyy-MM-dd) ใช้จัดกลุ่ม high score รายวัน
	
	// อ่าน/เขียน “ข้อมูลผู้เล่นจริง” เฉพาะใน user:// เท่านั้น
	private string SavePathUser = "user://players.json";  // path ไฟล์ข้อมูลรวม (players + leaderboards) ในโฟลเดอร์ user
	private string DefaultPath = "res://SceneLogin/saveUserLogin/players.json";

	public class SaveData                           // โครงสร้างข้อมูลผู้เล่น 1 คน
	{
		public string PlayerName { get; set; }      // ชื่อผู้เล่น
		public string Password   { get; set; }      // รหัสผ่าน (หมายเหตุ: โปรดแฮชจริงจังในโปรดักชัน)
		public string CreatedAt  { get; set; }      // เวลาสมัคร/ถูกสร้าง (ISO 8601)
	}

	private string MakeTodayKeyLocal() => DateTime.Now.ToString("yyyy-MM-dd"); // คืนวันที่ท้องถิ่นรูปแบบ yyyy-MM-dd

	public void StampTodayKey()                     // อัปเดต TodayKey ให้เป็นวันนี้ (ท้องถิ่น)
	{
		TodayKey = MakeTodayKeyLocal();             // เซ็ตคีย์
		GD.Print($"🗓️ TodayKey set to {TodayKey}"); // log เพื่อดีบัก
	}

	public void SetCurrentUserAndStampToday(SaveData user) // ตั้งผู้ใช้ปัจจุบัน + ประทับ TodayKey
	{
		CurrentUser = user;                         // เซ็ตผู้ใช้
		StampTodayKey();                            // อัปเดตคีย์วันที่
		GD.Print($"🔑 Login as {CurrentUser?.PlayerName ?? "(null)"} ; TodayKey={TodayKey}"); // log
	}

	public override void _Ready()                   // เรียกเมื่อโหนดพร้อมใช้งาน
	{
		Instance = this;                            // ตั้งซิงเกิลตัน
		TodayKey = MakeTodayKeyLocal();             // ค่าตั้งต้นของ TodayKey
		GD.Print("🟢 PlayerLogin ready at " + ProjectSettings.GlobalizePath(SavePathUser)); // พิมพ์ path จริงบนดิสก์
		GD.Print($"🗓️ Initial TodayKey = {TodayKey}"); // พิมพ์คีย์วันที่เริ่มต้น
	}

	// ===== Loader: รองรับสคีมาเก่า [] และสคีมาใหม่ { "players": { ... } } ; อ่านเฉพาะ user:// =====
	public List<SaveData> LoadPlayers()             // อ่านรายชื่อผู้เล่นทั้งหมดจากไฟล์ user://players.json
	{
		try
		{
			string pathUser = ProjectSettings.GlobalizePath(SavePathUser); // แปลง user:// → path จริงของระบบไฟล์
			if (!File.Exists(pathUser)) return new List<SaveData>();       // ถ้าไฟล์ไม่มี → คืนลิสต์ว่าง

			string json = File.ReadAllText(pathUser);                      // อ่านทั้งหมดเป็นสตริง
			if (string.IsNullOrWhiteSpace(json)) return new List<SaveData>(); // ถ้าว่าง → คืนลิสต์ว่าง

			// ตรวจอักษรตัวแรกเพื่อแยก schema
			char first = '\0';                                             // ตัวแปรเก็บอักขระตัวแรกที่ไม่ใช่ white space
			foreach (var ch in json) { if (!char.IsWhiteSpace(ch)) { first = ch; break; } }

			// สคีมาเก่า: เป็นลิสต์ SaveData ตรง ๆ
			if (first == '[')                                              // ถ้าขึ้นต้นด้วย '[' แปลว่าเป็น array
			{
				return JsonSerializer.Deserialize<List<SaveData>>(json)    // แปลงเป็น List<SaveData>
					   ?? new List<SaveData>();                            // ถ้า null → คืนลิสต์ว่าง
			}

			// สคีมาใหม่: เป็น object ที่มี "players"
			using var doc = JsonDocument.Parse(json);                       // parse เป็น JsonDocument
			var root = doc.RootElement;                                     // เข้าถึง root
			if (root.ValueKind != JsonValueKind.Object) return new List<SaveData>(); // ไม่ใช่ object → คืนว่าง
			if (!root.TryGetProperty("players", out var playersElem) ||     // ต้องมีคีย์ "players"
				playersElem.ValueKind != JsonValueKind.Object)
				return new List<SaveData>();                                // ไม่มี → คืนว่าง

			var list = new List<SaveData>();                                // เตรียมลิสต์ผลลัพธ์
			foreach (var prop in playersElem.EnumerateObject())             // วนทีละผู้เล่น (key = ชื่อ)
			{
				var name = prop.Name;                                       // ชื่อผู้เล่นจากชื่อพร็อพเพอร์ตี
				var pObj = prop.Value;                                      // ค่าของ object ผู้เล่น

				string pwd = "";                                            // รหัสผ่าน (เริ่มค่าว่าง)
				string reg = "";                                            // เวลาสมัคร

				if (pObj.ValueKind == JsonValueKind.Object)                 // ถ้าโครงสร้างถูกต้อง
				{
					if (pObj.TryGetProperty("password", out var pwdElem) && // มี password?
						pwdElem.ValueKind == JsonValueKind.String)
						pwd = pwdElem.GetString() ?? "";                     // อ่าน password

					// รองรับทั้ง registered_at และ CreatedAt
					if (pObj.TryGetProperty("registered_at", out var regElem) && // โครงสร้างใหม่
						regElem.ValueKind == JsonValueKind.String)
						reg = regElem.GetString() ?? "";
					else if (pObj.TryGetProperty("CreatedAt", out var reg2Elem) && // บางไฟล์เก่าใช้ CreatedAt
							 reg2Elem.ValueKind == JsonValueKind.String)
						reg = reg2Elem.GetString() ?? "";
				}

				if (string.IsNullOrEmpty(reg))                               // ถ้าไม่พบเวลา
					reg = DateTime.UtcNow.ToString("o");                     // ใช้เวลาปัจจุบันแบบ ISO (UTC)

				list.Add(new SaveData {                                      // เพิ่มลงผลลัพธ์
					PlayerName = name,
					Password   = pwd,
					CreatedAt  = reg
				});
			}

			return list;                                                     // คืนลิสต์ผู้เล่นทั้งหมด
		}
		catch (Exception ex)                                                 // จัดการความผิดพลาดการอ่าน/parse
		{
			GD.PushError("❌ Failed to parse JSON: " + ex.Message);          // log error
			return new List<SaveData>();                                     // คืนว่างเพื่อให้โปรแกรมไปต่อได้
		}
	}

	// ===== สมัครผู้เล่นใหม่ (บันทึกลงเอกสารรวมผ่าน LeaderboardStore; ไม่ยุ่งกับ res://) =====
	public bool SavePlayer(string name, string password) // สมัครผู้ใช้ใหม่ และบันทึกลง players.json (สคีมาใหม่)
	{
		var doc = LeaderboardStore.LoadDoc();                  // โหลดเอกสารรวม (Dictionary) จากไฟล์
		doc = LeaderboardStore.EnsureRoot(doc);                // ensure ให้มีคีย์พื้นฐาน "players", "leaderboards"

		var players = (GDict)doc["players"];                   // อ้างถึงโหนด players
		if (players.ContainsKey(name))                         // ถ้าชื่อซ้ำ
		{
			GD.Print("🚫 Duplicate name: " + name);            // log และ
			return false;                                      // ไม่อนุญาตให้สมัคร
		}

		var nowIso = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"); // เวลาเขตท้องถิ่นพร้อม timezone
		players[name] = new GDict {                                   // สร้างเรคคอร์ดผู้เล่นใหม่
			{ "password",      password },                             // เก็บรหัส (plain-text ตามเวอร์ชันนี้)
			{ "registered_at", nowIso },                               // เวลาเริ่มต้นใช้งาน
			{ "levels",        new GDict() },                           // ที่ว่างไว้เก็บความคืบหน้าด่าน
			{ "current_level", 1 },              // เพิ่ม: เริ่มต้นที่ด่าน 1
			{ "high_scores",   new GDict() }     // เพิ่ม: เก็บ high score รายเลเวล
			
		};

		// เซฟกลับเอกสารรวม (ตำแหน่งไฟล์ถูกกำหนดใน LeaderboardStore เอง)
		LeaderboardStore.SaveDoc(doc);                                 // เขียนลงไฟล์ user://players.json

		GD.Print("✅ Saved new user (new schema only): " + name);      // log ว่าสมัครสำเร็จ

		SetCurrentUserAndStampToday(new SaveData {                      // ตั้ง CurrentUser และ TodayKey ทันที
			PlayerName = name,
			Password   = password,
			CreatedAt  = nowIso
		});
		
		// 🟢 เพิ่มบรรทัดนี้ (ตั้งชื่อให้ HUD ใช้)
		CurrentPlayerName = name;

		return true;                                                   // สมัครสำเร็จ
	}

// ===== ล็อกอินผู้ใช้เดิม (ตรวจจากไฟล์ user://players.json) =====
public bool LoginExisting(string name, string password)
{
	var list = LoadPlayers();
	var user = list.Find(p => p.PlayerName == name && p.Password == password);
	if (user == null) return false;

	SetCurrentUserAndStampToday(user);

	var doc = LeaderboardStore.LoadDoc();
	doc = LeaderboardStore.EnsureRoot(doc);
	var players = (GDict)doc["players"];

	// ถ้าไม่มี record ของผู้เล่นนี้ → สร้างใหม่
	if (!players.ContainsKey(name))
	{
		players[name] = new GDict {
			{ "registered_at", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") },
			{ "levels", new GDict() },
			{ "current_level", 1 }
		};
		GD.Print($"[LoginExisting] 🆕 Created new player record for {name}");
	}

	var p = (GDict)players[name];

	// 🟢 ตรวจว่าผู้เล่นมีเลเวลถึงไหนในไฟล์ แล้วอัปเดต current_level ให้ตรง
	if (p.ContainsKey("levels"))
	{
		var levels = (GDict)p["levels"];
		int maxLv = 0;
		foreach (var key in levels.Keys)
		{
			if (int.TryParse(key.AsString(), out int lv))
				maxLv = Math.Max(maxLv, lv);  // ❗ ไม่บวก +1 แล้ว
		}

		int current = p.ContainsKey("current_level") ? (int)(long)p["current_level"] : 1;

		// ✅ Debug Log
		GD.Print($"[LoginExisting] 🔍 Loaded from file → current_level={current}, maxLvFound={maxLv}");
		
		// ถ้า current_level > maxLvFound → ลดลงให้ตรง
		if ((int)(long)p["current_level"] > maxLv)
		{
		p["current_level"] = maxLv;
		GD.Print($"[LoginExisting] 🔧 Fixed current_level (was higher than levels) → now {maxLv}");
		}

		if (maxLv > current)
		{
			p["current_level"] = maxLv;
			GD.Print($"[LoginExisting] 🟢 Auto-recovered current_level set to {maxLv}");
		}
		else
		{
			GD.Print($"[LoginExisting] ℹ️ Keep current_level = {current}");
		}

		GameProgress.CurrentLevelIndex = (int)(long)p["current_level"];
	}
	else
	{
		GameProgress.CurrentLevelIndex = 1;
		GD.Print($"[LoginExisting] ℹ️ No levels found → set current_level = 1");
	}

	LeaderboardStore.SaveDoc(doc);  // ✅ เซฟกลับ

	CurrentPlayerName = name;
	GD.Print($"[LoginExisting] 🔓 Loaded level {GameProgress.CurrentLevelIndex} for {name}");
	return true;
}

}
