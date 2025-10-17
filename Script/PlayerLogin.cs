using Godot;                       
using System;                      
using System.Collections.Generic;  // ใช้โครงสร้างข้อมูลพวก List<T>, Dictionary<T> ฯลฯ
using System.IO;  // ใช้จัดการไฟล์ เช่น File.ReadAllText(), File.Exists()
using System.Text.Json; // ใช้สำหรับอ่าน/เขียน JSON (serialize / deserialize)
using GDict = Godot.Collections.Dictionary; // ตั้งชื่อสั้นให้ Godot.Collections.Dictionary เป็น GDict

//
//// PlayerLogin (Autoload/Singleton)  คลาสนี้ตั้งเป็นซิงเกิลตัน ใช้ได้ข้ามซีน
//// - ถือสถานะผู้เล่นปัจจุบัน เช่น CurrentUser, CurrentPlayerName, TodayKey  ข้อมูลล็อกอิน
////- จัดการไฟล์ players.json (อ่าน/เขียน บันทึกชื่อ/รหัส/เลเวล) ที่เก็บข้อมูลผู้เล่น
//// - สมัคร / ล็อกอิน / mirror ไป res:// เพื่อดีบักใน Editor  สำหรับตอนพัฒนาเกม
//// ควรตั้งเป็น Autoload ใน Project Settings → Autoload  เพื่อให้คงค่าไว้ทุกซีน
//
public partial class PlayerLogin : Node   // สร้างคลาส PlayerLogin สืบทอดจาก Node (Godot)
{
	// Single Instance 
	public static PlayerLogin Instance { get; private set; } // อินสแตนซ์แบบโกลบอล ใช้เรียกได้จากทุกที่

	// Public State
	public string CurrentPlayerName { get; private set; } = "Guest"; // ชื่อผู้เล่นปัจจุบัน เริ่มต้นเป็น Guest
	public SaveData CurrentUser { get; set; }  // เก็บข้อมูลผู้ใช้ปัจจุบันในรูปแบบอ็อบเจ็กต์ SaveData
	public string TodayKey { get; internal set; }  // เก็บวันที่ปัจจุบัน เช่น "2025-10-15"

	// Storage Paths
	private string SavePathUser = "user://players.json"; // ไฟล์ข้อมูลผู้เล่นจริง (เขียนใน user data)
	private string DefaultPath = "res://SceneLogin/saveUserLogin/players.json"; // ไฟล์ต้นแบบสำหรับเริ่มต้น (อยู่ในโปรเจกต์)

	/// <summary> 
	/// โครงสร้างข้อมูลของผู้เล่น 1 คน (ใช้สำหรับ serialize / deserialize)
	/// </summary>
	public class SaveData // ประกาศคลาสภายในชื่อ SaveData
	{
		public string PlayerName { get; set; } // ชื่อผู้เล่น (ใช้เป็นคีย์)
		public string Password   { get; set; } // รหัสผ่านของผู้เล่น
		public string CreatedAt  { get; set; } // วันที่สมัคร (ในรูปแบบ ISO)
	}

	// ฟังก์ชันสร้างข้อความวันที่วันนี้ เช่น "2025-10-15"
	private string MakeTodayKeyLocal() => DateTime.Now.ToString("yyyy-MM-dd"); // ใช้ DateTime.Now แปลงเป็น string รูปแบบ yyyy-MM-dd

	// ฟังก์ชันอัปเดต TodayKey ให้เป็นวันที่ปัจจุบัน
	public void StampTodayKey()
	{
		TodayKey = MakeTodayKeyLocal();  // เรียก MakeTodayKeyLocal() แล้วเก็บใน TodayKey
		GD.Print($"🗓️ TodayKey set to {TodayKey}");// พิมพ์ข้อความบอกใน Output
	}

	// ฟังก์ชันตั้งค่าผู้เล่นปัจจุบัน + อัปเดตวันที่
	public void SetCurrentUserAndStampToday(SaveData user)
	{
		CurrentUser = user;// เก็บข้อมูลผู้เล่นปัจจุบัน
		StampTodayKey();   // อัปเดตวันที่วันนี้
		GD.Print($"🔑 Login as {CurrentUser?.PlayerName ?? "(null)"} ; TodayKey={TodayKey}"); // แสดงชื่อผู้เล่นและวันที่ใน Output
	}

