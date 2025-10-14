using Godot;
using System;

public partial class HudCard : CanvasLayer
{
	// ===== ScoreManager =====
	[Export] public NodePath ScoreManagerPath { get; set; } = null;
	private ScoreManager _sm;

	// === Navigation options ===
	[Export] public bool RetryReloadsScene { get; set; } = true;
	// วางแทนบรรทัดเดิม
	[Export(PropertyHint.File, "*.tscn")] public string MenuScenePath { get; set; } = "";
	[Export(PropertyHint.File, "*.tscn")] public string NextScenePath { get; set; } = "";
	[Export] public bool QuitExitsGameIfNoMenu { get; set; } = true;

	// ===== Labels บนการ์ด =====
	[Export] public NodePath LevelLabelPath { get; set; } = "FreeLayer/LevelLabel";
	[Export] public NodePath ScoreLabelPath { get; set; } = "FreeLayer/ScoreLabel";
	[Export] public NodePath NameLabelPath  { get; set; } = "FreeLayer/NameLabel";
	[Export] public NodePath TimerLabelPath { get; set; } = "FreeLayer/TimerLabel";
	private Label _levelLabel, _scoreLabel, _nameLabel, _timerLabel;

	// ===== Fallback label (ถ้าไม่มี overlay container) =====
	[Export] public NodePath GameOverLabelPath { get; set; } = "GameOverLabel";
	private Label _gameOverLabel;

	// ===== Overlay container =====
	[Export] public NodePath OverlayPath   { get; set; } = "GameOverLabel";
	[Export] public NodePath TitlePath     { get; set; } = "Center/root/Title";
	[Export] public NodePath HintPath      { get; set; } = "Center/root/Hint";
	[Export] public NodePath RetryPath     { get; set; } = "Center/root/Buttons/Retry";
	[Export] public NodePath QuitPath      { get; set; } = "Center/root/Buttons/Quit";
	[Export] public NodePath NextPath      { get; set; } = "Center/root/Buttons/Next";
	[Export] public NodePath ScoreGroup7Path { get; set; } = "ScoreGroup7";

	private Control _overlay, _scoreGroup7;
	private Label _title, _hint;
	private Button _retry, _quit, _next;

	// ===== Mult (ส้ม) 1..5 =====
	[Export] public NodePath[] MultFilledPaths { get; set; } = {
		"FreeLayer/MULT/F1","FreeLayer/MULT/F2","FreeLayer/MULT/F3","FreeLayer/MULT/F4","FreeLayer/MULT/F5"
	};
	[Export] public NodePath[] MultEmptyPaths  { get; set; } = {
		"FreeLayer/MULT/E1","FreeLayer/MULT/E2","FreeLayer/MULT/E3","FreeLayer/MULT/E4","FreeLayer/MULT/E5"
	};
	private CanvasItem[] _multFilled = new CanvasItem[5];
	private CanvasItem[] _multEmpty  = new CanvasItem[5];

	// ===== Life (แดง) 0..3 =====
	[Export] public NodePath[] LifeFilledPaths { get; set; } = {
		"FreeLayer/Bgp/p/LF1","FreeLayer/Bgp/p/LF2","FreeLayer/Bgp/p/LF3"
	};
	[Export] public NodePath[] LifeEmptyPaths  { get; set; } = {
		"FreeLayer/Bgp/p/LE1","FreeLayer/Bgp/p/LE2","FreeLayer/Bgp/p/LE3"
	};
	private CanvasItem[] _lifeFilled = new CanvasItem[3];
	private CanvasItem[] _lifeEmpty  = new CanvasItem[3];

	// ===== Flash score =====
	[Export] public Color ScoreFlashColor { get; set; } = new Color(1f, 0.9f, 0.2f);
	[Export] public float ScoreFlashSeconds { get; set; } = 0.35f;
	private Color _scoreNormalColor = Colors.White;
	private bool _flashedThisLevel = false;

