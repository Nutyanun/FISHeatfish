using Godot;          // ใช้คลาสของ Godot เช่น Node, Sprite2D, InputEvent
using System;         // ใช้ฟังก์ชันพื้นฐานของ .NET

// คลาส PlayLevel แทน “ปุ่มเข้าเล่นเกม” (มักอยู่ในหน้าอธิบายเกม หรือหน้าเริ่มต้น)
// สืบทอดจาก Sprite2D เพราะใช้รูปภาพเป็นปุ่มให้กด
public partial class PlayLevel : Sprite2D
{	
	// ฟังก์ชันตรวจจับการคลิกเมาส์
	public override void _Input(InputEvent @event)
	{
		// ตรวจว่าผู้ใช้กดปุ่มเมาส์หรือไม่ (MouseButton Event)
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			// ดึงตำแหน่งของเมาส์ตอนที่คลิก
			Vector2 mousePos = mb.Position;

			// ตรวจว่าจุดที่คลิกอยู่บนสไปรต์นี้หรือไม่
			if (IsPointOverSprite(mousePos))
			{
				// แสดงข้อความใน Output Console เพื่อ debug
				GD.Print("Clicked mook1 by mouse");

				// เปลี่ยนฉากไปยัง “playing.tscn” (หน้าที่เล่นเกมจริง)
				// ตรง path นี้สามารถแก้ให้ตรงกับไฟล์ Scene ของเราได้เลย
				GetTree().ChangeSceneToFile("res://sceneplaying/playing.tscn");
			}
		}
	}

	// ฟังก์ชันช่วยตรวจว่าตำแหน่งที่คลิกอยู่ในกรอบของ Sprite หรือไม่
	private bool IsPointOverSprite(Vector2 globalPoint)
	{
		var tex = Texture;                   // ดึง Texture ของ Sprite ที่ผูกไว้
		if (tex == null) return false;       // ถ้า Sprite ยังไม่มีภาพ → ออกเลย (กัน Error)

		Vector2 texSize = tex.GetSize();     // ขนาดจริงของ Texture (กว้าง x สูง)
		Vector2 scaledSize = texSize * Scale; // ขนาดหลังจากถูกปรับขยายด้วยค่า Scale

		// คำนวณตำแหน่งมุมบนซ้ายของ Sprite
		// โดยปกติ Sprite2D มี Centered = true → ตำแหน่ง GlobalPosition คือจุดกึ่งกลาง
		Vector2 topLeft = GlobalPosition - scaledSize * 0.5f;

		// สร้างกรอบสี่เหลี่ยมครอบ Sprite ตามตำแหน่งและขนาดจริง
		Rect2 rect = new Rect2(topLeft, scaledSize);

		// ตรวจว่าจุดคลิก (globalPoint) อยู่ในกรอบสี่เหลี่ยมนี้ไหม
		return rect.HasPoint(globalPoint);
	}
}
