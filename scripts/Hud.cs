using Godot;

public partial class Hud : CanvasLayer
{
	// ===== Export & Labels =====
	[Export] public int LevelNumber = 1;

	[Export] public Label ScoreLabel { get; set; }
	[Export] public Label LivesLabel { get; set; }
	[Export] public Label LevelLabel { get; set; }

	[Export] public Label HighScoreLabel { get; set; }
	[Export] public Label MultiplierLabel { get; set; }
	[Export] public Label TimerLabel { get; set; }

	// แสดงคะแนนโบนัสแยก
	[Export] public Label BonusLabel { get; set; }

	// ป้าย “BONUS TIME!”
	private Label _bonusBanner;

	// ===== Overlay (GameOver / LevelClear) =====
	private Control _overlay;
	private Label _title;
	private Label _hint;
	private Button _retry;
	private Button _quit;
	private Button _next;
	private bool _isLevelClear = false;

	// ===== Timer fallback =====
	[Export] public bool  DriveTimerHere   = false;
	[Export] public float StartTimeSeconds = 180f;
	private float _fallbackTimeLeft;

	// ===== Internal state =====
	private bool _targetAnnounced = false;

	public override void _Ready()
	{
		// HUD/Overlay ให้ทำงานแม้ตอน pause
		ProcessMode = Node.ProcessModeEnum.Always;

		// ----- Auto-wire labels -----
		ScoreLabel      ??= GetNodeOrNull<Label>("%ScoreLabel")      ?? GetNodeOrNull<Label>("ScoreLabel");
		LivesLabel      ??= GetNodeOrNull<Label>("%LivesLabel")      ?? GetNodeOrNull<Label>("LivesLabel");
		LevelLabel      ??= GetNodeOrNull<Label>("%LevelLabel")      ?? GetNodeOrNull<Label>("LevelLabel");
		HighScoreLabel  ??= GetNodeOrNull<Label>("%HighScoreLabel")  ?? GetNodeOrNull<Label>("HighScoreLabel");
		MultiplierLabel ??= GetNodeOrNull<Label>("%MultiplierLabel") ?? GetNodeOrNull<Label>("MultiplierLabel");
		TimerLabel      ??= GetNodeOrNull<Label>("%TimerLabel")      ?? GetNodeOrNull<Label>("TimerLabel");
		BonusLabel      ??= GetNodeOrNull<Label>("%BonusLabel")      ?? GetNodeOrNull<Label>("BonusLabel");

		_bonusBanner    ??= GetNodeOrNull<Label>("%BonusBanner")     ?? GetNodeOrNull<Label>("BonusBanner");

		if (TimerLabel == null) GD.PushWarning("[HUD] TimerLabel not found. Check unique name or node path.");
		else TimerLabel.Text = "Time : 00:00";

		// ตั้งค่าเริ่มต้นของ BonusLabel/BonusBanner
		if (BonusLabel != null)
		{
			BonusLabel.Visible = false;
			BonusLabel.Text = "Bonus : 0";
			BonusLabel.Scale = Vector2.One;
			BonusLabel.Modulate = Colors.White;
		}
		if (_bonusBanner != null)
		{
			_bonusBanner.Visible = false;
			_bonusBanner.Modulate = new Color(1, 1, 1, 0); // โปร่งใสไว้ก่อน
			_bonusBanner.Scale = Vector2.One;
		}

		// ----- Overlay wiring -----
		_overlay = GetNodeOrNull<Control>("%GameOverLabel") ?? GetNodeOrNull<Control>("GameOverLabel");
		if (_overlay != null)
		{
			_title = _overlay.GetNodeOrNull<Label>("%Title") ?? _overlay.GetNodeOrNull<Label>("Center/root/Title");
			_hint  = _overlay.GetNodeOrNull<Label>("%Hint")  ?? _overlay.GetNodeOrNull<Label>("Center/root/Hint");
			_retry = _overlay.GetNodeOrNull<Button>("%Retry")?? _overlay.GetNodeOrNull<Button>("Center/root/Buttons/Retry");
			_quit  = _overlay.GetNodeOrNull<Button>("%Quit") ?? _overlay.GetNodeOrNull<Button>("Center/root/Buttons/Quit");
			_next  = _overlay.GetNodeOrNull<Button>("%Next") ?? _overlay.GetNodeOrNull<Button>("Center/root/Buttons/Next");

			_overlay.ProcessMode = Node.ProcessModeEnum.Always;
			_overlay.ZIndex = 1000;
			_overlay.Visible = false;
			_overlay.MoveToFront();
			_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;

			if (_retry != null) { _retry.ProcessMode = Node.ProcessModeEnum.Always; _retry.MouseFilter = Control.MouseFilterEnum.Stop; _retry.Pressed += OnRetryPressed; }
			if (_quit  != null) { _quit.ProcessMode  = Node.ProcessModeEnum.Always; _quit.MouseFilter  = Control.MouseFilterEnum.Stop;  _quit.Pressed  += OnQuitPressed;  }
			if (_next  != null) { _next.ProcessMode  = Node.ProcessModeEnum.Always; _next.MouseFilter  = Control.MouseFilterEnum.Stop;  _next.Pressed  += OnNextPressed;  }
		}

		// ----- Connect ScoreManager signals -----
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
			sm.GameOver           += ShowGameOver;

			// โบนัส (ชื่อเมธอด/พารามิเตอร์ต้องตรงกับ ScoreManager ของคุณ)
			sm.BonusScoreChanged  += OnBonusScoreChanged;  // int totalBonus
			sm.BonusPhaseStarted  += OnBonusPhaseStarted;  // void
			sm.BonusPhaseEnded    += OnBonusPhaseEnded;    // int totalBonus

			GD.Print("[HUD] Connected to ScoreManager (incl. Bonus signals).");
		}
		else
		{
			GD.PushWarning("[HUD] ScoreManager not found; using HUD fallback timer.");
		}