	public override void _Ready()
	{
		Layer = 100; // ให้อยู่บนสุด

		_sm = !ScoreManagerPath.IsEmpty ? GetNodeOrNull<ScoreManager>(ScoreManagerPath)
			 : GetNodeOrNull<ScoreManager>("%ScoreManager");
		if (_sm == null) { GD.PushError("[HudCard] ScoreManager not found"); return; }

		_levelLabel    = GetNodeOrNull<Label>(LevelLabelPath);
		_scoreLabel    = GetNodeOrNull<Label>(ScoreLabelPath);
		_nameLabel     = GetNodeOrNull<Label>(NameLabelPath);
		_timerLabel    = GetNodeOrNull<Label>(TimerLabelPath);
		_gameOverLabel = GetNodeOrNull<Label>(GameOverLabelPath);
		if (_gameOverLabel != null) _gameOverLabel.Visible = false;
		if (_scoreLabel != null) _scoreNormalColor = _scoreLabel.Modulate;

		for (int i = 0; i < 5; i++) {
			_multFilled[i] = GetNodeOrNull<CanvasItem>(i < MultFilledPaths.Length ? MultFilledPaths[i] : default);
			_multEmpty[i]  = GetNodeOrNull<CanvasItem>(i < MultEmptyPaths.Length  ? MultEmptyPaths[i]  : default);
		}
		for (int i = 0; i < 3; i++) {
			_lifeFilled[i] = GetNodeOrNull<CanvasItem>(i < LifeFilledPaths.Length ? LifeFilledPaths[i] : default);
			_lifeEmpty[i]  = GetNodeOrNull<CanvasItem>(i < LifeEmptyPaths.Length  ? LifeEmptyPaths[i]  : default);
		}

		// ===== Overlay wiring (หาทั่วทั้ง overlay กันพลาด) =====
		_overlay = GetNodeOrNull<Control>(OverlayPath) ?? GetNodeOrNull<Control>("%GameOverLabel");
		if (_overlay != null)
		{
			_title = _overlay.GetNodeOrNull<Label>(TitlePath)
					 ?? _overlay.GetNodeOrNull<Label>("%Title")
					 ?? _overlay.FindChild("Title", true, false) as Label;

			_hint  = _overlay.GetNodeOrNull<Label>(HintPath)
					 ?? _overlay.GetNodeOrNull<Label>("%Hint")
					 ?? _overlay.FindChild("Hint", true, false) as Label;

			_retry = _overlay.GetNodeOrNull<Button>(RetryPath)
					 ?? _overlay.GetNodeOrNull<Button>("%Retry")
					 ?? _overlay.FindChild("Retry", true, false) as Button;

			_quit  = _overlay.GetNodeOrNull<Button>(QuitPath)
					 ?? _overlay.GetNodeOrNull<Button>("%Quit")
					 ?? _overlay.FindChild("Quit", true, false) as Button;

			_next  = _overlay.GetNodeOrNull<Button>(NextPath)
					 ?? _overlay.GetNodeOrNull<Button>("%Next")
					 ?? _overlay.FindChild("Next", true, false) as Button;

			_scoreGroup7 = GetNodeOrNull<Control>(ScoreGroup7Path)
						   ?? GetNodeOrNull<Control>("%ScoreGroup7")
						   ?? _overlay.FindChild("ScoreGroup7", true, false) as Control;

			_overlay.ProcessMode = Node.ProcessModeEnum.Always;
			_overlay.ZIndex = 1000;
			_overlay.Visible = false;
			_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
			if (_scoreGroup7 != null) _scoreGroup7.Visible = false;

			if (_retry != null) { _retry.ProcessMode = Node.ProcessModeEnum.Always; _retry.MouseFilter = Control.MouseFilterEnum.Stop; _retry.Pressed += OnRetryPressed; }
			if (_quit  != null) { _quit.ProcessMode  = Node.ProcessModeEnum.Always; _quit.MouseFilter  = Control.MouseFilterEnum.Stop;  _quit.Pressed  += OnQuitPressed;  }
			if (_next  != null) { _next.ProcessMode  = Node.ProcessModeEnum.Always; _next.MouseFilter  = Control.MouseFilterEnum.Stop;  _next.Pressed  += OnNextPressed;  }

			GD.Print($"[HUD] overlay wired: retry={_retry!=null}, next={_next!=null}, quit={_quit!=null}");
		}

		// signals
		_sm.ScoreChanged += OnScoreChanged;
		_sm.TotalScoreChanged += OnTotalScoreChanged;
		_sm.LivesChanged += OnLivesChanged;
		_sm.LevelChanged += OnLevelChanged;
		_sm.MultiplierChanged += OnMultiplierChanged;
		_sm.TimeLeftChanged += OnTimeLeftChanged;
		_sm.LevelCleared += OnLevelCleared;
		_sm.GameOver += OnGameOver;

		_sm.SyncRequestFromHud();
		
		// === Show player name from PlayerLogin or GameProgress ===
string playerName = "Guest";

// ถ้ามี PlayerLogin.Instance ใช้งานอยู่
if (PlayerLogin.Instance != null)
	playerName = PlayerLogin.Instance.CurrentPlayerName;

// ถ้า GameProgress มีข้อมูลชื่อ
else if (!string.IsNullOrEmpty(PlayerLogin.Instance.CurrentPlayerName))
	playerName = PlayerLogin.Instance.CurrentPlayerName;

// อัปเดตชื่อใน Label
if (_nameLabel != null)
	_nameLabel.Text = playerName;
else
	GD.PushWarning("[HUD] NameLabel not found, cannot show player name.");
	}

