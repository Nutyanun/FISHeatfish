using Godot;
using System;

public partial class GoScore : Button
{
	[Export] public int LevelNumber = 1;
	
	public override void _Ready()
	{
		this.Pressed += OnExitButtonPressed;
	}

	private void OnExitButtonPressed()
{
	GameProgress.CurrentPlayingLevel = LevelNumber; // จดว่าเป็นสกอร์ของเลเวลนี้
	GetTree().ChangeSceneToFile($"res://scenescore/score{LevelNumber}.tscn");
}
}
