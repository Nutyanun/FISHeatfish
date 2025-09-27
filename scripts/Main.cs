using Godot;

public partial class Main : Node
{
	private T FindNodeSmart<T>(string unique, string a, string b) where T : Node =>
		GetNodeOrNull<T>(unique) ?? GetNodeOrNull<T>(a) ?? GetNodeOrNull<T>(b);

	public override void _Ready()
	{
		GetTree().Paused = false;

		var hud = FindNodeSmart<Hud>("%HUD", "/root/Main/HUD", "/root/HUD");
		var sm  = FindNodeSmart<ScoreManager>("%ScoreManager", "/root/Main/ScoreManager", "/root/ScoreManager");

		if (hud == null) { GD.PushError("[Main] HUD not found"); return; }
		if (sm  == null) { GD.PushError("[Main] ScoreManager not found"); return; }

		// ตั้งค่าเริ่มต้นบน HUD
		hud.HideOverlay();
		hud.UpdateScore(sm.Score, sm.TargetScore);
		hud.UpdateLives(sm.Lives);
		hud.UpdateLevel(sm.Level);
		hud.UpdateTotalScore(sm.TotalScore, sm.HighScore);   // <-- เรียกเมธอดตรง ๆ

		// ต่อสัญญาณ
		sm.ScoreChanged       += hud.UpdateScore;
		sm.TotalScoreChanged  += hud.UpdateTotalScore;
		sm.LivesChanged       += hud.UpdateLives;
		sm.LevelChanged       += hud.UpdateLevel;
		sm.MultiplierChanged  += hud.UpdateMultiplier;
		sm.TimeLeftChanged    += hud.UpdateTimer;
		sm.LevelCleared       += hud.ShowLevelClear;
		sm.GameOver           += hud.ShowGameOver;

		GD.Print("[Main] signals wired");
	}
}
