using Godot;
using System;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using Game;

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

	[Signal] public delegate void BonusScoreChangedEventHandler(int totalBonus);
	[Signal] public delegate void BonusPhaseStartedEventHandler();
	[Signal] public delegate void BonusPhaseEndedEventHandler(int totalBonus);

	// ===== Config =====
	[Export] public int  BaseTargetScore { get; set; } = 300;
	[Export] public int  TargetIncrement { get; set; } = 100;
	[Export] public int  StartLives      { get; set; } = 3;
	[Export] public bool InfiniteLives   { get; set; } = false;

	// ===== State =====
	public int Level { get; private set; } = 1;
	public int TotalScore { get; private set; }
	public int LevelScore { get; private set; }
	public int TargetScore { get; private set; }

	public float TimeLeftSec { get; private set; } = 120f;
	[Export] public bool CountDown = true;

	private int _mult = 1;
	private int _fishInWindow = 0;
	private int _needFish = 3;
	private float _windowLeft = 0f;

	private int _lives;
	private int _highScore = 0;

	private CrystalSpawner _crystal;

	private bool _isRunning = true;
	public  bool IsGameOver { get; private set; }
	public  bool IsLevelCleared { get; private set; }

	// Bonus (เก็บตัวเลขไว้ให้ HUD)
	private int  _bonusScore = 0;
	private bool _bonusEnabledThisLevel = false;

	private const string SAVE_FILE = "user://save.dat";

	// (ถ้ามีระบบจริง ให้แทนที่ได้)
	public static class GameProgress { public static int CurrentPlayingLevel = 1; }

	// ===== กติกาด่าน =====
	private readonly struct LevelRule
	{
		public readonly int Seconds;
		public readonly bool BonusOn;
		public readonly CrystalType[] CrystalColors;
		public readonly float CrystalIntervalSec;
		public readonly int   CrystalMaxOnScreen;

		public LevelRule(int seconds, bool bonusOn, CrystalType[] colors, float intervalSec, int maxOnScreen)
		{ Seconds = seconds; BonusOn = bonusOn; CrystalColors = colors; CrystalIntervalSec = intervalSec; CrystalMaxOnScreen = maxOnScreen; }
	}

	private static readonly CrystalType[] NONE = Array.Empty<CrystalType>();

	private readonly LevelRule[] _rules =
	{
		new LevelRule(120, false, NONE, 45f, 1),
		new LevelRule(150, true,  NONE, 45f, 1),
		new LevelRule(180, true,  NONE, 45f, 1),

		new LevelRule(180, true,  new[]{ CrystalType.Purple, CrystalType.Blue }, 45f, 2),
		new LevelRule(180, true,  new[]{ CrystalType.Purple, CrystalType.Blue, CrystalType.Green }, 45f, 3),

		new LevelRule(180, true,  new[]{ CrystalType.Red, CrystalType.Blue, CrystalType.Green, CrystalType.Pink, CrystalType.Purple }, 45f, 5),
		new LevelRule(180, true,  new[]{ CrystalType.Red, CrystalType.Blue, CrystalType.Green, CrystalType.Pink, CrystalType.Purple }, 45f, 5),
	};

	public override void _Ready()
	{
		LoadHighScore();
		Level = (GameProgress.CurrentPlayingLevel > 0) ? GameProgress.CurrentPlayingLevel : 1;

		_crystal = GetNodeOrNull<CrystalSpawner>("%CrystalSpawner") ?? GetNodeOrNull<CrystalSpawner>("CrystalSpawner");

		_lives = InfiniteLives ? int.MaxValue / 2 : StartLives;
		EmitSignal(SignalName.LivesChanged, _lives);

		StartLevel(Level);
	}

	private LevelRule GetRule(int lv)
	{
		if (lv <= 0) lv = 1;
		if (lv > _rules.Length) lv = _rules.Length;
		return _rules[lv - 1];
	}

	private void StartLevel(int nextLevel)
	{
		Level = Math.Max(1, nextLevel);
		var rule = GetRule(Level);

		TimeLeftSec = rule.Seconds;
		TargetScore = BaseTargetScore + (Level - 1) * TargetIncrement;
		LevelScore  = 0;

		EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);
		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);

		_bonusScore = 0;
		_bonusEnabledThisLevel = rule.BonusOn;

		_mult = 1; _fishInWindow = 0; _windowLeft = 0f;

		IsLevelCleared = false; IsGameOver = false; _isRunning = true;

		// ใช้ CrystalType (global) ตรง ๆ ได้เลย เพราะ CrystalSpawner ก็ใช้ตัวเดียวกันแล้ว
		_crystal?.ApplyRule(rule.CrystalColors, rule.CrystalIntervalSec, rule.CrystalMaxOnScreen);

		EmitSignal(SignalName.LevelChanged, Level);
	}

	public override void _Process(double delta)
	{
		if (!_isRunning || IsGameOver || IsLevelCleared) return;
		float dt = (float)delta;

		if (CountDown) { TimeLeftSec -= dt; if (TimeLeftSec < 0f) TimeLeftSec = 0f; }
		else           { TimeLeftSec += dt; }

		EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);

		if (CountDown && TimeLeftSec <= 0f)
		{
			if (LevelScore >= TargetScore) OnLevelCleared();
			else                           OnGameOver();
		}

		if (_windowLeft > 0f)
		{
			_windowLeft -= dt;
			if (_windowLeft <= 0f)
			{
				_mult = 1; _fishInWindow = 0; _windowLeft = 0f;
				EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft);
			}
		}
	}

	// ====== คะแนน ======
	public void AddScore(int baseScore)
	{
		if (IsGameOver || IsLevelCleared) return;
		int add = Math.Max(0, baseScore) * Math.Max(1, _mult);
		LevelScore += add; TotalScore += add;

		if (TotalScore > _highScore) { _highScore = TotalScore; SaveHighScore(); }

		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, _highScore);

		_fishInWindow++; _windowLeft = Mathf.Max(_windowLeft, 3.5f);
		if (_fishInWindow >= _needFish && _mult < 5)
		{
			_mult++; _fishInWindow = 0;
			EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft);
		}
	}
	public void AddScore(int baseScore, object _unused) => AddScore(baseScore);

	// ====== ชีวิต ======
	public void LoseLife(int n = 1)
	{
		if (InfiniteLives || IsGameOver || IsLevelCleared) return;
		_lives -= Math.Max(1, n); if (_lives < 0) _lives = 0;
		EmitSignal(SignalName.LivesChanged, _lives);
		if (_lives <= 0) OnGameOver();
	}

	private void OnLevelCleared()
	{
		if (IsLevelCleared || IsGameOver) return;
		IsLevelCleared = true; _isRunning = false;
		EmitSignal(SignalName.LevelCleared, LevelScore, Level);
	}

	private void OnGameOver()
	{
		if (IsGameOver || IsLevelCleared) return;
		IsGameOver = true; _isRunning = false;
		EmitSignal(SignalName.GameOver, LevelScore, Level);
	}

	public void SetRunning(bool run) { _isRunning = run && !IsGameOver && !IsLevelCleared; }
	public void GoToNextLevel() => StartLevel(Level + 1);

	// ====== Save / Load ไฮสกอร์อย่างง่าย ======
	private void LoadHighScore()
	{
		if (!FileAccess.FileExists(SAVE_FILE)) { _highScore = 0; return; }
		using var f = FileAccess.Open(SAVE_FILE, FileAccess.ModeFlags.Read);
		if (f == null) { _highScore = 0; return; }
		_highScore = (int)f.Get32();
	}
	private void SaveHighScore()
	{
		using var f = FileAccess.Open(SAVE_FILE, FileAccess.ModeFlags.Write);
		f.Store32((uint)_highScore);
	}

	// ====== ยูทิล/เมธอดเสริมที่ HUD/Skill เรียกหา ======
	public float GetTimeLeft() => TimeLeftSec;

	public void SyncRequestFromHud()
	{
		EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);
		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, _highScore);
		EmitSignal(SignalName.LivesChanged, _lives);
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft);
		EmitSignal(SignalName.LevelChanged, Level);
	}

	public int  GetBonusScore()      => _bonusScore;
	public int  GetTotalWithBonus()  => TotalScore + _bonusScore;
	public int  LoadHighScoreForLevel(int _level) => _highScore;

	// ====== ตัวคูณ (รองรับหลายรูปแบบการเรียก) ======
	public void AddMultiplierFromCrystal(int add = 1)
	{
		int before = _mult;
		_mult = Math.Clamp(_mult + Math.Max(1, add), 1, 9);
		if (_mult != before)
			EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft);
	}
	public void AddMultiplierFromCrystal(CrystalType crystal)
	{
		int delta = (crystal == CrystalType.Pink) ? 2 : 1;
		AddMultiplierFromCrystal(delta);
	}
	public void AddMultiplierFromCrystal(object _) => AddMultiplierFromCrystal(1);
}
