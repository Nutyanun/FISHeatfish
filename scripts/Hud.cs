using Godot;

public partial class Hud : CanvasLayer
{
	[Export] public Label ScoreLabel { get; set; }
	[Export] public Label LivesLabel { get; set; }
	[Export] public Label LevelLabel { get; set; }

	// เพิ่มแสดงผลใหม่
	[Export] public Label HighScoreLabel { get; set; }
	[Export] public Label MultiplierLabel { get; set; }
	[Export] public Label TimerLabel { get; set; }

	// Overlay ใช้ทั้ง Level Clear และ Game Over
	private Control _overlay;
	private Label _title;
	private Label _hint;
	private Button _retry;
	private Button _quit;

	private bool _isLevelClear = false;

	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.Always;

		// หา label อัตโนมัติถ้ายังไม่ได้ลากใน Inspector
		ScoreLabel      ??= GetNodeOrNull<Label>("%ScoreLabel")      ?? GetNodeOrNull<Label>("ScoreLabel");
		LivesLabel      ??= GetNodeOrNull<Label>("%LivesLabel")      ?? GetNodeOrNull<Label>("LivesLabel");
		LevelLabel      ??= GetNodeOrNull<Label>("%LevelLabel")      ?? GetNodeOrNull<Label>("LevelLabel");
		HighScoreLabel  ??= GetNodeOrNull<Label>("%HighScoreLabel")  ?? GetNodeOrNull<Label>("HighScoreLabel");
		MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel") ?? GetNodeOrNull<Label>("MultiplierLabel");
		TimerLabel      ??= GetNodeOrNull<Label>("%TimerLabel")      ?? GetNodeOrNull<Label>("TimerLabel");

		_overlay = GetNodeOrNull<Control>("%GameOverLabel") ?? GetNodeOrNull<Control>("GameOverLabel");
		if (_overlay != null)
		{
			// *** ในโปรเจกต์คุณสะกด "root" ***
			_title = _overlay.GetNodeOrNull<Label>("%Title") ?? _overlay.GetNodeOrNull<Label>("Center/root/Title");
			_hint  = _overlay.GetNodeOrNull<Label>("%Hint")  ?? _overlay.GetNodeOrNull<Label>("Center/root/Hint");
			_retry = _overlay.GetNodeOrNull<Button>("%Retry")?? _overlay.GetNodeOrNull<Button>("Center/root/Buttons/Retry");
			_quit  = _overlay.GetNodeOrNull<Button>("%Quit") ?? _overlay.GetNodeOrNull<Button>("Center/root/Buttons/Quit");

			_overlay.ProcessMode = Node.ProcessModeEnum.Always;
			_overlay.ZIndex = 1000;
			_overlay.Visible = false;
			_overlay.MoveToFront();
			_overlay.MouseFilter = Control.MouseFilterEnum.Ignore; // ซ่อน → ไม่บังคลิก

			if (_retry != null)
			{
				_retry.ProcessMode = Node.ProcessModeEnum.Always;
				_retry.MouseFilter = Control.MouseFilterEnum.Stop;
				_retry.Pressed += OnRetryPressed;
			}
			if (_quit != null)
			{
				_quit.ProcessMode = Node.ProcessModeEnum.Always;
				_quit.MouseFilter = Control.MouseFilterEnum.Stop;
				_quit.Pressed += OnQuitPressed;
			}
		}

		// ต่อสัญญาณกับ ScoreManager
		var sm = GetNodeOrNull<ScoreManager>("%ScoreManager") ?? GetNodeOrNull<ScoreManager>("ScoreManager");
		if (sm != null)
		{
			sm.ScoreChanged       += UpdateLevelScore;
			sm.TotalScoreChanged  += UpdateTotalScore;
			sm.LivesChanged       += UpdateLives;
			sm.LevelChanged       += UpdateLevel;
			sm.MultiplierChanged  += UpdateMultiplier;
			sm.TimeLeftChanged    += UpdateTimer;

			sm.LevelCleared       += OnLevelCleared;
			sm.GameOver           += ShowGameOver; // overload (int,int) ด้านล่าง
		}

