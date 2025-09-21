using Godot;
using System;

public partial class Gobackbutton : TextureButton
{
	public override void _Ready()
	{
		GD.Print("Gobackbutton ready!");
		this.Pressed += OnExitButtonPressed;
	}

	private void OnExitButtonPressed()
	{
		GD.Print("exit button pressed!");
		GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
	}
}