	public override void _UnhandledInput(InputEvent e)
{
	// ถ้า overlay ยังไม่ขึ้น → ไม่รับ input
	if (_overlay == null || !_overlay.Visible)
		return;

	// ป้องกัน pause ซ้ำจากระบบอื่น (PauseUI)
	GetViewport().SetInputAsHandled();  // บอกว่า input นี้ถูกจัดการแล้ว (จะไม่ส่งต่อให้ node อื่น)

	// ตรวจปุ่ม Enter
	if (e.IsActionPressed("ui_accept"))
	{
		if (_next != null && _next.Visible && !_next.Disabled)
		{
			OnNextPressed();
		}
		else if (_retry != null && _retry.Visible && !_retry.Disabled)
		{
			OnRetryPressed();
		}
	}

	// ตรวจปุ่มออก
	if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("quit"))
	{
		OnQuitPressed();
	}
}

	// ===== Handlers =====
	private void OnLevelChanged(int level)
	{
		_flashedThisLevel = false;
		if (_levelLabel != null) _levelLabel.Text = $"LV.{level}";
		HideOverlay();
		if (_gameOverLabel != null) _gameOverLabel.Visible = false;
	}

	private void OnScoreChanged(int levelScore, int target)
	{
		if (!_flashedThisLevel && target > 0 && levelScore >= target) {
			_flashedThisLevel = true;
			FlashScoreOnce();
		}
	}

	private void OnTotalScoreChanged(int totalScore, int highScore)
	{
		if (_scoreLabel != null) _scoreLabel.Text = totalScore.ToString();
	}

	private void OnLivesChanged(int lives)
	{
		int f = Mathf.Clamp(lives, 0, 3);
		for (int i = 0; i < 3; i++) {
			if (_lifeEmpty[i]  != null) _lifeEmpty[i].Visible  = true;
			if (_lifeFilled[i] != null) _lifeFilled[i].Visible = (i < f);
			if (_lifeFilled[i] != null && _lifeEmpty[i] != null) _lifeFilled[i].ZIndex = _lifeEmpty[i].ZIndex + 1;
		}
	}

	private void OnMultiplierChanged(int mult, int fishInWindow, int needFish, float windowLeft)
	{
		int f = Mathf.Clamp(mult, 0, 5);
		for (int i = 0; i < 5; i++) {
			if (_multEmpty[i]  != null) _multEmpty[i].Visible  = true;
			if (_multFilled[i] != null) _multFilled[i].Visible = (i < f);
			if (_multFilled[i] != null && _multEmpty[i] != null) _multFilled[i].ZIndex = _multEmpty[i].ZIndex + 1;
		}
	}

	private void OnTimeLeftChanged(float timeLeft)
	{
		if (_timerLabel == null) return;
		int t = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
		_timerLabel.Text = $"Time : {t/60:00}:{t%60:00}";
	}

	private void OnGameOver(int finalScore, int level)
	{
		GD.Print("[HUD] OnGameOver received");
		if (_overlay != null)
			ShowOverlay(OverlayMode.GameOver, "GAME OVER", "Press ENTER or click Retry");
		else
			ShowCenterBigLabel("GAME OVER");
	}

	private void OnLevelCleared(int finalScore, int level)
	{
		GD.Print("[HUD] OnLevelCleared received");
		if (_overlay != null)
			ShowOverlay(OverlayMode.LevelClear, $"LEVEL {level} CLEAR!", "Press ENTER or click Next");
		else
			ShowCenterBigLabel($"LEVEL {level} CLEAR!");
	}

	// ===== Overlay helpers =====
	private enum OverlayMode { GameOver, LevelClear }

	private void ShowOverlay(OverlayMode mode, string title, string hint)
	{
		if (_overlay == null) return;

		if (_title != null) _title.Text = title;
		if (_hint  != null) _hint.Text  = hint;

		// โชว์/ซ่อนและ disable ปุ่มให้ถูกโหมด
		if (_retry != null) { _retry.Visible = (mode == OverlayMode.GameOver); _retry.Disabled = !_retry.Visible; }
		if (_next  != null) { _next.Visible  = (mode == OverlayMode.LevelClear); _next.Disabled  = !_next.Visible; }
		if (_quit  != null) { _quit.Visible  = true; _quit.Disabled = false; }

		_overlay.Visible = true;
		_overlay.ZIndex = 1000;
		_overlay.ProcessMode = Node.ProcessModeEnum.Always;
		_overlay.MouseFilter = Control.MouseFilterEnum.Stop;

		if (_scoreGroup7 != null) _scoreGroup7.Visible = true;

		// โฟกัสปุ่มแรกที่มองเห็น
		if (_next != null && _next.Visible) _next.GrabFocus();
		else if (_retry != null && _retry.Visible) _retry.GrabFocus();
		else if (_quit != null && _quit.Visible) _quit.GrabFocus();
	}

	private void HideOverlay()
	{
		if (_overlay == null) return;
		_overlay.Visible = false;
		_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
		if (_scoreGroup7 != null) _scoreGroup7.Visible = false;
	}

	private async void ShowCenterBigLabel(string text)
	{
		if (_gameOverLabel == null) return;

		_gameOverLabel.Visible = true;
		_gameOverLabel.Text = text;
		_gameOverLabel.ZIndex = 9999;
		_gameOverLabel.Modulate = new Color(1,1,1,0);

		await ToSignal(GetTree(), "process_frame");
		var vp = GetViewport().GetVisibleRect().Size;
		var sz = _gameOverLabel.GetRect().Size;
		_gameOverLabel.Position = (vp - sz) * 0.5f;

		var tween = CreateTween();
		tween.TweenProperty(_gameOverLabel, "modulate:a", 1.0, 0.35);
		GD.Print($"[HUD] show label '{text}' at {_gameOverLabel.Position}, size={sz}");
	}

	// ===== Buttons =====
	private async void OnRetryPressed()
{
	GD.Print("[HUD] Retry pressed");

	// ✅ ปลด pause ก่อน reload (สำคัญมาก!)
	GetTree().Paused = false;

	// ✅ ถ้ามี overlay ก็ซ่อน
	HideOverlay();

	// ✅ รอให้ process frame นึง เพื่อให้ tree resume แล้วค่อย reload
	await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

	GetTree().ReloadCurrentScene();
}


	private async void OnNextPressed()
{
	GD.Print("[HUD] Next pressed");

	GetTree().Paused = false;
	HideOverlay();
	await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	GetTree().ChangeSceneToFile("res://scenescore/score.tscn");
}


	private void OnQuitPressed()
{
	GD.Print("[HUD] Quit pressed");

	if (!string.IsNullOrEmpty(MenuScenePath))
	{
		// ถ้าตั้งหน้าเมนูไว้ ให้กลับไปเมนูแทนการออก
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile(MenuScenePath);
		return;
	}

	if (QuitExitsGameIfNoMenu)
	{
		QuitApp();   // ออกจากเกมแบบกันพลาด/ข้ามแพลตฟอร์ม
	}
	else
	{
		HideOverlay(); // ไม่ออกก็ซ่อนโอเวอร์เลย์
	}
}

