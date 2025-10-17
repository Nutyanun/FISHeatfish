using Godot;                   // ใช้คลาสหลักจาก Godot เช่น Node, Variant
using System;                  // ใช้ฟังก์ชันพื้นฐาน เช่น Math

// คลาส CrystalDirector ใช้ "ควบคุมการเกิดคริสตัลแต่ละสี" ตามระดับเลเวลของเกม
public partial class CrystalDirector : Node
{
	// ===== Export variables =====
	[Export] public NodePath ScoreManagerPath { get; set; } = null; // Path ไปยัง Node ScoreManager (กำหนดใน Inspector)
	[Export] public NodePath SpawnerPath { get; set; } = null;      // Path ไปยัง Node CrystalSpawner (กำหนดใน Inspector)

	// ===== private fields =====
	private Node _sm;                   // ตัวชี้ไปยัง ScoreManager (ไม่ fix type เพื่อให้ยืดหยุ่น)
	private CrystalSpawner _spawner;    // ตัวชี้ไปยัง CrystalSpawner จริง

	private int _lastLevel = -1;        // เก็บด่านล่าสุดที่ตรวจเจอ (ไว้เช็คการเปลี่ยนด่าน)
	private float _prevTime = 9999f;    // เวลาคงเหลือเฟรมก่อนหน้า (ใช้ดูช่วง 20 วิสุดท้าย)
	private bool _pinkTriggered = false; // ธงป้องกันไม่ให้ spawn คริสตัลชมพูซ้ำหลายรอบ

	// ===== ฟังก์ชันจะถูกเรียกเมื่อ Node พร้อมทำงานใน Scene =====
	public override void _Ready()
	{
		// พยายามดึง Node ScoreManager ตาม path ที่กำหนดใน Inspector
		_sm = (ScoreManagerPath != null && !ScoreManagerPath.IsEmpty) // ถ้ามีการตั้งค่า NodePath ไว้ใน Inspector
		? GetNodeOrNull(ScoreManagerPath)                       // → ใช้ path นั้นเพื่อหา Node โดยตรง
	  	: GetTree().CurrentScene?.FindChild("ScoreManager", true, false); // ถ้าไม่มี path → ค้นหา Node ที่ชื่อ "ScoreManager" ใน Scene ปัจจุบันแบบ recursive

		// พยายามดึง Node CrystalSpawner ตาม path ที่กำหนดใน Inspector
		_spawner = (SpawnerPath != null && !SpawnerPath.IsEmpty) // ถ้ามีการตั้งค่า NodePath ไว้ใน Inspector
		? GetNodeOrNull(SpawnerPath) as CrystalSpawner // → ดึง Node จาก path แล้วแปลงชนิดเป็น CrystalSpawner
		: GetTree().CurrentScene?.FindChild("CrystalSpawner", true, false) as CrystalSpawner; // ถ้าไม่มี path → ค้นหา Node ที่ชื่อ "CrystalSpawner" ใน Scene ปัจจุบัน

		// ถ้าไม่เจอ Node ใด ๆ ให้เตือนใน console
		if (_sm == null) GD.PushWarning("[CrystalDirector] ScoreManager not found.");      // เตือนถ้าไม่พบ ScoreManager
		if (_spawner == null) GD.PushWarning("[CrystalDirector] CrystalSpawner not found."); // เตือนถ้าไม่พบ CrystalSpawner

		// ดึงค่าเลเวลปัจจุบันจาก ScoreManager ถ้ามี (ค่าเริ่มต้น = 1)
		int lv = GetInt("Level", 1);

		// เรียกใช้ ApplyLevelRule() ครั้งแรกเลย เพื่อกำหนดรูปแบบการปล่อยคริสตัลตามด่าน
		ApplyLevelRule(lv);

		// เก็บสถานะเริ่มต้นไว้ใช้เปรียบเทียบตอน Process
		_lastLevel = lv;
		_pinkTriggered = false;
		_prevTime = GetFloat("TimeLeftSec", 9999f);
	}

