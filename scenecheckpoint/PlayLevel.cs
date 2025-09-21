using Godot;
using System;

public partial class PlayLevel : Sprite2D
{	
	public override void _Input(InputEvent @event)
	{
		// เมาส์คลิกซ้าย
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			Vector2 mousePos = mb.Position;
			if (IsPointOverSprite(mousePos))
			{
				GD.Print("Clicked mook1 by mouse");
				GetTree().ChangeSceneToFile("res://sceneplaying/playing.tscn"); // แก้ path ตามไฟล์ Scene ของคุณ
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
