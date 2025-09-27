using Godot;
using System;

public partial class Playbutton : TextureButton
{
	[Export] public int LevelNumber = 1;
	
	public override void _Ready()
	{
		GD.Print("Gobackbutton ready!");
		this.Pressed += OnExitButtonPressed;
	}

	private void OnExitButtonPressed()
	{
		GD.Print("exit button pressed!");
		GetTree().ChangeSceneToFile("res://scenes/main.tscn");
	}
}