	// ฟังก์ชัน Godot เรียกอัตโนมัติเมื่อ Node พร้อมใช้งาน (เช่น ตอนเปิดซีน)
	public override void _Ready()
	{
		Instance = this;    // ตั้งค่า Instance ให้ชี้มาที่อ็อบเจ็กต์นี้ (Autoload)
		TodayKey = MakeTodayKeyLocal(); // ตั้งวันที่ปัจจุบันให้ TodayKey

		// แสดงข้อความว่าพร้อมแล้ว และแปลง path user:// เป็น path จริงของระบบ
		GD.Print("🟢 PlayerLogin ready at " + ProjectSettings.GlobalizePath(SavePathUser));

		// แสดงค่า TodayKey ที่ตั้งไว้ตอนเริ่มต้น
		GD.Print($"🗓️ Initial TodayKey = {TodayKey}");

		// ตรวจว่ามีไฟล์ players.json ใน user:// แล้วยัง
		if (!Godot.FileAccess.FileExists(SavePathUser))
		{
			// ถ้าไม่มีไฟล์ user:// แต่มีไฟล์ต้นฉบับใน res://
			if (Godot.FileAccess.FileExists(DefaultPath))
			{
				using var src = Godot.FileAccess.Open(DefaultPath, Godot.FileAccess.ModeFlags.Read); // เปิดไฟล์ต้นฉบับแบบอ่าน
				using var dst = Godot.FileAccess.Open(SavePathUser, Godot.FileAccess.ModeFlags.Write); // เปิดไฟล์ปลายทางแบบเขียน
				dst.StoreString(src.GetAsText());             // อ่านเนื้อหาจากต้นฉบับแล้วเขียนลงปลายทาง
				GD.Print($"📦 Copied default players.json to {SavePathUser}"); // แจ้งว่าก๊อปไฟล์เสร็จ
			}
			else
			{
				// ถ้าไม่มีไฟล์ต้นแบบเลย → แจ้ง error
				GD.PushError("❌ Default player file not found at " + DefaultPath);
			}
		}
	}
	// ฟังก์ชันคัดลอก (mirror) ไฟล์จาก user:// → res://
	// ใช้เฉพาะตอนดีบักใน Editor เพราะ res:// เขียนได้เฉพาะในโหมดพัฒนา
	private void MirrorToRes()
	{
		try
		{
			string userPath = ProjectSettings.GlobalizePath(SavePathUser); // แปลง path เสมือน user:// → path จริงบนเครื่อง
			string resPath  = ProjectSettings.GlobalizePath(DefaultPath);  // แปลง path เสมือน res:// → path จริงบนเครื่อง

			if (System.IO.File.Exists(userPath)) // ถ้ามีไฟล์ใน user:// จริง
			{
				System.IO.File.Copy(userPath, resPath, true); // คัดลอกไฟล์ไปยัง res:// ทับของเดิม (true = overwrite)
				GD.Print("🔁 Mirrored user://players.json → res://players.json"); // แจ้งว่าคัดลอกสำเร็จ
			}
			else
			{
				GD.PushWarning("⚠️ No user file found to mirror!"); // ถ้าไม่มีไฟล์ user://  เตือน
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"⚠️ Mirror failed: {ex.Message}");// ถ้ามีข้อผิดพลาด แสดงข้อความเตือน
		}
	}