		// กันค้าง pause จากรอบก่อน
		GetTree().Paused = false;
		HideOverlay();

		// fallback timer
		_fallbackTimeLeft = StartTimeSeconds;
		UpdateTimer(_fallbackTimeLeft);
		_targetAnnounced = false;
	}

	public override void _Process(double delta)
	{
		// เดินเวลาที่ HUD เอง (เฉพาะ DriveTimerHere = true)
		if (DriveTimerHere && !GetTree().Paused && (_overlay == null || !_overlay.Visible))
		{
			_fallbackTimeLeft = Mathf.Max(0, _fallbackTimeLeft - (float)delta);
			UpdateTimer(_fallbackTimeLeft);
		}
	}

	// ===== Update methods =====
	public void UpdateLevelScore(int levelScore, int target)
	{
		if (ScoreLabel != null) ScoreLabel.Text = $"Score : {levelScore} / {target}";

		// ถึงเป้า (ครั้งแรก) → เอฟเฟกต์เล็ก ๆ ที่สกอร์
		if (!_targetAnnounced && levelScore >= target)
		{
			_targetAnnounced = true;
			FlashScoreLabel();
		}
		if (_targetAnnounced && levelScore < target)
			_targetAnnounced = false;
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
		_targetAnnounced = false;

		// รีเซ็ตโบนัสเมื่อขึ้นด่านใหม่
		if (BonusLabel != null)
		{
			BonusLabel.Text = "Bonus : 0";
			BonusLabel.Visible = false;
			BonusLabel.Scale = Vector2.One;
			BonusLabel.Modulate = Colors.White;
		}
		if (_bonusBanner != null)
		{
			_bonusBanner.Visible = false;
			_bonusBanner.Modulate = new Color(1, 1, 1, 0);
			_bonusBanner.Scale = Vector2.One;
		}
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
		if (TimerLabel == null) return;
		int t  = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
		int mm = t / 60;
		int ss = t % 60;
		TimerLabel.Text = $"Time : {mm:00}:{ss:00}";
	}

	// alias
	public void UpdateScore(int cur, int target) => UpdateLevelScore(cur, target);

	// ===== Bonus live update =====
	private void OnBonusScoreChanged(int totalBonus)
	{
		if (BonusLabel == null) return;
		BonusLabel.Visible = true;
		BonusLabel.Text = $"Bonus : {totalBonus}";
		PulseBonusLabel();
	}

	private void OnBonusPhaseStarted()
	{
		if (_bonusBanner == null) return;

		_bonusBanner.Visible = true;
		_bonusBanner.Modulate = new Color(1, 1, 1, 0);
		_bonusBanner.Scale = new Vector2(0.85f, 0.85f);

		var tw = CreateTween();
		if (tw == null) return;

		tw.SetParallel();
		tw.TweenProperty(_bonusBanner, "modulate:a", 1f, 0.20);                   // fade in
		tw.TweenProperty(_bonusBanner, "scale", new Vector2(1.15f, 1.15f), 0.15)  // pop
		  .From(_bonusBanner.Scale);
		tw.Chain().TweenProperty(_bonusBanner, "scale", Vector2.One, 0.10);
	}

	private void OnBonusPhaseEnded(int totalBonus)
	{
		// จางหายและซ่อนแบนเนอร์
		if (_bonusBanner != null)
		{
			var tw = CreateTween();
			if (tw == null) { _bonusBanner.Visible = false; }
			else
			{
				tw.TweenProperty(_bonusBanner, "modulate:a", 0f, 0.25);
				tw.TweenCallback(Callable.From(() =>
				{
					_bonusBanner.Visible = false;
					_bonusBanner.Modulate = new Color(1, 1, 1, 0);
					_bonusBanner.Scale = Vector2.One;
				}));
			}
		}
	}

	private void PulseBonusLabel()
	{
		if (BonusLabel == null) return;

		var tw = CreateTween();
		if (tw == null) return;

		tw.SetParallel();
		tw.TweenProperty(BonusLabel, "scale", new Vector2(1.15f, 1.15f), 0.10).From(Vector2.One);
		tw.TweenProperty(BonusLabel, "modulate", new Color(1f, 1f, 0.7f), 0.10).From(Colors.White);
		tw.Chain().TweenProperty(BonusLabel, "scale", Vector2.One, 0.10);
		tw.Chain().TweenProperty(BonusLabel, "modulate", Colors.White, 0.10);
	}

	// ===== Level clear / game over flow =====
	private void OnLevelCleared(int finalScore, int level)
	{
		GD.Print($"[HUD] Level {level} cleared (score={finalScore}).");
		HideOverlay();
		GetTree().Paused = false;
	}

	public void ShowLevelClear(int level, int finalScore)
	{
		if (_overlay == null) return;
		_isLevelClear = true;

		if (_title != null) _title.Text = "LEVEL CLEAR!";
		if (_hint  != null)  _hint.Text = "Press Enter to Next";

		if (_quit != null) _quit.Visible = false;
		if (_next != null)  _next.Visible = true;

		_overlay.Visible = true;
		_overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		_overlay.MoveToFront();

		GetTree().Paused = true;
	}

	public void ShowGameOver(int finalScore, int level) => ShowGameOver();

	public void ShowGameOver()
	{
		if (_overlay == null) return;
		_isLevelClear = false;

		if (_title != null) _title.Text = "GAME OVER";
		if (_hint  != null)  _hint.Text = "Press Enter to Retry";

		if (_next != null)  _next.Visible = false;
		if (_quit != null)  _quit.Visible = true;

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
			_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
	}

	// ===== Buttons / Input =====
	private async void OnRetryPressed()
	{
		GD.Print("[HUD] Retry pressed");
		GetTree().Paused = false;
		HideOverlay();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetTree().ReloadCurrentScene();
	}

	private async void OnQuitPressed()
	{
		GD.Print("[HUD] Quit pressed");
		GetTree().Paused = false;
		HideOverlay();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
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

	private async void OnNextPressed()
	{
		GD.Print("[HUD] Next pressed");

		var sm = GetNodeOrNull<ScoreManager>("%ScoreManager") ?? GetNodeOrNull<ScoreManager>("ScoreManager");
		if (sm != null)
		{
			GameProgress.CurrentPlayingLevel = sm.Level;
			GameProgress.LastLevelScore = sm.Score;
		}

		GetTree().Paused = false;
		HideOverlay();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		GetTree().ChangeSceneToFile("res://scenescore/score.tscn");
	}

	// ===== Small visual effect when reaching target =====
	private void FlashScoreLabel()
	{
		if (ScoreLabel == null) return;

		var tween = CreateTween();
		if (tween == null) return;

		var fromCol = new Color(1f, 1f, 0f, 1f); // เหลือง
		var toCol   = new Color(1f, 1f, 1f, 1f); // ขาว

		tween.SetParallel();
		tween.TweenProperty(ScoreLabel, "modulate", fromCol, 0.0);
		tween.TweenProperty(ScoreLabel, "scale", new Vector2(1.15f, 1.15f), 0.15f).From(Vector2.One);
		tween.Chain().TweenProperty(ScoreLabel, "modulate", toCol, 0.6f);
		tween.Chain().TweenProperty(ScoreLabel, "scale", Vector2.One, 0.12f);
	}
}