	// ===== ฟังก์ชันทำงานทุกเฟรม =====
	public override void _Process(double delta)
	{
		// ถ้า node ที่จำเป็นยังไม่พร้อม → ออกก่อน
		if (_sm == null || _spawner == null) return;

		// อ่านค่าปัจจุบันจาก ScoreManager
		int level = GetInt("Level", 1);             // ด่านปัจจุบัน
		float timeLeft = GetFloat("TimeLeftSec", 9999f); // เวลาที่เหลือของด่าน

		// ถ้าด่านเปลี่ยน (เลเวลไม่เท่าเดิม) → เรียกเปลี่ยนกติกา spawn
		if (level != _lastLevel)
		{
			ApplyLevelRule(level);   // ตั้งค่าคริสตัลใหม่ตามเลเวล
			_lastLevel = level;      // บันทึกเลเวลล่าสุด
			_pinkTriggered = false;  // รีเซ็ตธงชมพู (เริ่มนับใหม่)
		}

		// (ตัวอย่างโค้ดถูกปิดไว้) — ใช้สำหรับปล่อยคริสตัลชมพู 1 ชิ้นในช่วง 20 วิสุดท้าย
		/*
		if (level >= 4 && !_pinkTriggered && _prevTime > 20f && timeLeft <= 20f)
		{
			_spawner.ForcePinkOnceInLastWindow(); // สั่ง spawn คริสตัลชมพูทันที
			_pinkTriggered = true;                 // ป้องกันไม่ให้เกิดซ้ำ
		}
		*/

		// อัปเดตเวลาเฟรมก่อนหน้าไว้เปรียบเทียบในรอบต่อไป
		_prevTime = timeLeft;
	}

	// ===== ฟังก์ชันกำหนดรูปแบบการปล่อยคริสตัลตามเลเวล =====
	private void ApplyLevelRule(int level)
	{
		// ด่าน 1–3: ยังไม่ให้มีคริสตัลเลย
		if (level <= 3)
		{
			_spawner.ApplyRule(Array.Empty<CrystalType>(), 60f, 0); // ไม่มีสี, interval 60 วิ, จำนวนสูงสุด 0
			_spawner.ResetPinkForced();                             // รีเซ็ตสถานะคริสตัลชมพู
			return;
		}

		// ตัวแปรภายในของแต่ละด่าน
		CrystalType[] colors; // รายชื่อสีที่จะปล่อย
		int maxOnScreen;      // จำนวนสูงสุดที่มีบนจอในเวลาเดียวกัน

		// ตั้งค่าตามระดับเลเวล
		if (level == 4)
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue }; // เริ่มมีเขียว+ฟ้า
			maxOnScreen = 2;
		}
		else if (level == 5)
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue, CrystalType.Purple }; // เพิ่มม่วง
			maxOnScreen = 3;
		}
		else if (level == 6)
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red }; // เพิ่มแดง
			maxOnScreen = 4;
		}
		else // ด่าน 7 ขึ้นไป
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red, CrystalType.Pink }; // ปลดล็อกครบทุกสี
			maxOnScreen = 5;
		}

		// ส่งค่ากติกาไปให้ CrystalSpawner ใช้
		_spawner.ApplyRule(colors, 60f, maxOnScreen);

		// รีเซ็ตสถานะการบังคับ spawn คริสตัลชมพู (เผื่อรอบใหม่)
		_spawner.ResetPinkForced();
	}

	// ===== ฟังก์ชันช่วยอ่านค่า int จาก ScoreManager อย่างปลอดภัย =====
	private int GetInt(string name, int def)
	{
		try
		{
			Variant v = _sm?.Get(name) ?? def; // ดึงค่าด้วยชื่อ property (อาจเป็น field หรือ export)
			return v.VariantType switch
			{
				Variant.Type.Int => (int)(long)v,                   // ถ้าเป็น int → แปลงตรง ๆ
				Variant.Type.Float => (int)Mathf.Round((float)(double)v), // ถ้าเป็น float → ปัดเป็น int
				_ => def                                            // ถ้าไม่รู้จักชนิด → คืนค่าดีฟอลต์
			};
		}
		catch { return def; } // ถ้ามีข้อผิดพลาด → คืนค่าดีฟอลต์
	}

	// ===== ฟังก์ชันช่วยอ่านค่า float จาก ScoreManager อย่างปลอดภัย =====
	private float GetFloat(string name, float def)
	{
	try
	{
		// พยายามดึงค่า property จาก Node _sm (ScoreManager)
		// ถ้า _sm ยังไม่มีหรือดึงไม่สำเร็จ → ใช้ค่าดีฟอลต์ def แทน
		Variant v = _sm?.Get(name) ?? def;

		// ตรวจชนิดข้อมูลของค่า v ที่ดึงมา แล้วแปลงเป็น float ตามความเหมาะสม
		return v.VariantType switch
		{
		Variant.Type.Float => (float)(double)v, // ถ้าเป็น float → แปลงจาก double แล้วคืนค่าได้เลย
		Variant.Type.Int => (float)(long)v,     // ถ้าเป็น int → แปลงเป็น float เพื่อให้ใช้ได้เหมือนกัน
		_ => def                                // ถ้าเป็นชนิดอื่น เช่น string หรือ null → คืนค่าดีฟอลต์ def
		};
	}
	catch
		{
		// ถ้ามีข้อผิดพลาดใด ๆ (เช่น node ไม่มี property ชื่อนี้)
		// → คืนค่าดีฟอลต์แทน เพื่อกันเกม error หรือหยุดทำงาน
		return def;
		}
	}
}