	// โหลดผู้เล่นทั้งหมดจากไฟล์ user://players.json
	// รองรับทั้งรูปแบบเก่า (array) และใหม่ (object { players: {...} })
	public List<SaveData> LoadPlayers()
	{
		try
		{
			string pathUser = ProjectSettings.GlobalizePath(SavePathUser); // แปลง path user:// เป็น path จริงในเครื่อง

			if (!File.Exists(pathUser))   // ถ้าไม่มีไฟล์ user://players.json
				return new List<SaveData>(); // คืนลิสต์ว่างทันที

			string json = File.ReadAllText(pathUser);// อ่านเนื้อหาทั้งไฟล์เป็น string

			if (string.IsNullOrWhiteSpace(json)) // ถ้าไฟล์ว่างหรือเป็นช่องว่าง
				return new List<SaveData>(); // คืนลิสต์ว่าง

			char first = '\0';  // ตัวแปรเก็บอักษรตัวแรกของไฟล์ (ใช้ตรวจรูปแบบ)
			foreach (var ch in json)  // วนอ่านอักษรในไฟล์ทีละตัว
			{
				if (!char.IsWhiteSpace(ch)) { first = ch; break; } // ถ้าไม่ใช่ช่องว่าง → เก็บตัวแรกแล้วหยุด
			}

			if (first == '[')   // ถ้าขึ้นต้นด้วย [ → เป็น array schema เก่า
			{
				return JsonSerializer.Deserialize<List<SaveData>>(json) // แปลง JSON เป็น List<SaveData>
				?? new List<SaveData>(); // ถ้า null → คืนลิสต์ว่าง
			}

			using var doc = JsonDocument.Parse(json);   // แปลง JSON เป็น DOM object
			var root = doc.RootElement;   // ดึง root ของเอกสาร

			if (root.ValueKind != JsonValueKind.Object) // ถ้า root ไม่ใช่ object
				return new List<SaveData>();  // คืนลิสต์ว่าง

			if (!root.TryGetProperty("players", out var playersElem)   // ถ้าไม่มี key "players"
				|| playersElem.ValueKind != JsonValueKind.Object) // หรือไม่ใช่ object
				return new List<SaveData>();  // คืนลิสต์ว่าง

			var list = new List<SaveData>();  // เตรียมลิสต์เก็บข้อมูลผู้เล่น

			foreach (var prop in playersElem.EnumerateObject())  // วนลูปผู้เล่นทุกคน
			{
				var name = prop.Name; // ชื่อผู้เล่น (key)
				var pObj = prop.Value;   // object ของผู้เล่น
				string pwd = "";   // รหัสผ่าน (ดีฟอลต์ค่าว่าง)
				string reg = "";   // วันที่สมัคร (ดีฟอลต์ค่าว่าง)

				if (pObj.ValueKind == JsonValueKind.Object)  // ถ้า pObj เป็น object
				{
					if (pObj.TryGetProperty("password", out var pwdElem)  // ถ้ามีฟิลด์ "password"
						&& pwdElem.ValueKind == JsonValueKind.String) // และเป็น string
						pwd = pwdElem.GetString() ?? "";  // ดึงค่า string ออกมา

					if (pObj.TryGetProperty("registered_at", out var regElem) // ถ้ามีฟิลด์ registered_at
						&& regElem.ValueKind == JsonValueKind.String)
						reg = regElem.GetString() ?? "";   // ดึงวันที่
					else if (pObj.TryGetProperty("CreatedAt", out var reg2Elem) // หรือใช้ฟิลด์เก่า CreatedAt
						&& reg2Elem.ValueKind == JsonValueKind.String)
						reg = reg2Elem.GetString() ?? "";
				}

				if (string.IsNullOrEmpty(reg))  // ถ้าไม่มีเวลาเลย
					reg = DateTime.UtcNow.ToString("o"); // ใส่เวลาปัจจุบัน (UTC ISO format)

				list.Add(new SaveData { PlayerName = name, Password = pwd, CreatedAt = reg }); // เพิ่มเข้าในลิสต์
			}

			return list;  // คืนค่าลิสต์ผู้เล่นทั้งหมด
		}
		catch (Exception ex)
		{
			GD.PushError("❌ Failed to parse JSON: " + ex.Message);  // ถ้ามี error ในการอ่าน JSON
			return new List<SaveData>();   // คืนลิสต์ว่าง
		}
	}

