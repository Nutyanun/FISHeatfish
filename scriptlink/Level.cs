using Godot;          // ใช้คลาสหลักของ Godot เช่น Node, Color, Vector2
using System;         // ใช้ฟีเจอร์พื้นฐานของ C# (.NET)

// คลาส Level ใช้แทน “ปุ่มด่าน” หรือ “ไอคอนด่าน” บนหน้าเลือกด่าน
// สืบทอดจาก Sprite2D เพราะใช้รูปภาพของด่านเป็นตัวแสดงผล
public partial class Level : Sprite2D
{
	// ตัวแปรภายใน (private) สำหรับสถานะของด่าน
	private float _time = 0f;       // เวลาใช้คำนวณเอฟเฟกต์วิบวับ
	private bool _isActive = false; // ด่านที่สามารถเลือกเล่นได้
	private bool _isDone = false;   // ด่านที่ผ่านแล้ว

	// ฟังก์ชันเปิด/ปิดสถานะ Active ของด่าน
	public void SetActive(bool active)
	{
		_isActive = active;       // กำหนดค่าว่า active หรือไม่
		SetProcess(true);         // เปิดให้ _Process() ทำงานตลอด (จำเป็นถ้ามี effect)
	}

	// ฟังก์ชันกำหนดสถานะว่าผ่านแล้วหรือยัง
	public void SetDone(bool done)
	{
		_isDone = done;           // true = ผ่านแล้ว
	}

	// ฟังก์ชันหลักที่ Godot เรียกทุกเฟรม → ใช้สำหรับอัปเดต effect ของด่าน
	public override void _Process(double delta)
	{
		if (_isActive)
		{
			// เอฟเฟกต์วิบวับ (ใช้ sine wave ทำให้สีสว่างขึ้น-ลงเรื่อย ๆ)
			_time += (float)delta * 5f;                         // เพิ่มค่าตามเวลา
			float brightness = (Mathf.Sin(_time) + 1f) / 4f + 0.5f;  // คำนวณค่าความสว่าง
			SelfModulate = new Color(brightness, brightness, brightness, 1f); // ตั้งค่าสี
		}
		else if (_isDone)
		{
			// ถ้าผ่านแล้ว → แสดงเป็นสีแดง
			SelfModulate = new Color(1f, 0.4f, 0.4f, 1f);
		}
		else
		{
			// ถ้ายังไม่ถึงด่านนี้ → สีขาวปกติแต่ไม่วิบวับ (ซีดลงได้ถ้าต้องการ)
			SelfModulate = new Color(1f, 1f, 1f, 1f);
		}
	}

	// ฟังก์ชันรับอินพุตจากผู้ใช้ เช่น การคลิกเมาส์
	public override void _Input(InputEvent @event)
	{
		// ถ้าด่านนี้ยังไม่ active และยังไม่ผ่าน → ห้ามคลิก
		if (!_isActive && !_isDone) return;

		// ตรวจว่าเป็นการคลิกเมาส์ซ้าย (Pressed = true)
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			Vector2 mousePos = mb.Position;    // ดึงตำแหน่งเมาส์ตอนคลิก

			// ตรวจว่าจุดที่คลิกอยู่บน sprite ของด่านนี้ไหม
			if (IsPointOverSprite(mousePos))
			{
				// ถ้าเป็นด่านที่ active (เล่นได้)
				if (_isActive)
				{
					// บันทึกหมายเลขด่านปัจจุบันที่กำลังจะเล่น
					GameProgress.CurrentPlayingLevel = LevelNumber;  

					// สลับไปหน้าอธิบายเกมของด่านนั้น (เช่น explaingame1, explaingame2, ...)
					GetTree().ChangeSceneToFile($"res://sceneexplaingame/explaingame{LevelNumber}.tscn");

					// หมายเหตุ: ยังไม่เรียก Advance() ที่นี่
					// เพราะจะให้เรียกตอนจบด่าน (ในหน้า Win Scene) เท่านั้น
				}
				// ถ้าด่านนี้ “ผ่านแล้ว” → อนุญาตให้เล่นซ้ำได้
				else if (_isDone)
				{
					GameProgress.CurrentPlayingLevel = LevelNumber;  
					GetTree().ChangeSceneToFile($"res://sceneexplaingame/explaingame{LevelNumber}.tscn");
				}
			}
		}
	}

	// ฟังก์ชันตรวจว่าคลิกโดนพื้นที่ของสไปรต์หรือไม่
	private bool IsPointOverSprite(Vector2 globalPoint)
	{
		var tex = Texture;               // ดึง texture ของ Sprite
		if (tex == null) return false;   // ถ้ายังไม่มีภาพ → ออกเลย

		Vector2 texSize = tex.GetSize();           // ขนาดจริงของภาพ
		Vector2 scaledSize = texSize * Scale;      // ขนาดภาพหลังจากถูกย่อ/ขยายใน Editor
		Vector2 topLeft = GlobalPosition - scaledSize * 0.5f;  // คำนวณมุมบนซ้ายของ sprite
		Rect2 rect = new Rect2(topLeft, scaledSize);           // สร้างกรอบสี่เหลี่ยมครอบ sprite

		return rect.HasPoint(globalPoint);         // คืนค่า true ถ้าจุดคลิกอยู่ในกรอบนี้
	}

	// ตัวแปร Export → ปรับค่าได้ใน Inspector ของ Godot
	[Export] public int LevelNumber { get; set; } = 1;  // หมายเลขด่าน (เช่น 1, 2, 3, ...)
}
