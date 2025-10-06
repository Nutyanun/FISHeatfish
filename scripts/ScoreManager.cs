using Godot;

public partial class ScoreManager : Node2D
{
	// ===== Signals =====
	[Signal] public delegate void ScoreChangedEventHandler(int levelScore, int target);
	[Signal] public delegate void TotalScoreChangedEventHandler(int totalScore, int highScore);
	[Signal] public delegate void LivesChangedEventHandler(int lives);
	[Signal] public delegate void LevelChangedEventHandler(int level);
	[Signal] public delegate void MultiplierChangedEventHandler(int mult, int fishInWindow, int needFish, float windowLeft);
	[Signal] public delegate void TimeLeftChangedEventHandler(float timeLeft);
	[Signal] public delegate void LevelClearedEventHandler(int finalScore, int level);
	[Signal] public delegate void GameOverEventHandler(int finalScore, int level);

	// ===== Gameplay basic =====
	[Export] public int TargetScore { get; set; } = 500;  // ใช้ผ่านด่าน
	[Export] public int Lives { get; set; } = 3;
	[Export] public int Level { get; set; } = 1;

	// แยกสกอร์
	public int LevelScore { get; private set; } = 0;     // ไว้ตัดผ่านด่าน
	public int TotalScore { get; private set; } = 0;     // รวมทั้งหมดในรอบนี้
	public int HighScore  { get; private set; } = 0;     // สถิติสะสม

	// === ALIAS เพื่อเข้ากับโค้ดเก่า ===
	public int Score => LevelScore;

	public bool IsGameOver { get; private set; } = false;
	public bool IsLevelCleared { get; private set; } = false;

	// ===== Multiplier system =====
	[Export] public int   FishPerStep   { get; set; } = 10;   // ครบเท่านี้ในหน้าต่าง → Mult +1 (ไม่ครบ → -1)
	[Export] public float WindowSeconds { get; set; } = 20f;  // ความยาวหน้าต่าง
	[Export] public int   MultMin       { get; set; } = 1;
	[Export] public int   MultMax       { get; set; } = 5;

	public int  Mult { get; private set; } = 1;
	private int  _fishInWindow = 0;
	private float _windowLeft;

	// ===== Game timer (3 นาที) =====
	[Export] public float GameTimeSeconds { get; set; } = 180f;
	private float _timeLeft;

	private const string SAVE_PATH = "user://save_highscore.dat";

	public override void _Ready()
	{
		LoadHighScore();

		LevelScore = 0;
		TotalScore = 0;
		Mult = MultMin;
		_fishInWindow = 0;
		_windowLeft = WindowSeconds;

		_timeLeft = GameTimeSeconds;
		IsGameOver = false;
		IsLevelCleared = false;

		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.LevelChanged, Level);
		EmitSignal(SignalName.MultiplierChanged, Mult, _fishInWindow, FishPerStep, _windowLeft);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);
	}

	public override void _Process(double delta)
	{
		if (IsGameOver || IsLevelCleared) return;

		// หน้าต่างตัวคูณ
		_windowLeft -= (float)delta;
		if (_windowLeft <= 0f)
		{
			if (_fishInWindow >= FishPerStep)
				Mult = Mathf.Clamp(Mult + 1, MultMin, MultMax);
			else
				Mult = Mathf.Clamp(Mult - 1, MultMin, MultMax);

			_fishInWindow = 0;
			_windowLeft = WindowSeconds;
			EmitSignal(SignalName.MultiplierChanged, Mult, _fishInWindow, FishPerStep, _windowLeft);
		}

		// เวลารวมทั้งเกม
		_timeLeft -= (float)delta;
		EmitSignal(SignalName.TimeLeftChanged, Mathf.Max(_timeLeft, 0f));
		if (_timeLeft <= 0f)
			DoGameOver();
	}

	public void AddScore(int basePoints)
	{
	if (IsGameOver || IsLevelCleared) return;

	_fishInWindow++;
	int gained = basePoints * Mult;

	LevelScore += gained;
	TotalScore += gained;

	if (TotalScore > HighScore)
	{
		HighScore = TotalScore;
		SaveHighScore();
	}

	EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
	EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
	EmitSignal(SignalName.MultiplierChanged, Mult, _fishInWindow, FishPerStep, _windowLeft);

	// ✅ เปลี่ยนส่วนนี้
	if (LevelScore >= TargetScore)
	{
		// แค่แจ้งว่าแต้มถึงเป้า แต่ยังไม่จบ
		GD.Print($"[ScoreManager] Target reached ({LevelScore}/{TargetScore}) — continue until time runs out!");
		// สามารถใส่เอฟเฟกต์หรือเสียงเฉลิมฉลองได้ เช่น:
		// GetNode<AudioStreamPlayer>("SfxTarget").Play();
	}

	// ❌ ไม่ emit LevelCleared และไม่ pause เกมที่นี่
	}


	public void LoseLife(int amount = 1)
	{
		if (IsGameOver || IsLevelCleared) return;

		Lives -= amount;
		if (Lives < 0) Lives = 0;
		EmitSignal(SignalName.LivesChanged, Lives);

		if (Lives == 0)
			DoGameOver();
	}

	private void DoGameOver()
	{
	if (IsGameOver) return;
	IsGameOver = true;
	SaveHighScore();

	// ถ้าผู้เล่นได้แต้มถึงเป้า → ถือว่าผ่านด่าน
	if (LevelScore >= TargetScore)
	{
		GameProgress.Advance();   // ปลดล็อกด่านต่อไป
		GD.Print("[ScoreManager] Level complete! Unlocked next checkpoint.");
	}

	EmitSignal(SignalName.GameOver, LevelScore, Level);
	GetTree().Paused = true;
	}


	public void ResetForNewLevel(int newLevel, int newTarget, int lives)
	{
		Level = newLevel;
		TargetScore = newTarget;
		Lives = lives;

		LevelScore = 0;
		IsLevelCleared = false;
		GetTree().Paused = false;

		Mult = MultMin;
		_fishInWindow = 0;
		_windowLeft = WindowSeconds;

		// ถ้าอยากรีเซ็ตเวลาเกมทั้งรอบเมื่อขึ้นด่านใหม่ ให้ปลดคอมเมนต์:
		// _timeLeft = GameTimeSeconds;

		EmitSignal(SignalName.LevelChanged, Level);
		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.MultiplierChanged, Mult, _fishInWindow, FishPerStep, _windowLeft);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);
	}

	private void SaveHighScore()
	{
		using var f = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Write);
		f.Store32((uint)HighScore);
	}
	private void LoadHighScore()
	{
		if (!FileAccess.FileExists(SAVE_PATH)) { HighScore = 0; return; }
		using var f = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Read);
		HighScore = (int)f.Get32();
	}
}
