using Godot;
using System;

public partial class Gobackmenu : Sprite2D
{
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent 
			&& mouseEvent.Pressed 
			&& mouseEvent.ButtonIndex == MouseButton.Left)
		{
			// แปลงตำแหน่งเมาส์มาเป็น local ของ Sprite
			Vector2 mousePos = ToLocal(mouseEvent.Position);

			// เช็คว่าเมาส์อยู่ใน sprite (texture rect)
			if (GetRect().HasPoint(mousePos))
			{
				GD.Print("Clicked GoBack Sprite!");
				GetTree().ChangeSceneToFile("res://scene/checkpoint.tscn"); // แก้ path ให้ตรงกับซีนจริง
			}
		}
	}
}
