using Godot;                     // นำเข้า namespace ของ Godot สำหรับใช้คลาส Node, Color ฯลฯ
using System.Linq;               // ใช้ LINQ (เช่น .OfType(), .OrderBy(), .Where()) เพื่อจัดการลิสต์

// คลาส CheckpointManager สืบทอดจาก Node2D → ใช้จัดการระบบด่านทั้งหมด
public partial class CheckpointManager : Node2D
{
	public override void _Ready()   // ฟังก์ชัน _Ready() จะทำงานอัตโนมัติเมื่อ Node ถูกโหลดเข้าฉาก
	{
		// ✅ โหลด progress ของผู้เล่นที่ล็อกอินล่าสุด จากไฟล์ players.json
		var login = PlayerLogin.Instance;         // ดึง instance ของ PlayerLogin (autoload singleton)
		
		// ตรวจว่ามีผู้เล่นล็อกอินอยู่หรือไม่
		if (login?.CurrentUser != null)
		{
			var name = login.CurrentUser.PlayerName;     // ดึงชื่อผู้เล่นจากข้อมูลล็อกอิน
			var doc = LeaderboardStore.LoadDoc();        // โหลดเอกสาร JSON จาก LeaderboardStore (ข้อมูลผู้เล่นทั้งหมด)

			// ถ้ามี key "players" อยู่ในเอกสาร
			if (doc.ContainsKey("players"))
			{
				var players = (Godot.Collections.Dictionary)doc["players"];  // แปลง section "players" เป็น Dictionary
				if (players.ContainsKey(name))                               // ตรวจว่ามีชื่อผู้เล่นนี้อยู่ในไฟล์ไหม
				{
					var p = (Godot.Collections.Dictionary)players[name];      // ดึงข้อมูลผู้เล่นคนนั้นออกมา
					
					// ถ้ามี key "current_level" → หมายถึงเคยผ่านถึงด่านนี้แล้ว
					if (p.ContainsKey("current_level"))
						GameProgress.CurrentLevelIndex = (int)(long)p["current_level"];  // แปลงค่าที่อ่านได้ (long → int) แล้วเก็บไว้ใน GameProgress
				}
			}
		}

		GameProgress.Load();        // โหลดข้อมูลความคืบหน้าล่าสุด (จากไฟล์ save เช่น user://save.dat)
		UpdateLevelVisual();        // เรียกฟังก์ชันเพื่ออัปเดต “สีและสถานะ” ของแต่ละด่าน
	}

	// ฟังก์ชันนี้จะอัปเดตการแสดงผลของด่านทั้งหมด เช่น สีแดง=ผ่านแล้ว, ขาว=ปัจจุบัน, เทา=ยังไม่ถึง
	private void UpdateLevelVisual()
	{
		// ดึงลูก Node ของ CheckpointManager ทั้งหมดที่เป็นประเภท Level
		// จากนั้นเรียงตามตัวเลขท้ายชื่อ เช่น Level1, Level2, Level3, ...
		var Levels = GetChildren()                                  // ดึง Node ลูกทั้งหมด
			.OfType<Level>()                                        // เลือกเฉพาะ Node ที่เป็นคลาส Level
			.OrderBy(m => ExtractNumber(m.Name.ToString()))          // เรียงตามตัวเลขท้ายชื่อ (เช่น "Level3" จะได้ค่า 3)
			.ToList();                                               // แปลงเป็น List เพื่อวนลูปง่ายขึ้น

		// วนซ้ำตรวจสอบทุกด่านในลิสต์
		for (int idx = 0; idx < Levels.Count; idx++)
		{
			var script = Levels[idx];          // ดึง Level ปัจจุบันจากลิสต์ (เช่น Level1, Level2, ...)

			// 🔴 ถ้าด่านนี้อยู่ “ก่อน” ด่านปัจจุบัน → ถือว่าผ่านแล้ว
			if (idx < GameProgress.CurrentLevelIndex)
			{
				script.SetDone(true);          // ตั้งสถานะว่า “ผ่านแล้ว”
				script.SetActive(false);       // ปิดไม่ให้กดเล่นอีก
				script.SelfModulate = new Color(1f, 0.4f, 0.4f);   // เปลี่ยนสีเป็นแดง
			}
			// ⚪ ถ้าเป็นด่านเดียวกับ CurrentLevelIndex → ด่านที่กำลังจะเล่น
			else if (idx == GameProgress.CurrentLevelIndex)
			{
				script.SetActive(true);        // เปิดให้เลือกเล่นได้
				script.SetDone(false);         // ยังไม่ถือว่าผ่าน
				script.SelfModulate = new Color(1, 1, 1);           // สีปกติ (ขาว)
			}
			// ⚫ ถ้ายังไม่ถึงด่านนี้ → ถือว่ายังล็อกอยู่
			else
			{
				script.SetDone(false);         // ยังไม่ผ่าน
				script.SetActive(false);       // ยังเลือกเล่นไม่ได้
				script.SelfModulate = new Color(0.6f, 0.6f, 0.6f);  // ทำให้ซีดลง (สีเทา)
			}
		}
	}

	// ฟังก์ชันช่วยดึงเฉพาะตัวเลขจากชื่อ Node เช่น "Level3" → 3
	private int ExtractNumber(string name)
	{
		// ดึงเฉพาะตัวเลขออกมาจากชื่อ Node ด้วย LINQ (Where + char.IsDigit)
		string digits = new string(name.Where(char.IsDigit).ToArray());
		
		// ถ้าไม่มีตัวเลขเลย → คืนค่า 0, ถ้ามี → แปลงเป็น int แล้วส่งกลับ
		return string.IsNullOrEmpty(digits) ? 0 : int.Parse(digits);
	}
	
	// ฟังก์ชันใช้รีเฟรชข้อมูลด่าน (เช่น หลังจากเล่นผ่าน 1 ด่าน)
	public void Refresh()
	{
		GameProgress.Load();     // โหลดข้อมูลล่าสุดจากไฟล์ save
		UpdateLevelVisual();     // อัปเดตการแสดงผลใหม่ (เช่น ด่านใหม่กลายเป็นสีขาว)
	}
}
