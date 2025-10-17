using Godot;                         // ใช้คลาสของ Godot (Node, FileAccess, GD.Print ฯลฯ)
using System;                        // ใช้ฟังก์ชันพื้นฐานของ .NET (เช่น Exception)
using System.Text.Json;              // ใช้สำหรับแปลงข้อมูลเป็น JSON และอ่านกลับ
using System.Collections.Generic;    // ใช้สำหรับ Dictionary<> และ List<>

// คลาส GameProgress ใช้เก็บความคืบหน้าของผู้เล่น เช่น ด่านที่ถึง, คะแนนล่าสุด ฯลฯ
public partial class GameProgress : Node
{
	// ตัวแปรสถานะหลัก (เป็น static เพราะแชร์ข้ามซีนได้)
	public static int CurrentLevelIndex = 1;    // เก็บว่าปัจจุบันปลดล็อกถึงด่านที่เท่าไร
	public static int CurrentPlayingLevel = 1;  // เก็บว่ากำลังเล่นด่านไหนอยู่
	public static int LastLevelScore = 0;       // คะแนนที่ได้ในด่านล่าสุด
	public static int LastBonusScore { get; set; } = 0;  // คะแนนโบนัสล่าสุด
	public static int LastTotalScore { get; set; } = 0;  // คะแนนรวม (รวมโบนัส)
	public static int LastHighScore { get; set; } = 0;   // คะแนนสูงสุดล่าสุด

	// ใช้ระบุว่าผู้เล่นผ่านด่านล่าสุดแล้วหรือยัง (true = ผ่านแล้ว)
	public static bool IsLevelCleared { get; set; } = false;

	//  กำหนด path ที่ใช้เซฟไฟล์เกม (อยู่ใน user data ของเครื่อง)
	private static readonly string SavePath = "user://savegame.json";

	// เก็บจำนวนปลาที่กินได้ของแต่ละชนิด เช่น FishCountByType["fish1"] = 5
	public static Dictionary<string, int> FishCountByType = new();

	// ฟังก์ชันรีเซ็ตข้อมูลปลาทั้งหมด (เรียกทุกครั้งที่เริ่มเล่นด่านใหม่)
	public static void ResetFishCount()
	{
		FishCountByType.Clear();   // ล้างข้อมูลใน Dictionary ทั้งหมด
	}

	// ฟังก์ชันเพิ่มจำนวนปลาที่กินได้ของชนิดที่กำหนด
	public static void AddFishCount(string fishType)
	{
		// ถ้ายังไม่มี key ของปลาชนิดนี้ใน Dictionary → เพิ่มใหม่
		if (!FishCountByType.ContainsKey(fishType))
			FishCountByType[fishType] = 0;

		// เพิ่มจำนวนปลาชนิดนั้น +1
		FishCountByType[fishType]++;
	}

	// เรียกอัตโนมัติเมื่อ Node นี้ถูกโหลดใน Scene → โหลดข้อมูลเกมจากไฟล์
	public override void _Ready()
	{
		Load();   // โหลดข้อมูลจากไฟล์ savegame.json ถ้ามีอยู่
	}

	// ฟังก์ชัน Advance() ใช้ตอนผ่านด่าน → เพิ่มด่านถัดไปและเซฟ
	public static void Advance()
	{
		CurrentLevelIndex++;  // เพิ่มด่านที่ปลดล็อกขึ้น 1
		Save();               // เซฟข้อมูลใหม่ลงไฟล์
	}

	// รีเซ็ตความคืบหน้า (ใช้ตอนเริ่มเกมใหม่)
	public static void Reset()
	{
		CurrentLevelIndex = 0;  // กลับไปเริ่มด่านแรก
		Save();                 // บันทึกไฟล์ใหม่ (ค่า reset)
	}

	// ฟังก์ชัน Save() → บันทึกข้อมูลลงไฟล์ JSON
	public static void Save()
	{
		// สร้าง object SaveData แล้วแปลงเป็น JSON string
		var json = JsonSerializer.Serialize(new SaveData
		{
			currentLevel = CurrentLevelIndex   // เก็บแค่ข้อมูลด่านปัจจุบัน
		});

		// เปิดไฟล์ในโหมด Write (เขียนทับ)
		using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		f.StoreString(json);   // เขียนข้อความ JSON ลงไฟล์
	}

	// ฟังก์ชัน Load() → โหลดข้อมูลจากไฟล์กลับมาใช้ในเกม
	public static void Load()
	{
		// ถ้าไฟล์ save ยังไม่มี ให้ข้ามออกไปเลย
		if (!FileAccess.FileExists(SavePath)) return;

		// เปิดไฟล์ในโหมด Read (อ่านอย่างเดียว)
		using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);

		try
		{
			// แปลงข้อมูลในไฟล์ (string) กลับมาเป็น object SaveData
			var data = JsonSerializer.Deserialize<SaveData>(f.GetAsText());

			// ถ้าอ่านสำเร็จและไม่ null → อัปเดต CurrentLevelIndex
			if (data != null) CurrentLevelIndex = data.currentLevel;
		}
		catch (Exception e)
		{
			// ถ้าอ่านไฟล์พังหรือ JSON ผิด → แสดง error บน console
			GD.PrintErr("Load error: " + e.Message);
		}
	}

	// คลาสย่อยภายใน (ใช้เป็นโครงสร้างข้อมูลสำหรับเซฟ)
	private class SaveData
	{
		// เก็บแค่ค่า currentLevel เพื่อ serialize เป็น JSON
		public int currentLevel { get; set; }
	}
}
