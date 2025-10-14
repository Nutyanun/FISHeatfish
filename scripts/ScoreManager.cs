using Godot;
using System;

public partial class ScoreManager : Node2D
{
	// ===== Signals (หลัก) =====
	[Signal] public delegate void ScoreChangedEventHandler(int levelScore, int target);
	[Signal] public delegate void TotalScoreChangedEventHandler(int totalScore, int highScore);
	[Signal] public delegate void LivesChangedEventHandler(int lives);
	[Signal] public delegate void LevelChangedEventHandler(int level);
	[Signal] public delegate void MultiplierChangedEventHandler(int mult, int fishInWindow, int needFish, float windowLeft);
	[Signal] public delegate void TimeLeftChangedEventHandler(float timeLeft);
	[Signal] public delegate void LevelClearedEventHandler(int finalScore, int level);
	[Signal] public delegate void GameOverEventHandler(int finalScore, int level);

	// ===== Signals (Compatibility กับโค้ดเก่า – โบนัส) =====
	[Signal] public delegate void BonusScoreChangedEventHandler(int totalBonus);
	[Signal] public delegate void BonusPhaseStartedEventHandler();
	[Signal] public delegate void BonusPhaseEndedEventHandler(int totalBonus);

	// ===== Config =====
	[Export] public int BaseTargetScore { get; set; } = 300;
	[Export] public int TargetGrowthPerLevel { get; set; } = 100;
	[Export] public int StartLives { get; set; } = 3;
	[Export] public float LevelTimeSeconds { get; set; } = 150f;

	[Export] public int NeedFishForNextMult { get; set; } = 3;
	[Export] public float MultWindowSeconds { get; set; } = 6f;

	// ===== State =====
	private int _level = 1;
	private int _levelScore = 0;
	private int _targetScore = 0;

	private int _totalScore = 0;
	private int _highScore = 0;

	private int _lives = 0;

	private int _mult = 1;
	private int _fishInWindow = 0;
	private float _windowLeft = 0f;

	private float _timeLeft = 0f;
	private bool _running = false;

	// Compatibility flags
	private bool _flagLevelCleared = false;
	private bool _flagGameOver = false;

	// Bonus compatibility (stub)
	private int _bonusScore = 0;

	// ===== RO properties (รวมทั้ง compat names) =====
	public int Score => _levelScore;
	public int TargetScore => _targetScore;
	public int Lives => _lives;
	public int Level => _level;
	public int TotalScore => _totalScore;
	public int HighScore => _highScore;

	// ใช้แทนเมธอดเก่า sm.IsLevelCleared() / sm.IsGameOver()
	public bool IsLevelCleared => _flagLevelCleared;
	public bool IsGameOver => _flagGameOver;

	// ===== High score save/load อย่างง่าย =====
	private void LoadHighScore()
	{
		_highScore = ProjectSettings.HasSetting("game/high_score")
			? (int)ProjectSettings.GetSetting("game/high_score")
			: 0;
	}

	private void SaveHighScore()
	{
		if (_totalScore > _highScore)
		{
			_highScore = _totalScore;
			ProjectSettings.SetSetting("game/high_score", _highScore);
			ProjectSettings.Save();
		}
	}

	public override void _Ready()
	{
		LoadHighScore();
		StartRun();
	}

	public void StartRun()
	{
		_level = 1;
		_totalScore = 0;
		_lives = StartLives;
		_flagLevelCleared = false;
		_flagGameOver = false;
		_bonusScore = 0;

		StartLevel(_level);
		EmitAllState();
	}

