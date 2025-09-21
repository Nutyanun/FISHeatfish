using Godot;
using System;

public partial class HighScore : Node2D
{
	 public override void _Ready()
{
	// หา Node ที่ชื่อว่า "TextureButton"
	var texBtn = GetNode<TextureButton>("Sprite2D/TextureButton");
	if (texBtn == null)
	{
		GD.PrintErr("❌ ไม่เจอ TextureButton");
		return;
	}
	texBtn.Pressed += OnTextureButtonPressed;// เชื่อม Signal กดปุ่ม
}
private void OnTextureButtonPressed()
{
	GD.Print("TextureButton ถูกกดแล้ว!");
	GetTree().ChangeSceneToFile("res://SceneStartandHigh/StartGame.tscn");// เปลี่ยนฉาก 
}
}
