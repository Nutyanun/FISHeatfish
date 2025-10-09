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

	// สำหรับ HUD โบนัส
	[Signal] public delegate void BonusScoreChangedEventHandler(int totalBonus);
	[Signal] public delegate void BonusPhaseStartedEventHandler();
	[Signal] public delegate void BonusPhaseEndedEventHandler(int totalBonus);

	// ===== Config ด่าน =====
	[Export] public int  BaseTargetScore { get; set; } = 300;
	[Export] public int  BaseTimeSeconds { get; set; } = 150;
	[Export] public int  TimeIncPerLevel { get; set; } = 90;
	[Export] public bool AutoAdvanceOnTimeUp { get; set; } = false;

	// ===== ค่าเริ่มต้นชีวิตต่อเลเวล =====
	[Export] public int StartingLives { get; set; } = 3;

	// ===== โปรยเหรียญช่วงท้ายเกม =====
	[Export] public NodePath BonusSpawnerPath { get; set; } = "../BonusCoinSpawner";
	[Export] public float BonusTriggerSeconds { get; set; } = 20f; // เริ่มโปรยเมื่อเวลาเหลือ <= ค่านี้
	[Export] public int   BonusStartLevel    { get; set; } = 2;   // ใช้ระบบเหรียญตั้งแต่เลเวลนี้ขึ้นไป

	private BonusCoinSpawner _bonus;
	private bool _bonusStartedThisLevel = false; // กันสตาร์ตซ้ำ
	private bool _bonusRunning = false;          // กำลังโปรยเหรียญอยู่หรือไม่
	private int  _bonusScore = 0;                // คะแนนโบนัสของเลเวลนี้

	// ===== สถานะหลัก =====
	[Export] public int Level { get; set; } = 1;
	[Export] public int Lives { get; set; } = 3;

	public int TargetScore { get; private set; } = 300;
	public int LevelScore  { get; private set; } = 0;  // คะแนนจากปลาในเลเวลนี้
	public int TotalScore  { get; private set; } = 0;  // คะแนนรวม (ปลา + โบนัส)
	public int HighScore   { get; private set; } = 0;

	public int Score => LevelScore;

	public bool IsGameOver     { get; private set; } = false;
	public bool IsLevelCleared { get; private set; } = false;

	// ===== Multiplier =====
	[Export] public int   FishPerStep   { get; set; } = 10;
	[Export] public float WindowSeconds { get; set; } = 20f;
	[Export] public int   MultMin       { get; set; } = 1;
	[Export] public int   MultMax       { get; set; } = 5;

	public int   Mult { get; private set; } = 1;
	private int   _fishInWindow = 0;
	private float _windowLeft;

	// ===== เวลา (ต่อด่าน) =====
	private float _timeLeft;

	private const string SAVE_PATH = "user://save_highscore.dat";

	// ---------- Helpers ----------
	private int TargetForLevel(int lvl) => BaseTargetScore << (lvl - 1); // x2 ทุกด่าน
	private int TimeForLevel(int lvl)   => BaseTimeSeconds + TimeIncPerLevel * (lvl - 1);

	// ---------- Lifecycle ----------
	public override void _Ready()
	{
		LoadHighScore();

		if (GameProgress.CurrentPlayingLevel > 0)
			Level = GameProgress.CurrentPlayingLevel;

		if (!BonusSpawnerPath.IsEmpty)
		{
			_bonus = GetNodeOrNull<BonusCoinSpawner>(BonusSpawnerPath);
			if (_bonus != null)
			{
				_bonus.BonusStarted += OnBonusStarted;
				_bonus.BonusEnded   += OnBonusEnded;
				_bonus.BonusTick    += OnBonusTick;
			}
		}

		StartLevel(Level);
	}

	private void StartLevel(int lvl)
	{
		Level = Mathf.Max(1, lvl);

		Lives = StartingLives;
		TargetScore     = TargetForLevel(Level);
		LevelScore      = 0;
		IsLevelCleared  = false;
		IsGameOver      = false;

		_bonusScore = 0;
		_bonusStartedThisLevel = false;
		_bonusRunning = false;

		Mult           = Mathf.Clamp(MultMin, MultMin, MultMax);
		_fishInWindow  = 0;
		_windowLeft    = WindowSeconds;

		_timeLeft = TimeForLevel(Level);

		EmitSignal(SignalName.LevelChanged, Level);
		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.MultiplierChanged, Mult, _fishInWindow, FishPerStep, _windowLeft);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

		GD.Print($"[ScoreManager] Start Level {Level}: target={TargetScore}, time={_timeLeft}s, lives={Lives}");
	}

	public override void _Process(double delta)
	{
		if (IsGameOver) return;

		// Multiplier window
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

		// Time countdown
		_timeLeft = Mathf.Max(0, _timeLeft - (float)delta);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

		// เริ่มโปรยเมื่อเข้า 20 วิสุดท้าย และเฉพาะเลเวลที่กำหนดขึ้นไป
		if (!_bonusStartedThisLevel && Level >= BonusStartLevel && _timeLeft <= BonusTriggerSeconds)
			StartBonusRainForRemainingTime();

		// หมดเวลา → ถ้ากำลังโปรยอยู่ให้ "รอ" จนกว่าจะจบก่อนค่อยสรุปผล
		if (_timeLeft <= 0f && !IsLevelCleared)
		{
			if (_bonusRunning) return;

			if (LevelScore >= TargetScore)
				FinishCurrentLevel();
			else
				FailByTimeUp();
		}
	}

	// ---------- Gameplay API ----------
	public void AddScore(int basePoints)
	{
		if (IsGameOver) return;

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
	}

	public void LoseLife(int amount = 1)
	{
		if (IsGameOver) return;

		Lives = Mathf.Max(0, Lives - amount);
		EmitSignal(SignalName.LivesChanged, Lives);

		if (Lives == 0)
			DoGameOver();
	}

	// ---------- Bonus: โปรยเหรียญช่วงท้าย ----------
	private void StartBonusRainForRemainingTime()
	{
		if (_bonus == null) return;

		_bonusStartedThisLevel = true;
		_bonusRunning = true;

		_bonusScore = 0;
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore); // HUD: แสดง Bonus : 0 ตั้งแต่เริ่ม

		float duration = Mathf.Max(0f, _timeLeft);
		_bonus.Start(duration);

		GD.Print($"[ScoreManager] BONUS coins raining for last {duration:0.##}s.");
	}

	private void OnBonusStarted()
	{
		EmitSignal(SignalName.BonusPhaseStarted); // HUD แสดงแบนเนอร์
	}

	private void OnBonusTick(int value, int runningTotal)
	{
		_bonusScore = runningTotal;
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore); // HUD อัปเดตสด
	}

	private void OnBonusEnded(int totalBonus)
	{
		_bonusScore = totalBonus;

		// HUD เห็นยอดสุดท้าย
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);

		// รวมโบนัสเข้าคะแนนรวม (ไม่แตะ LevelScore)
		TotalScore += _bonusScore;
		if (TotalScore > HighScore)
		{
			HighScore = TotalScore;
			SaveHighScore();
		}
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);

		_bonusRunning = false;

		// แจ้ง HUD ให้ซ่อนแบนเนอร์ / เอฟเฟกต์
		EmitSignal(SignalName.BonusPhaseEnded, _bonusScore);

		// ถ้าเวลาหมดแล้วและยังไม่ได้เคลียร์ แต่ถึงเป้า → เคลียร์ตอนนี้
		if (_timeLeft <= 0f && !IsLevelCleared && LevelScore >= TargetScore)
			FinishCurrentLevel();

		GD.Print($"[ScoreManager] BONUS rain ended. Bonus={_bonusScore}, Total={TotalScore}");
	}

	// ---------- End-of-level / GameOver ----------
	private void FinishCurrentLevel()
	{
		if (IsLevelCleared) return;

		IsLevelCleared = true;
		SaveHighScore();

		GD.Print($"[ScoreManager] Level {Level} cleared at time-up. Score={LevelScore} (Target={TargetScore}), Bonus={_bonusScore}, Total={TotalScore}");
		EmitSignal(SignalName.LevelCleared, LevelScore, Level);

		if (AutoAdvanceOnTimeUp)
			AdvanceToNextLevel();
	}

	private void AdvanceToNextLevel()
	{
		int nextLevel = Level + 1;
		StartLevel(nextLevel);
	}

	private void FailByTimeUp()
	{
		IsGameOver = true;
		SaveHighScore();

		GD.Print($"[ScoreManager] Time up but target not met. Score={LevelScore}/{TargetScore} → GAME OVER");
		EmitSignal(SignalName.GameOver, LevelScore, Level);

		GetTree().Paused = true;
	}

	private void DoGameOver()
	{
		if (IsGameOver) return;

		IsGameOver = true;
		SaveHighScore();

		GD.Print("[ScoreManager] GAME OVER (lives = 0)");
		EmitSignal(SignalName.GameOver, LevelScore, Level);
		GetTree().Paused = true;
	}

	// ---------- External control ----------
	public void ResetForNewLevel(int newLevel, int newTargetIgnored, int lives)
	{
		Lives = lives;
		StartLevel(newLevel);
	}

	// ---------- Save / Load ----------
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
