using Godot;
using System;

public partial class Mook1 : Sprite2D
{
	private float _time = 0f;

	public override void _Process(double delta)
	{
		_time += (float)delta * 5f; // ยิ่งเลขเยอะ → วิบวับเร็ว

		// ค่า brightness จาก 0.5 → 1.0 (ไม่มืดเกินไป)
		float brightness = (Mathf.Sin(_time) + 1f) / 4f + 0.5f;

		// ปรับความสว่างของมุก
		SelfModulate = new Color(brightness, brightness, brightness, 1f);
	}
	
	public override void _Input(InputEvent @event)
	{
		// เมาส์คลิกซ้าย
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			Vector2 mousePos = mb.Position;
			if (IsPointOverSprite(mousePos))
			{
				GD.Print("Clicked mook1 by mouse");
				GetTree().ChangeSceneToFile("res://scene/explaingame.tscn"); // แก้ path ตามไฟล์ Scene ของคุณ
			}
		}
	}
	private bool IsPointOverSprite(Vector2 globalPoint)
{
	var tex = Texture;
	if (tex == null) return false;

	Vector2 texSize = tex.GetSize();        // ขนาด texture
	Vector2 scaledSize = texSize * Scale;   // ขยายตาม Scale ของ Sprite

	// Sprite2D โดยปกติ Centered = true -> คำนวณจากจุดกึ่งกลาง
	Vector2 topLeft = GlobalPosition - scaledSize * 0.5f;
	Rect2 rect = new Rect2(topLeft, scaledSize);

	return rect.HasPoint(globalPoint);
}
}
