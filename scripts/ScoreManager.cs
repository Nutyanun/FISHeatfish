using Godot;
using System;
using System.Collections.Generic;

public partial class ScoreManager : Node2D
{
	// ====== Signals ======
	[Signal] public delegate void ScoreChangedEventHandler(int levelScore, int target);
	[Signal] public delegate void TotalScoreChangedEventHandler(int totalScore, int highScore);
	[Signal] public delegate void LivesChangedEventHandler(int lives);
	[Signal] public delegate void LevelChangedEventHandler(int level);
	[Signal] public delegate void MultiplierChangedEventHandler(int mult, int fishInWindow, int needFish, float windowLeft);
	[Signal] public delegate void TimeLeftChangedEventHandler(float timeLeft);
	[Signal] public delegate void LevelClearedEventHandler(int finalScore, int level);
	[Signal] public delegate void GameOverEventHandler(int finalScore, int level);

	// HUD / Bonus UI signals
	[Signal] public delegate void BonusScoreChangedEventHandler(int totalBonus);
	[Signal] public delegate void BonusPhaseStartedEventHandler();
	[Signal] public delegate void BonusPhaseEndedEventHandler(int totalBonus);

	// ====== Config ด่าน ======
	[Export] public int  BaseTargetScore { get; set; } = 300;
	[Export] public int  BaseTimeSeconds { get; set; } = 150;
	[Export] public int  TimeIncPerLevel { get; set; } = 90;
	[Export] public bool AutoAdvanceOnTimeUp { get; set; } = false;

	// ====== ค่าเริ่มต้นชีวิตต่อเลเวล ======
	[Export] public int StartingLives { get; set; } = 3;

	// ====== โปรยเหรียญช่วงท้ายเกม ======
	[Export] public NodePath BonusSpawnerPath { get; set; } = "../BonusCoinSpawner";
	[Export] public float BonusTriggerSeconds { get; set; } = 20f;
	[Export] public int   BonusStartLevel    { get; set; } = 2;

	private BonusCoinSpawner _bonus;
	private bool _bonusStartedThisLevel = false;
	private bool _bonusRunning = false;
	private int  _bonusScore = 0;

	// ====== สถานะหลัก ======
	[Export] public int Level { get; set; } = 1;
	[Export] public int Lives { get; set; } = 3;

	public int TargetScore { get; private set; } = 300;
	public int LevelScore  { get; private set; } = 0;
	public int TotalScore  { get; private set; } = 0;
	public int HighScore   { get; private set; } = 0;
	public int Score => LevelScore;

	public bool IsGameOver     { get; private set; } = false;
	public bool IsLevelCleared { get; private set; } = false;

	// ====== Multiplier ======
	[Export] public int   FishPerStep   { get; set; } = 10;
	[Export] public float WindowSeconds { get; set; } = 20f;
	[Export] public int   MultMin       { get; set; } = 1;
	[Export] public int   MultMax       { get; set; } = 5;

	public int   Mult { get; private set; } = 1;
	private int   _fishInWindow = 0;
	private float _windowLeft;

	// ====== เวลา ======
	private float _timeLeft;
	private const string SAVE_PATH = "user://save_highscore.dat";

	// ====== ระบบนับปลาที่กินได้ ======
	public Dictionary<string, int> FishCountByType { get; private set; } = new();

	// ---------- Helpers ----------
	private int TargetForLevel(int lvl) => BaseTargetScore << (lvl - 1);
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
		GameProgress.ResetFishCount();
		Level = Mathf.Max(1, lvl);
		Lives = StartingLives;
		TargetScore = TargetForLevel(Level);
		HighScore = LoadHighScoreForLevel(Level);
		LevelScore = 0;
		IsLevelCleared = false;
		IsGameOver = false;
		_bonusScore = 0;
		_bonusStartedThisLevel = false;
		_bonusRunning = false;
		Mult = Mathf.Clamp(MultMin, MultMin, MultMax);
		_fishInWindow = 0;
		_windowLeft = WindowSeconds;
		_timeLeft = TimeForLevel(Level);
		FishCountByType.Clear();

