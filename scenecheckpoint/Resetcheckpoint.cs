using Godot;
using System;

public partial class Resetcheckpoint : Button
{
	public override void _Pressed()
	{
		// ✅ รีเซ็ตเฉพาะความคืบหน้าของตัวเอง
		GameProgress.CurrentLevelIndex = 0;
		GameProgress.LastHighScore = 0;
		GameProgress.LastLevelScore = 0;
		GameProgress.Save(); // เซฟสถานะใหม่ของตัวเองไว้

		GD.Print("[Resetcheckpoint] Reset GameProgress only for current user.");

		// ✅ กลับไปหน้า checkpoint
		GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
	}
}
