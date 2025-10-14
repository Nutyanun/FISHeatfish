using Godot;
using System;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;

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

	// ===== Bonus-related Signals =====
	[Signal] public delegate void BonusScoreChangedEventHandler(int totalBonus);
	[Signal] public delegate void BonusPhaseStartedEventHandler();
	[Signal] public delegate void BonusPhaseEndedEventHandler(int totalBonus);

	// ===== Config =====
	[Export] public int BaseTargetScore { get; set; } = 300;
	[Export] public int TargetGrowthPerLevel { get; set; } = 100;

	[Export] public int BaseTimeSeconds { get; set; } = 150;
	[Export] public int TimeGrowthPerLevel { get; set; } = 30;

	[Export] public int StartingLives { get; set; } = 3;

	// ===== Bonus =====
	[Export] public NodePath BonusSpawnerPath { get; set; } = "../BonusCoinSpawner";
	[Export] public float BonusTriggerSeconds { get; set; } = 20f;
	[Export] public int BonusStartLevel { get; set; } = 2;

	private BonusCoinSpawner _bonus;
	private bool _bonusStarted = false;
	private bool _bonusRunning = false;
	private int _bonusScore = 0;

	// ===== Multiplier =====
	[Export] public int MaxMult { get; set; } = 5;
	[Export] public int NeedFishForNextMult { get; set; } = 10;
	[Export] public float MultWindowSeconds { get; set; } = 20f;

	private int _fishInWindow = 0;
	private float _windowLeft = 0f;
	private int _mult = 1;

	// ===== State =====
	public int Level { get; private set; } = 1;
	public int Lives { get; private set; } = 3;
	public int LevelScore { get; private set; } = 0;
	public int TotalScore { get; private set; } = 0;
	public int HighScore { get; private set; } = 0;
	
	// ====== สถานะเกม ======
public bool IsLevelCleared { get; private set; } = false;
public bool IsGameOver { get; private set; } = false;

	private int _targetScore;
	private float _timeLeft;
	private bool _isRunning = false;
	private bool _isLevelCleared = false;
	private bool _isGameOver = false;

	// ===== Save paths =====
	private const string SAVE_PATH = "user://save_highscore.dat";

	public override void _Ready()
	{
		LoadHighScore();

		if (GameProgress.CurrentPlayingLevel > 0)
			Level = GameProgress.CurrentPlayingLevel;

		Lives = StartingLives;
		StartLevel(Level);

		// connect BonusSpawner
		if (!BonusSpawnerPath.IsEmpty)
		{
			_bonus = GetNodeOrNull<BonusCoinSpawner>(BonusSpawnerPath);
			if (_bonus != null)
			{
				_bonus.BonusStarted += OnBonusStarted;
				_bonus.BonusEnded += OnBonusEnded;
				_bonus.BonusTick += OnBonusTick;
			}
		}

		GD.Print($"[ScoreManager] Ready → Level {Level}");
	}

	// ===== Start Level =====
	private void StartLevel(int lvl)
	{
		GameProgress.ResetFishCount();
		Level = Mathf.Max(1, lvl);
		Lives = StartingLives;

		_targetScore = BaseTargetScore + (Level - 1) * TargetGrowthPerLevel;
		_timeLeft = BaseTimeSeconds + (Level - 1) * TimeGrowthPerLevel;

		LevelScore = 0;
		_bonusScore = 0;
		_bonusStarted = false;
		_bonusRunning = false;

		_mult = 1;
		_fishInWindow = 0;
		_windowLeft = MultWindowSeconds;

		_isLevelCleared = false;
		_isGameOver = false;
		_isRunning = true;

		HighScore = LoadHighScoreForLevel(Level);

		EmitSignal(SignalName.LevelChanged, Level);
		EmitSignal(SignalName.ScoreChanged, LevelScore, _targetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);
	}

	// ===== Process =====
	public override void _Process(double delta)
	{
		if (!_isRunning || _isGameOver) return;

		// --- multiplier timer ---
		_windowLeft -= (float)delta;
		if (_windowLeft <= 0f)
		{
			_windowLeft = MultWindowSeconds;
			_fishInWindow = 0;
			EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
		}

		// --- time countdown ---
		_timeLeft = Mathf.Max(0, _timeLeft - (float)delta);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

		// --- trigger bonus phase ---
		if (!_bonusStarted && Level >= BonusStartLevel && _timeLeft <= BonusTriggerSeconds)
			StartBonusRainForRemainingTime();

		// --- time up ---
		if (_timeLeft <= 0f && !_isLevelCleared)
		{
			if (_bonusRunning) return;
			if (LevelScore >= _targetScore)
				FinishCurrentLevel();
			else
				FailByTimeUp();
		}
	}

	// ===== Gameplay =====
	public void AddScore(int basePoints, string fishType = "fish1")
	{
		if (_isGameOver) return;

		_fishInWindow++;
		if (_fishInWindow >= NeedFishForNextMult)
		{
			_mult = Mathf.Min(_mult + 1, MaxMult);
			_fishInWindow = 0;
		}
		_windowLeft = MultWindowSeconds;

		int gained = basePoints * _mult;
		LevelScore += gained;
		TotalScore += gained;

		if (TotalScore > HighScore)
		{
			HighScore = TotalScore;
			SaveHighScore();
		}

		EmitSignal(SignalName.ScoreChanged, LevelScore, _targetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);

		GD.Print($"[ScoreManager] +{gained} (x{_mult}) from {fishType}");
	}

	public void LoseLife(int amount = 1)
	{
		if (_isGameOver) return;
		Lives = Mathf.Max(0, Lives - amount);
		EmitSignal(SignalName.LivesChanged, Lives);

		if (Lives == 0) DoGameOver();
	}

	// ===== Bonus =====
	private void StartBonusRainForRemainingTime()
	{
		if (_bonus == null) return;
		_bonusStarted = true;
		_bonusRunning = true;
		_bonusScore = 0;

		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
		EmitSignal(SignalName.BonusPhaseStarted);

		float duration = Mathf.Max(0f, _timeLeft);
		_bonus.Start(duration);
		GD.Print($"[ScoreManager] BONUS started for {duration:0.##}s");
	}

	private void OnBonusTick(int value, int runningTotal)
	{
		_bonusScore = runningTotal;
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
		EmitSignal(SignalName.TotalScoreChanged, LevelScore + _bonusScore, HighScore);
	}

	private void OnBonusEnded(int totalBonus)
	{
		_bonusScore = totalBonus;
		_bonusRunning = false;
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
		TotalScore += _bonusScore;
		if (TotalScore > HighScore) { HighScore = TotalScore; SaveHighScore(); }
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
		EmitSignal(SignalName.BonusPhaseEnded, _bonusScore);
		if (_timeLeft <= 0f && !_isLevelCleared && LevelScore >= _targetScore)
			FinishCurrentLevel();
	}

	private void OnBonusStarted() => GD.Print("[ScoreManager] Bonus phase started");

	// ===== Level End =====
