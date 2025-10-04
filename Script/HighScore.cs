using Godot;
using System;
using System.Collections.Generic;

public partial class HighScore : Node2D
{
	private VBoxContainer _listContainer; // ไว้เก็บ Label ของผู้เล่น
	private TextureButton _returnBtn;     // ปุ่ม RETURN

	public override void _Ready()
	{
		// หา Node ที่ใช้แสดงรายชื่อผู้เล่น (VBoxContainer)
		_listContainer = GetNode<VBoxContainer>("Sprite2D2/VBoxContainer");


		// หา TextureButton สำหรับปุ่ม RETURN
		_returnBtn = GetNode<TextureButton>("Sprite2D/TextureButton");
		if (_returnBtn != null)
			_returnBtn.Pressed += OnTextureButtonPressed;

		// โหลดข้อมูลผู้เล่นจาก PlayerLogin
		if (PlayerLogin.Instance != null)
		{
			var players = PlayerLogin.Instance.LoadPlayers();

			// ล้าง label เก่า
			foreach (Node child in _listContainer.GetChildren())
			{
				child.QueueFree();
			}

			// เพิ่ม Label ของผู้เล่นแต่ละคน
			foreach (var player in players)
			{
				Label lbl = new Label();
				lbl.Text = $"{player.PlayerName}";
				lbl.AddThemeFontSizeOverride("font_size", 20); // ปรับขนาดตัวอักษร
				_listContainer.AddChild(lbl);

				GD.Print($"✅ Added player label: {player.PlayerName}");
			}
		}
		else
		{
			GD.PrintErr("❌ PlayerLogin.Instance is NULL, autoload ไม่ทำงาน");
		}
	}

	private void OnTextureButtonPressed()
	{
		GD.Print("✅ RETURN กดแล้ว → กลับไปหน้า StartGame");
		GetTree().ChangeSceneToFile("res://SceneStartandHigh/StartGame.tscn");
	}
}