// --- helper: ออกจากเกมแบบครอบคลุม ---
private void QuitApp()
{
	// บางแพลตฟอร์ม/บางจังหวะสั่ง Quit ตรง ๆ อาจไม่ทำงาน ให้ลองทั้งทันทีและแบบ deferred
	GetTree().Paused = false;

	// เคสเว็บ (HTML5) ส่วนใหญ่ "ออก" จะทำอะไรไม่ได้ ให้ลองกลับหน้าเมนูถ้ามี
	if (OS.HasFeature("web"))
	{
		GD.Print("[HUD] Quit on web: fallback to hide or go menu if set.");
		if (!string.IsNullOrEmpty(MenuScenePath))
		{
			GetTree().ChangeSceneToFile(MenuScenePath);
		}
		else
		{
			// บนเว็บไม่มีการปิดหน้าต่างจากเกมได้ ปิดโอเวอร์เลย์แทน
			HideOverlay();
		}
		return;
	}

	// ลองแบบปกติ
	GetTree().Quit();

	// เผื่อสั่งในสัญญาณปุ่มแล้วไม่ปิด ให้ deferred อีกครั้ง
	Callable.From(() => GetTree().Quit()).CallDeferred();

	// เผื่อกรณีขี้เกียจจริง ๆ: ส่ง notification ปิดหน้าต่าง (desktop)
	if (!OS.HasFeature("web"))
	{
		GetTree().Root.PropagateNotification((int)Node.NotificationWMCloseRequest);
	}
}


	private void OnResetHighPressed()
	{
		GD.Print("[HUD] ResetHigh pressed (implement if needed)");
	}

	// ===== Flash score =====
	private async void FlashScoreOnce()
	{
		if (_scoreLabel == null) return;
		_scoreLabel.Modulate = ScoreFlashColor;
		await ToSignal(GetTree().CreateTimer(ScoreFlashSeconds), "timeout");
		_scoreLabel.Modulate = _scoreNormalColor;
	}
}
