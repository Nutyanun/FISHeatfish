using Godot;
using System;
using System.Linq;
using System.Text;

public partial class ScoreScene : Node2D
{
	[Export] public Label TitleLabel { get; set; }
	[Export] public Label ScoreLabel { get; set; }
	[Export] public Label HighScoreLabel { get; set; }
	[Export] public RichTextLabel FishSummary { get; set; }
	[Export] public Label BonusLabel { get; set; }
	[Export] public Label TotalLabel { get; set; }


	public override void _Ready()
	{
	int levelIndex = GameProgress.CurrentPlayingLevel;
	int score = GameProgress.LastLevelScore;
	int bonus = GameProgress.LastBonusScore;
	int total = GameProgress.LastTotalScore;

	// ✅ โหลด high score จากไฟล์จริงแทนที่จะใช้ GameProgress
	int high = LoadHighScoreForLevel(levelIndex);

	// ถ้าได้คะแนนใหม่สูงกว่า → บันทึกแทน
	if (total > high)
	{
		SaveHighScoreForLevel(levelIndex, total);
		high = total;
	}

	if (TitleLabel != null)
		TitleLabel.Text = $"Level {levelIndex} Summary";

	if (ScoreLabel != null)
		ScoreLabel.Text = $"Score: {score}";

	if (BonusLabel != null)
		BonusLabel.Text = $"Bonus: {bonus}";

	if (TotalLabel != null)
		TotalLabel.Text = $"Total: {total}";

	if (HighScoreLabel != null)
	HighScoreLabel.Text = $"High Score: {high}";

	ShowFishSummary();

	GD.Print($"[ScoreScene] FishScore={score}, Bonus={bonus}, Total={total}, High={high}");
	}
	
	private void ShowFishSummary()
	{
		if (FishSummary == null) return;
		if (GameProgress.FishCountByType == null || GameProgress.FishCountByType.Count == 0)
		{
			FishSummary.Text = "0  0  0  0";
			return;
		}

		// ✅ เรียงตามลำดับ fish1, fish2, fish3, shark
		string[] order = { "fish2", "fish3", "fish1", "shark" };
		var sb = new StringBuilder();

		foreach (var type in order)
		{
			int count = GameProgress.FishCountByType.ContainsKey(type)
				? GameProgress.FishCountByType[type]
				: 0;

			sb.Append(count.ToString().PadRight(6)); // เว้นช่องให้อ่านง่าย
		}

		FishSummary.Text = sb.ToString().TrimEnd();
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