		GetTree().Paused = false; // กันค้าง pause จากรอบก่อน
		HideOverlay();
	}

	// ===== Update methods =====
	public void UpdateLevelScore(int levelScore, int target)
	{
		if (ScoreLabel != null) ScoreLabel.Text = $"Score : {levelScore} / {target}";
	}

	public void UpdateTotalScore(int total, int hi)
	{
		if (HighScoreLabel != null) HighScoreLabel.Text = $"Total : {total}    High : {hi}";
	}

	public void UpdateLives(int lives)
	{
		if (LivesLabel != null) LivesLabel.Text = $"Lives : {lives}";
	}

	public void UpdateLevel(int level)
	{
		if (LevelLabel != null) LevelLabel.Text = $"Level : {level}";
	}

	public void UpdateMultiplier(int mult, int fishInWindow, int needFish, float windowLeft)
	{
		if (MultiplierLabel != null)
		{
			int sec = Mathf.CeilToInt(windowLeft);
			MultiplierLabel.Text = $"Mult x{mult} | {fishInWindow}/{needFish} | {sec}s";
		}
	}

	public void UpdateTimer(float timeLeft)
	{
		if (TimerLabel != null)
		{
			int t = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
			int mm = t / 60;
			int ss = t % 60;
			TimerLabel.Text = $"Time {mm:00}:{ss:00}";
		}
	}

	// alias เผื่อโค้ดเก่าเรียก
	public void UpdateScore(int cur, int target) => UpdateLevelScore(cur, target);

	// ===== Overlay control =====
	private void OnLevelCleared(int finalScore, int level) => ShowLevelClear(level, finalScore);

	public void ShowLevelClear(int level, int finalScore)
	{
		if (_overlay == null) return;
		_isLevelClear = true;

		if (_title != null) _title.Text = "LEVEL CLEAR!";
		if (_hint  != null) _hint.Text  = "Press Enter for Next";

		_overlay.Visible = true;
		_overlay.MouseFilter = Control.MouseFilterEnum.Stop; // โชว์ → กันคลิกพื้นหลัง
		_overlay.MoveToFront();
		GetTree().Paused = true;
	}

	// overload ให้ match delegate (int,int)
	public void ShowGameOver(int finalScore, int level) => ShowGameOver();

	public void ShowGameOver()
	{
		if (_overlay == null) return;
		_isLevelClear = false;

		if (_title != null) _title.Text = "GAME OVER";
		if (_hint  != null) _hint.Text  = "Press Enter to retry";

		_overlay.Visible = true;
		_overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		_overlay.MoveToFront();
		GetTree().Paused = true;
	}

	public void HideOverlay()
	{
		if (_overlay != null)
		{
			_overlay.Visible = false;
			_overlay.MouseFilter = Control.MouseFilterEnum.Ignore; // ซ่อน → ไม่บังคลิก
		}
	}

	// ===== Buttons / Input =====
	private async void OnRetryPressed()
	{
		GD.Print("[HUD] Retry pressed");
		GetTree().Paused = false;
		HideOverlay();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // หลบจังหวะ pause/signal
		GetTree().ReloadCurrentScene();
	}

	private async void OnQuitPressed()
	{
		GD.Print("[HUD] Quit pressed");
		GetTree().Paused = false;
		HideOverlay();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetTree().CallDeferred("quit");
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (_overlay == null || !_overlay.Visible) return;

		if (e.IsActionPressed("ui_accept") || e.IsActionPressed("restart"))
		{
			if (_isLevelClear) OnNextPressed();
			else OnRetryPressed();
		}
		if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("quit"))
		{
			OnQuitPressed();
		}
	}

	private void OnNextPressed()
	{
		var sm = GetNodeOrNull<ScoreManager>("%ScoreManager") ?? GetNodeOrNull<ScoreManager>("ScoreManager");
		if (sm != null)
		{
			int nextLevel  = sm.Level + 1;
			int nextTarget = sm.TargetScore + 25; // ปรับสูตรตามเกม
			int lives      = sm.Lives;

			sm.ResetForNewLevel(nextLevel, nextTarget, lives);
		}

		HideOverlay();
		GetTree().Paused = false;
	}
}