	// ฟังก์ชันสมัครผู้เล่นใหม่ (เพิ่ม record ลง user://players.json)
	public bool SavePlayer(string name, string password)
	{
		var doc = LeaderboardStore.LoadDoc();   // โหลดไฟล์ JSON ทั้งหมดมาเป็น GDict
		doc = LeaderboardStore.EnsureRoot(doc);  // ตรวจว่ามี root object "players" และ "leaderboards"
		var players = (GDict)doc["players"];   // ดึง object "players" ออกมา

		if (players.ContainsKey(name))  // ถ้ามีชื่อผู้เล่นนี้อยู่แล้ว
		{
			GD.Print("🚫 Duplicate name: " + name);  // แจ้งชื่อซ้ำ
			return false;  // สมัครไม่สำเร็จ
		}

		var nowIso = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");// เวลาปัจจุบัน (ISO + timezone)

		players[name] = new GDict {   // เพิ่มข้อมูลผู้เล่นใหม่เข้า Dictionary
			{ "password",      password }, // รหัสผ่าน
			{ "registered_at", nowIso   }, // วันที่สมัคร
			{ "levels",        new GDict() },    // object ว่างสำหรับบันทึก level ในอนาคต
			{ "current_level", 1 },  // เริ่มที่เลเวล 1
			{ "high_scores",   new GDict() }   // object ว่างสำหรับสกอร์สูงสุด
		};

		LeaderboardStore.SaveDoc(doc);   // บันทึกเอกสารกลับไป user://players.json
		MirrorToRes();   // mirror ไป res:// เพื่อดีบัก
		GD.Print("✅ Saved new user (new schema only): " + name);   // แจ้งว่าบันทึกผู้ใช้ใหม่สำเร็จ

		try
		{
			var plain = ConvertToPlainDict(doc);    // แปลงจาก GDict → Dictionary ปกติ (.NET)
			string json = JsonSerializer.Serialize(plain, new JsonSerializerOptions { WriteIndented = true }); // แปลงเป็น JSON พร้อมจัดบรรทัดสวย
			string realPath = ProjectSettings.GlobalizePath(DefaultPath);  // แปลง path res:// เป็น path จริง
			System.IO.File.WriteAllText(realPath, json);  // เขียนไฟล์ players.json ลง res://
			GD.Print($"✅ Saved players.json to RES (real path): {realPath}"); // แจ้งว่าเขียนสำเร็จ
		}
		catch (Exception ex)
		{
			GD.PushWarning($"⚠️ Could not write back to res:// → {ex.Message}"); // ถ้าเขียนไม่ได้ → เตือน
		}

		SetCurrentUserAndStampToday(new SaveData {   // ตั้งค่าผู้ใช้ปัจจุบันในหน่วยความจำ
			PlayerName = name,   // ชื่อผู้เล่น
			Password   = password,  // รหัสผ่าน
			CreatedAt  = nowIso  // วันที่สมัคร
		});

		CurrentPlayerName = name;  // เก็บชื่อไว้ให้ UI หรือ HUD ใช้
		return true;  // คืนค่า true แสดงว่าสมัครสำเร็จ
	}
	// ฟังก์ชันล็อกอินผู้ใช้เดิมจากไฟล์ user://players.json
	// ถ้ามีข้อมูล → ตั้งค่าผู้เล่นปัจจุบัน + sync ความคืบหน้าจากไฟล์
	public bool LoginExisting(string name, string password)
	{
		var list = LoadPlayers(); // โหลดผู้เล่นทั้งหมดจากไฟล์ JSON
		var user = list.Find(p => p.PlayerName == name && p.Password == password); // ค้นผู้เล่นที่ชื่อและรหัสตรงกัน
		if (user == null) return false; // ถ้าไม่เจอ → คืนค่า false (ล็อกอินไม่สำเร็จ)

		SetCurrentUserAndStampToday(user);  // ตั้งผู้เล่นปัจจุบันและอัปเดตวันที่วันนี้

		var doc = LeaderboardStore.LoadDoc();   // โหลดเอกสาร JSON รวม (Godot.Dictionary)
		doc = LeaderboardStore.EnsureRoot(doc); // ตรวจให้แน่ใจว่ามีโครงสร้าง root "players"/"leaderboards"
		var players = (GDict)doc["players"]; // ดึง object ของ players ออกมา

		if (!players.ContainsKey(name))  // ถ้าไม่มีชื่อผู้เล่นใน schema ใหม่
		{
			players[name] = new GDict {   // สร้าง record ใหม่ให้ผู้เล่นนี้
				{ "registered_at", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") }, // เวลาเวลาปัจจุบัน
				{ "levels",        new GDict() }, // object ว่างเก็บข้อมูล level
				{ "current_level", 1 }  // เริ่มต้นที่ level 1
			};
			GD.Print($"[LoginExisting] 🆕 Created new player record for {name}"); // แสดงว่าเพิ่งสร้าง record ใหม่
		}

		var p = (GDict)players[name];  // ดึงข้อมูลผู้เล่นออกมาเก็บในตัวแปร p

		if (p.ContainsKey("levels")) // ถ้ามี key "levels" (เคยเล่นแล้ว)
		{
			var levels = (GDict)p["levels"]; // ดึงข้อมูลระดับ (levels)
			int maxLv = 0; // ตัวแปรเก็บ level สูงสุด

			foreach (var key in levels.Keys) // วนทุก key (ชื่อ level เช่น "1", "2", "3")
			{
				if (int.TryParse(key.AsString(), out int lv)) // แปลงชื่อ key เป็นตัวเลข
					maxLv = Math.Max(maxLv, lv);   // เก็บค่า level สูงสุดที่เจอ
			}

			int current = p.ContainsKey("current_level") // ถ้ามี key current_level
				? (int)(long)p["current_level"]    // ดึงค่าปัจจุบันจาก dict
				: 1;  // ถ้าไม่มีให้ค่าเริ่มต้น = 1

			GD.Print($"[LoginExisting] 🔍 Loaded from file → current_level={current}, maxLvFound={maxLv}"); // แสดงผลดีบัก

			if ((int)(long)p["current_level"] > maxLv)  // ถ้า current_level มากกว่า level สูงสุด
			{
				p["current_level"] = maxLv;  // ลดค่าให้เท่ากับ maxLv
				GD.Print($"[LoginExisting] 🔧 Fixed current_level (was higher than levels) → now {maxLv}");
			}

			if (maxLv > current)  // ถ้า level ที่เล่นถึงจริงมากกว่าค่าที่เก็บไว้
			{
				p["current_level"] = maxLv; // อัปเดต current_level = maxLv
				GD.Print($"[LoginExisting] 🟢 Auto-recovered current_level set to {maxLv}"); // แจ้งว่ากู้คืนค่า
			}
			else
			{
				GD.Print($"[LoginExisting] ℹ️ Keep current_level = {current}"); // ถ้าไม่ต้องแก้ → แจ้งว่าคงค่าเดิม
			}

			GameProgress.CurrentLevelIndex = (int)(long)p["current_level"]; // sync ค่ากับระบบความคืบหน้าในเกม
		}
		else
		{
			GameProgress.CurrentLevelIndex = 1;// ถ้าไม่มีข้อมูล levels → เริ่มที่ level 1
			GD.Print($"[LoginExisting] ℹ️ No levels found → set current_level = 1"); // แสดงข้อความแจ้ง
		}

		LeaderboardStore.SaveDoc(doc); // เซฟเอกสารกลับลงไฟล์ user://players.json
		MirrorToRes(); // mirror ไป res:// (เพื่อดีบัก)
		CurrentPlayerName = name;    // ตั้งชื่อผู้เล่นปัจจุบันให้ใช้ใน HUD
		GD.Print($"[LoginExisting] * Loaded level {GameProgress.CurrentLevelIndex} for {name}"); // แสดง log ดีบัก
		return true;  // คืนค่า true แสดงว่าล็อกอินสำเร็จ
	}

	// Helpers: แปลงข้อมูลจาก Godot Variant → .NET ปกติ (serialize)

	// ฟังก์ชันแปลงค่า Variant ของ Godot ให้กลายเป็น object ปกติใน .NET
	// ใช้ก่อน serialize ด้วย System.Text.Json
	private static object ConvertVariantToPlain(object value)
	{
		if (value is GDict gd)  // ถ้าเป็น Godot.Dictionary
		{
			var dict = new Dictionary<string, object>(); // สร้าง Dictionary ปกติ
			foreach (var key in gd.Keys)  // วนทุก key ของ GDict
			{
				string keyStr = key.AsString(); // แปลง key ให้เป็น string
				dict[keyStr] = ConvertVariantToPlain(gd[key]);  // แปลงค่าด้านใน (recursive)
			}
			return dict;  // คืน Dictionary ที่แปลงแล้ว
		}
		else if (value is Godot.Collections.Array arr) // ถ้าเป็น Array ของ Godot
		{
			var list = new List<object>(); // สร้าง List ปกติ
			foreach (var item in arr)  // วนทุก element
				list.Add(ConvertVariantToPlain(item));  // แปลงทีละตัว (recursive)
			return list; // คืน List ที่แปลงแล้ว
		}
		else if (value is Variant var)   // ถ้าเป็น Variant (wrapper type ของ Godot)
		{
			return ConvertVariantToPlain(var.Obj);  // เรียกตัวจริงของ Variant.Obj มาแปลงต่อ
		}
		else
		{
			return value ?? "";   // ถ้าเป็นชนิดพื้นฐาน (int, string ฯลฯ) → คืนค่าเดิม
		}
	}

	// ฟังก์ชันแปลง GDict (รูท) → Dictionary ปกติ 
	// ใช้เรียกก่อน serialize เป็น JSON สวย ๆ
	private static Dictionary<string, object> ConvertToPlainDict(GDict gdict)
	{
		var result = new Dictionary<string, object>();  // สร้าง Dictionary เปล่า
		foreach (var key in gdict.Keys) // วนทุกคีย์ใน GDict
		{
			string keyStr = key.AsString();   // แปลงคีย์เป็น string
			result[keyStr] = ConvertVariantToPlain(gdict[key]); // แปลงค่าด้านในแต่ละอัน
		}
		return result; // คืน Dictionary ที่แปลงเสร็จ
	}
}