		EmitSignal(SignalName.LevelChanged, Level);
		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.MultiplierChanged, Mult, _fishInWindow, FishPerStep, _windowLeft);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

		GD.Print($"[ScoreManager] Start Level {Level} | Target={TargetScore}, Time={_timeLeft}s");
	}

	// ---------- Process ----------
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

		// เริ่มโปรยโบนัสเมื่อเวลาเหลือ ≤ BonusTriggerSeconds
		if (!_bonusStartedThisLevel && Level >= BonusStartLevel && _timeLeft <= BonusTriggerSeconds)
			StartBonusRainForRemainingTime();

		// หมดเวลา → สรุปผล
		if (_timeLeft <= 0f && !IsLevelCleared)
		{
			if (_bonusRunning) return;

			if (LevelScore >= TargetScore)
				FinishCurrentLevel();
			else
				FailByTimeUp();
		}
	}

	// ---------- Gameplay ----------
	public void AddScore(int basePoints, string fishType = "fish1")
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

		GD.Print($"[ScoreManager] AddScore from {fishType}, +{basePoints} (x{Mult})");
	}

	public void LoseLife(int amount = 1)
	{
		if (IsGameOver) return;

		Lives = Mathf.Max(0, Lives - amount);
		EmitSignal(SignalName.LivesChanged, Lives);

		if (Lives == 0)
			DoGameOver();
	}

	// ---------- Bonus ----------
	private void StartBonusRainForRemainingTime()
	{
		if (_bonus == null) return;

		_bonusStartedThisLevel = true;
		_bonusRunning = true;
		_bonusScore = 0;

		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
		EmitSignal(SignalName.BonusPhaseStarted);

		float duration = Mathf.Max(0f, _timeLeft);
		_bonus.Start(duration);

		GD.Print($"[ScoreManager] BONUS started for {duration:0.##}s");
	}

	private void OnBonusStarted() => GD.Print("[ScoreManager] Bonus phase started");
	private void OnBonusTick(int value, int runningTotal)
	{
	_bonusScore = runningTotal;
	EmitSignal(SignalName.BonusScoreChanged, _bonusScore);

	// เพิ่มบรรทัดนี้ เพื่อให้ HUD อัปเดต TotalScore สดระหว่างโบนัส
	EmitSignal(SignalName.TotalScoreChanged, LevelScore + _bonusScore, HighScore);
	}


	private void OnBonusEnded(int totalBonus)
	{
		_bonusScore = totalBonus;
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);

		TotalScore += _bonusScore;
		if (TotalScore > HighScore)
		{
			HighScore = TotalScore;
			SaveHighScore();
		}
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);

		_bonusRunning = false;
		EmitSignal(SignalName.BonusPhaseEnded, _bonusScore);

		if (_timeLeft <= 0f && !IsLevelCleared && LevelScore >= TargetScore)
			FinishCurrentLevel();

		GD.Print($"[ScoreManager] BONUS ended. Bonus={_bonusScore}, Total={TotalScore}");
	}

	// ---------- Level End / GameOver ----------
	private void FinishCurrentLevel()
	{
		if (IsLevelCleared) return;
		IsLevelCleared = true;
		SaveHighScore();
		
		// ✅ ตรวจสอบและบันทึก High Score ของเลเวลนี้
	int prevHigh = LoadHighScoreForLevel(Level);
	if (LevelScore > prevHigh)
	{
		SaveHighScoreForLevel(Level, LevelScore);
		GameProgress.LastHighScore = LevelScore;
		GD.Print($"[ScoreManager] New High Score for Level {Level}: {LevelScore}");
	}
	else
	{
		GameProgress.LastHighScore = prevHigh;
		GD.Print($"[ScoreManager] High Score for Level {Level} remains {prevHigh}");
	}
	// ✅ บันทึก high score แยกแต่ละด่าน
	if (LevelScore > LoadHighScoreForLevel(Level))
	{
	SaveHighScoreForLevel(Level, LevelScore);
	GD.Print($"[ScoreManager] New High Score saved for Level {Level}: {LevelScore}");
	}

		GD.Print($"[ScoreManager] Level {Level} cleared | Score={LevelScore} | Bonus={_bonusScore}");
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

		GD.Print($"[ScoreManager] Time up | Score={LevelScore}/{TargetScore} → GAME OVER");
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

	// ---------- External ----------
	public void ResetForNewLevel(int newLevel, int newTargetIgnored, int lives)
	{
		Lives = lives;
		StartLevel(newLevel);
	}

	// ---------- Save / Load ----------
	public void SaveHighScore()
	{
		using var f = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Write);
		f.Store32((uint)HighScore);
	}

	public void LoadHighScore()
	{
		if (!FileAccess.FileExists(SAVE_PATH)) { HighScore = 0; return; }
		using var f = FileAccess.Open(SAVE_PATH, FileAccess.ModeFlags.Read);
		HighScore = (int)f.Get32();
	}
	
	// ---------- High Score ต่อเลเวล ----------
	private string GetHighScorePath(int levelIndex)
{
	return $"user://highscore_level{levelIndex}.dat";
}

public int LoadHighScoreForLevel(int levelIndex)
{
	var path = GetHighScorePath(levelIndex);
	if (!FileAccess.FileExists(path)) return 0;
	using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
	return (int)f.Get32();
}

public void SaveHighScoreForLevel(int levelIndex, int score)
{
	using var f = FileAccess.Open(GetHighScorePath(levelIndex), FileAccess.ModeFlags.Write);
	f.Store32((uint)score);
}



	// ---------- Bonus Getter ----------
	public int GetBonusScore() => _bonusScore;
	public int GetTotalWithBonus() => LevelScore + _bonusScore;
}
