using Godot;
using System;

public partial class ScoreGoback : TextureButton
{
	[Export] public int LevelNumber = 1;

	public override void _Ready()
	{
		int level = GameProgress.CurrentPlayingLevel;
		GD.Print($"Show score for Level {level}");
		// โหลดคะแนนของเลเวลนี้มาโชว์
	}

	public override void _Pressed()
	{
	if (GameProgress.CurrentPlayingLevel == GameProgress.CurrentLevelIndex +1)
		{
		GameProgress.Advance();
		GD.Print("Advance to level: " + GameProgress.CurrentLevelIndex);
		}

	GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
	}
}
