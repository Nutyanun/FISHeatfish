using Godot;
using System;

public partial class Gobackstartgame : TextureButton
{
	public override void _Ready()
	{
		GD.Print("Gobackbutton ready!");
		this.Pressed += OnExitButtonPressed;
	}

	private void OnExitButtonPressed()
	{
		GD.Print("exit button pressed!");
		GetTree().ChangeSceneToFile("res://SceneStartandHigh/Startgame.tscn");
	}
}
