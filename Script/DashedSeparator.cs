using Godot;    // ใช้ namespace ของ Godot เพื่อเข้าถึงคลาสต่าง ๆ เช่น Control, Color, Vector2, ฯลฯ
using System;   // ใช้ namespace มาตรฐานของ .NET ที่มี MathF และอื่น ๆ

// สร้างคลาสชื่อ DashedSeparator ซึ่งสืบทอดจาก Control
// ใช้สำหรับวาดเส้นประแนวนอน (เหมือนตัวแบ่งส่วนใน UI)
public partial class DashedSeparator : Control
{
	// [Export] ทำให้ปรับค่าได้ใน Inspector ของ Godot
	[Export] public float Thickness { get; set; } = 2f; // ความหนาของเส้น
	[Export] public float Dash { get; set; } = 10f;     // ความยาวของเส้นแต่ละช่วง
	[Export] public float Gap { get; set; } = 6f;       // ระยะช่องว่างระหว่างเส้น
	[Export] public Color LineColor { get; set; } = new Color(1, 1, 1, 0.22f); // สีของเส้น (ขาวโปร่ง 22%)

	// เมธอดนี้จะถูกเรียกเมื่อ Node พร้อมใช้งาน (ตอนเริ่มต้น)
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore; 
		// ตั้งค่าไม่ให้ตัวนี้รับการคลิกเมาส์ (ปล่อยให้ event ผ่านไปยัง UI ด้านล่าง)
	}

	// ฟังก์ชันวาด (Godot จะเรียกเมื่อจำเป็น เช่น ตอนสร้าง, resize, หรือ QueueRedraw)
	public override void _Draw()
	{
		float y = Size.Y * 0.5f; 
		// หาค่ากึ่งกลางแนวตั้งของคอนโทรล เพื่อวาดเส้นอยู่ตรงกลาง

		// วนลูปตั้งแต่ซ้ายสุด (x=0) ไปจนถึงขวาสุดของความกว้าง (Size.X)
		for (float x = 0; x < Size.X; x += Dash + Gap)
		{
			float x2 = MathF.Min(x + Dash, Size.X); 
			// จุดสิ้นสุดของเส้นแต่ละช่วง (ถ้าเกินขนาดจริงให้ตัดให้เท่ากับขนาดจอ)

			// วาดเส้นจากจุด (x,y) ไป (x2,y)
			DrawLine(new Vector2(x, y), new Vector2(x2, y), LineColor, Thickness);
			// ใช้สีและความหนาตามที่ตั้งไว้
		}
	}

	// ฟังก์ชันนี้ใช้รับการแจ้งเตือนจาก Godot เช่น เมื่อ resize หรือ theme เปลี่ยน
	public override void _Notification(int what)
	{
		if (what == (int)NotificationResized || what == (int)NotificationThemeChanged)
			QueueRedraw(); 
			// ให้รีเฟรชการวาดใหม่ เพื่อให้เส้นประขนาด/สีอัปเดตตาม UI
	}
}
