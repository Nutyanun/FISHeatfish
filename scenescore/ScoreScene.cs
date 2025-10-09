using Godot;
using System;

public partial class ScoreScene : Node2D
{
	[Export] public Label TitleLabel { get; set; }
	[Export] public Label ScoreLabel { get; set; }
	[Export] public Label HighScoreLabel { get; set; }

	public override void _Ready()
	{
		// ดึงข้อมูลจาก GameProgress
		int levelIndex = GameProgress.CurrentPlayingLevel;
		int score = GameProgress.LastLevelScore;

		// แสดงชื่อด่าน
		if (TitleLabel != null)
			TitleLabel.Text = $"Level {levelIndex} Score";

		// แสดงคะแนนล่าสุด
		if (ScoreLabel != null)
			ScoreLabel.Text = $"Score: {score}";

		// แสดง high score (อ่านจากไฟล์ถ้ามี)
		int high = LoadHighScoreForLevel(levelIndex);
		if (HighScoreLabel != null)
			HighScoreLabel.Text = $"High Score: {high}";

		// อัปเดต high score ถ้าทำลายสถิติ
		//if (score > high)
		//{
			//SaveHighScoreForLevel(levelIndex, score);
			//if (HighScoreLabel != null)
				//HighScoreLabel.Text = $"High Score: {score} ";
		//}
	}

	// -------- ระบบเก็บ High Score แยกแต่ละด่าน --------
	private string GetHighScorePath(int levelIndex)
	{
		return $"user://highscore_level{levelIndex}.dat";
	}

	private int LoadHighScoreForLevel(int levelIndex)
	{
		var path = GetHighScorePath(levelIndex);
		if (!FileAccess.FileExists(path)) return 0;
		using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return (int)f.Get32();
	}

	private void SaveHighScoreForLevel(int levelIndex, int score)
	{
		using var f = FileAccess.Open(GetHighScorePath(levelIndex), FileAccess.ModeFlags.Write);
		f.Store32((uint)score);
	}
	
	public override void _UnhandledInput(InputEvent e)
	{
	if (e.IsActionPressed("ui_accept") || e.IsActionPressed("ui_cancel"))
	{
		// ปลดล็อกเฉพาะตอนเล่นด่านใหม่จริง
		if (GameProgress.CurrentPlayingLevel == GameProgress.CurrentLevelIndex + 1)
		{
			GameProgress.Advance();
			GD.Print($"[ScoreScene] Advance to Level {GameProgress.CurrentLevelIndex}");
		}
		else
		{
			GD.Print($"[ScoreScene] Replay level {GameProgress.CurrentPlayingLevel}, no advance.");
		}

		// กลับหน้า checkpoint
		GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
	}
	}


}