private void FinishCurrentLevel()
{
	if (_isLevelCleared) return;
	_isLevelCleared = true;
	_isRunning = false;

	// ✅ เซฟ HighScore ต่อเลเวล
	SaveHighScoreForLevel(Level, Math.Max(LoadHighScoreForLevel(Level), LevelScore));

	// ✅ อัปเดตค่าใน GameProgress สำหรับไปหน้า ScoreScene
	GameProgress.LastLevelScore = LevelScore;
	GameProgress.LastBonusScore = _bonusScore;
	GameProgress.LastTotalScore = LevelScore + _bonusScore;
	GameProgress.LastHighScore = HighScore;

	// ====== บันทึก progress ต่อชื่อผู้เล่น ======
	var pl = PlayerLogin.Instance;
	if (pl?.CurrentUser != null)
	{
		string name = pl.CurrentUser.PlayerName;

		// โหลด doc หลังจากแน่ใจว่ามี player แล้ว
		var doc = LeaderboardStore.LoadDoc();
		doc = LeaderboardStore.EnsureRoot(doc);
		var players = (GDict)doc["players"];

		if (players.ContainsKey(name))
		{
			var p = (GDict)players[name];

			// อัปเดตด่านล่าสุดที่เล่นถึง
			p["current_level"] = Level;

			// บันทึก high score ต่อเลเวล
			if (!p.ContainsKey("high_scores"))
				p["high_scores"] = new GDict();

			var hs = (GDict)p["high_scores"];
			if (!hs.ContainsKey(Level.ToString()) || (int)(long)hs[Level.ToString()] < LevelScore)
				hs[Level.ToString()] = LevelScore;

			// ✅ เซฟไฟล์กลับ
			LeaderboardStore.SaveDoc(doc);
			GD.Print($"[ScoreManager] Saved progress for {name}: Level={Level}, High={LevelScore}");
		}
	}

	// ✅ ส่งสัญญาณกลับ HUD / Game
	EmitSignal(SignalName.LevelCleared, LevelScore, Level);
}


	private void FailByTimeUp()
	{
		_isGameOver = true;
		_isRunning = false;
		SaveHighScore();
		GD.Print($"[ScoreManager] Time up | Score={LevelScore}/{_targetScore} → GAME OVER");
		EmitSignal(SignalName.GameOver, LevelScore, Level);
	}

	private void DoGameOver()
	{
		if (_isGameOver) return;
		_isGameOver = true;
		_isRunning = false;
		SaveHighScore();
		EmitSignal(SignalName.GameOver, LevelScore, Level);
	}

	// ===== Save / Load =====
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

	private string GetHighScorePath(int levelIndex) => $"user://highscore_level{levelIndex}.dat";

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

	public int GetBonusScore() => _bonusScore;
	public int GetTotalWithBonus() => LevelScore + _bonusScore;
	
	// ===== Compatibility / HUD Sync =====
public void SyncRequestFromHud()
{
	// ส่งสถานะทั้งหมดกลับ HUD ทันที (เหมือนตอนเริ่มเกม)
	EmitSignal(SignalName.LevelChanged, Level);
	EmitSignal(SignalName.ScoreChanged, LevelScore, _targetScore);
	EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
	EmitSignal(SignalName.LivesChanged, Lives);
	EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
	EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

	// สัญญาณโบนัส (ถ้ามี)
	EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
}

// ===== Time control for SkillManager =====
public void AddTime(double seconds)
{
	// เพิ่มเวลาที่เหลือในด่าน
	_timeLeft += (float)seconds;
	GD.Print($"[ScoreManager] AddTime +{seconds:F1}s → {_timeLeft:F1}s");
	EmitSignal(SignalName.TimeLeftChanged, _timeLeft);
}

public void ReduceTime(double seconds)
{
	// ลดเวลา แต่ไม่ให้ติดลบ
	_timeLeft = Mathf.Max(0, _timeLeft - (float)seconds);
	GD.Print($"[ScoreManager] ReduceTime -{seconds:F1}s → {_timeLeft:F1}s");
	EmitSignal(SignalName.TimeLeftChanged, _timeLeft);
}

}
