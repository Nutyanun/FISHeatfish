using Godot;
using System;

public partial class Resetcheckpoint : Button
{
	public override void _Pressed()
	{
		GameProgress.Reset();                                // กลับ index = 0 + เซฟ
		GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
	}
}
