using Godot;

public partial class Main : Node
{
	private T FindNodeSmart<T>(string unique, string a, string b) where T : class =>
		GetNodeOrNull<T>(unique) ?? GetNodeOrNull<T>(a) ?? GetNodeOrNull<T>(b);

	public override void _Ready()
	{
		GetTree().Paused = false;

		// เปลี่ยน HUD -> Hud ตรงนี้
		var hud = FindNodeSmart<Hud>("%HUD", "/root/Main/HUD", "/root/HUD");
		var sm  = FindNodeSmart<ScoreManager>("%ScoreManager", "/root/Main/ScoreManager", "/root/ScoreManager");

		if (hud == null) { GD.PushError("[Main] HUD not found"); return; }
		if (sm  == null) { GD.PushError("[Main] ScoreManager not found"); return; }

		hud.HideOverlay();
		hud.UpdateScore(sm.Score, sm.TargetScore);
		hud.UpdateLives(sm.Lives);
		hud.UpdateLevel(sm.Level);

		sm.ScoreChanged += hud.UpdateScore;
		sm.LivesChanged += hud.UpdateLives;
		sm.LevelChanged += hud.UpdateLevel;
		sm.GameOver     += hud.ShowGameOver;

		GD.Print("[Main] signals wired");
	}
}