	private void StartLevel(int level)
	{
		_level = level;
		_levelScore = 0;
		_targetScore = BaseTargetScore + (level - 1) * TargetGrowthPerLevel;

		_timeLeft = LevelTimeSeconds;
		_mult = 1;
		_fishInWindow = 0;
		_windowLeft = 0f;

		_flagLevelCleared = false;
		_flagGameOver = false;

		_running = true;

		EmitSignal(SignalName.LevelChanged, _level);
		EmitSignal(SignalName.ScoreChanged, _levelScore, _targetScore);
		EmitSignal(SignalName.TotalScoreChanged, _totalScore, _highScore);
		EmitSignal(SignalName.LivesChanged, _lives);
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);
	}

	private void EmitAllState()
	{
		EmitSignal(SignalName.LevelChanged, _level);
		EmitSignal(SignalName.ScoreChanged, _levelScore, _targetScore);
		EmitSignal(SignalName.TotalScoreChanged, _totalScore, _highScore);
		EmitSignal(SignalName.LivesChanged, _lives);
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

		// bonus stubs
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
	}

	public override void _Process(double delta)
	{
		if (!_running) return;

		// multiplier window
		if (_windowLeft > 0f)
		{
			_windowLeft -= (float)delta;
			if (_windowLeft <= 0f)
			{
				_windowLeft = 0f;
				_fishInWindow = 0;
				EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
			}
		}

		// timer
		if (_timeLeft > 0f)
		{
			_timeLeft -= (float)delta;
			if (_timeLeft < 0f) _timeLeft = 0f;
			EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

			if (_timeLeft <= 0f)
			{
				if (_levelScore >= _targetScore)
				{
					_running = false;
					_flagLevelCleared = true;
					EmitSignal(SignalName.LevelCleared, _levelScore, _level);
					StartLevel(_level + 1); // ไปด่านถัดไป
				}
				else
				{
					_running = false;
					_flagGameOver = true;
					SaveHighScore();
					EmitSignal(SignalName.GameOver, _totalScore, _level);
				}
			}
		}
	}

	// ===== Game events (ใหม่) =====
	public void EatFish(int baseScore)
	{
		if (!_running) return;

		_windowLeft = MultWindowSeconds;
		_fishInWindow++;
		if (_fishInWindow >= NeedFishForNextMult)
		{
			_mult++;
			_fishInWindow = 0;
		}

		int gained = baseScore * _mult;
		_levelScore += gained;
		_totalScore += gained;

		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
		EmitSignal(SignalName.ScoreChanged, _levelScore, _targetScore);
		EmitSignal(SignalName.TotalScoreChanged, _totalScore, _highScore);

		if (_levelScore >= _targetScore)
		{
			_running = false;
			_flagLevelCleared = true;
			EmitSignal(SignalName.LevelCleared, _levelScore, _level);
			StartLevel(_level + 1);
		}
	}

	public void PlayerDie()
	{
		if (!_running) return;

		_lives = Math.Max(0, _lives - 1);
		EmitSignal(SignalName.LivesChanged, _lives);

		if (_lives <= 0)
		{
			_running = false;
			_flagGameOver = true;
			SaveHighScore();
			EmitSignal(SignalName.GameOver, _totalScore, _level);
		}
		else
		{
			_mult = 1;
			_fishInWindow = 0;
			_windowLeft = 0f;
			EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, NeedFishForNextMult, _windowLeft);
		}
	}

	public void SyncRequestFromHud() => EmitAllState();

	// ===== Compatibility Layer (ให้ไฟล์เก่าใช้ต่อได้) =====

	// เมธอดชื่อเดิมที่โปรเจกต์เก่าเรียก
	public void AddScore(int pts) => EatFish(pts);
	public void LoseLife() => PlayerDie();

	// Overloads ที่บางไฟล์เดิมส่งมา 2–3 อาร์กิวเมนต์ → เมินตัวที่เหลือ
	public void AddScore(int pts, object _) => AddScore(pts);
	public void AddScore(int pts, object _, object __) => AddScore(pts);

	// LoseLife แบบรับเหตุผล/ข้อมูลเพิ่ม → เมินพารามิเตอร์
	public void LoseLife(object _) => LoseLife();

	// Bonus API (stubs)
	public int GetBonusScore() => _bonusScore;
	public int GetTotalWithBonus() => _totalScore + _bonusScore;
	public int LoadHighScoreForLevel(int level) => _highScore;
	public void StartBonusPhase() => EmitSignal(SignalName.BonusPhaseStarted);
	public void EndBonusPhase() => EmitSignal(SignalName.BonusPhaseEnded, _bonusScore);
	public void AddBonus(int amount)
	{
		_bonusScore += Math.Max(0, amount);
		EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
	}
}
