using Godot;
using System;

public partial class StartGame : Node2D
{
	 public override void _Ready(){
		// ตรวจเช็คว่าเจอปุ่มหรือไม่
		var btnStart = GetNodeOrNull<Button>("Sprite2D/StartButton");  // ปุ่ม START
		if (btnStart != null){
			btnStart.Pressed += OnStartPressed;
		}else{
			GD.PrintErr("not found Button(START)");
		}
		var btnHighScore = GetNodeOrNull<Button>("Sprite2D/HighscButton");  // ปุ่ม High Score
		if (btnHighScore != null){
			btnHighScore.Pressed += OnHighScorePressed;
		}else{
			GD.PrintErr("not found Button2(HighScore)");
		}
	
	}
	private void OnStartPressed(){
		GD.Print("STARAT"); //ถูกกด
		var err = GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
		if (err != Error.Ok)
			GD.PrintErr("เปลี่ยนไป Scenbp.tscn ไม่สำเร็จ: " + err.ToString());
	}
	private void OnHighScorePressed(){
		GD.Print("HIGHSCORE");//ถุกกด
		var err = GetTree().ChangeSceneToFile("res://SceneHighSC/HighScore.tscn");
		if (err != Error.Ok)
			GD.PrintErr("เปลี่ยนไป Scenhs.tscn ไม่สำเร็จ: " + err.ToString());
	}
}
