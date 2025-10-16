using Godot;
using System;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;

public partial class ScoreManager : Node2D
{
	public static ScoreManager Instance { get; private set; }

public override void _EnterTree()
{
	// จะถูกเรียกก่อน _Ready() เสมอ → เหมาะสำหรับ Autoload
	Instance = this;
	GD.Print("[ScoreManager] Autoload instance ready.");
}

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
	[Export] public int  TargetIncrement { get; set; } = 1000;
	[Export] public int  StartLives      { get; set; } = 3;
	[Export] public bool InfiniteLives   { get; set; } = false;
	
	// === Combo / Multiplier config ===
	[Export] public int  ComboFishRequired = 10;   // ต้องกินให้ครบกี่ตัวต่อรอบ
	[Export] public float ComboWindowSec   = 15f;  // ภายในกี่วินาที
	
	// ==== CRYSTAL SPAWNER HOOK ====
	[Export] public NodePath CrystalSpawnerPath { get; set; } = null; // ลากโหนด Spawner มาวางได้
	private CrystalSpawner _crys;     // อ้างอิงสปอว์นเนอร์
	private bool _pinkForcedThisLevel; // กันเรียกบังคับชมพูซ้ำ

	// ===== State =====
	public int Level { get; private set; } = 1;
	public int TotalScore { get; private set; }
	public int LevelScore { get; private set; }
	public int TargetScore { get; private set; }

	public float TimeLeftSec { get; private set; } = 90f;
	[Export] public bool CountDown = true;

	private int _mult = 1;
	private int _fishInWindow = 0;
	private int _needFish = 3;
	private float _windowLeft = 0f;

	private int _lives;
	private int _highScore = 0;

	private CrystalSpawner _crystal;
	
	private BonusCoinSpawner _bonus;

	private bool _coinScheduledLast20 = false;
	private bool _pinkScheduledLast20 = false;
	private float _prevTimeLeft = 0f;
	private bool _bonusEnabledThisLevel = false;

	private bool _isRunning = true;
	public  bool IsGameOver { get; private set; }

	// ✅ ใช้คู่กับ GameProgress เพื่อไม่ให้นับด่านเกิน
	public  bool IsLevelCleared { get; private set; }

	// Bonus (เก็บตัวเลขไว้ให้ HUD)
	private int  _bonusScore = 0;

	private const string SAVE_FILE = "user://save.dat";
	
	public void AddTime(double s){ TimeLeftSec += (float)s; if(CountDown && TimeLeftSec<0) TimeLeftSec=0; EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec); }
	public void AddTimeSeconds(double s)=>AddTime(s);
	public void AddBonusTime(double s)=>AddTime(s);
	public void ReduceTime(double s)=>AddTime(-Math.Abs(s));
	public void SubtractTime(double s)=>AddTime(-Math.Abs(s));
	
	// ===== กติกาด่าน =====
	private readonly struct LevelRule
{
	public readonly int Seconds;
	public readonly bool BonusOn;
	public readonly CrystalType[] CrystalColors;
	public readonly float CrystalIntervalSec;
	public readonly int   CrystalMaxOnScreen;

	public LevelRule(int seconds, bool bonusOn, CrystalType[] colors, float intervalSec, int maxOnScreen)
	{
		Seconds = seconds;
		BonusOn = bonusOn;
		CrystalColors = colors;
		CrystalIntervalSec = intervalSec;
		CrystalMaxOnScreen = maxOnScreen;
	}
}

private static readonly CrystalType[] NONE = Array.Empty<CrystalType>();

// ปรับตามสเปค:
// L1-L3 ไม่มีคริสตัล
// L4: Green+Blue (2 สี), L5: +Purple, L6: +Red, L7: +Pink
private readonly LevelRule[] _rules =
{
	new LevelRule(90, false, NONE, 45f, 1),
	new LevelRule(120, true,  NONE, 45f, 1),
	new LevelRule(150, true,  NONE, 45f, 1),

	new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue }, 45f, 2),                                               // L4
	new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue, CrystalType.Purple }, 45f, 3),                             // L5
	new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red }, 45f, 4),           // L6
	new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red, CrystalType.Pink }, 45f, 5), // L7
};

	// ===== INITIALIZE =====
	public override void _Ready()
	{
		LoadHighScore();
		Level = (GameProgress.CurrentPlayingLevel > 0) ? GameProgress.CurrentPlayingLevel : 1;

		_crystal = GetNodeOrNull<CrystalSpawner>("%CrystalSpawner") ?? GetNodeOrNull<CrystalSpawner>("CrystalSpawner");
		
		if (CrystalSpawnerPath != null && !CrystalSpawnerPath.IsEmpty)
		_crys = GetNode<CrystalSpawner>(CrystalSpawnerPath);
	else
		_crys = GetTree().CurrentScene?.FindChild("CrystalSpawner", true, false) as CrystalSpawner;
		_bonus = GetNodeOrNull<BonusCoinSpawner>("%BonusCoinSpawner")
		?? GetNodeOrNull<BonusCoinSpawner>("BonusCoinSpawner");

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
// รีเซ็ตธงช่วง 20 วิท้าย + กันเศษโบนัสค้างจากด่านก่อน
		_coinScheduledLast20 = false;
		_pinkScheduledLast20 = false;
		_prevTimeLeft = TimeLeftSec;
		_bonus?.ForceStopAndClear();

		_mult = 1; _fishInWindow = 0; _windowLeft = 0f;

		IsLevelCleared = false;
		IsGameOver = false;
		GameProgress.IsLevelCleared = false;  // 🟢 รีเซ็ตทุกครั้งที่เริ่มด่าน
		_isRunning = true;
// ✅ ใช้ Level (ตัวใหญ่) และไม่ประกาศชื่อ rule ซ้ำ
if (Level >= 4 && _crystal != null)
{
	var lvRule = _rules[Level - 1];
	// ไม่ต้อง MapColors อีกแล้ว ส่งตรง ๆ
	_crystal.ApplyRule(lvRule.CrystalColors, lvRule.CrystalIntervalSec, lvRule.CrystalMaxOnScreen);
	_crystal.ResetPinkForced();
}

		EmitSignal(SignalName.LevelChanged, Level);
		_pinkForcedThisLevel = false;
		
	}

	public override void _Process(double delta)
	{
		
		if (!_isRunning || IsGameOver || IsLevelCleared) return;
		float dt = (float)delta;

		if (CountDown)
		{
			TimeLeftSec -= dt;
			if (TimeLeftSec < 0f) TimeLeftSec = 0f;
		}
		else
		{
			TimeLeftSec += dt;
		}

		EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);
		
		// === Trigger 20 วินาทีสุดท้าย ===
		bool justEnteredLast20 = CountDown && (_prevTimeLeft > 20f) && (TimeLeftSec <= 20f);
			if (justEnteredLast20)
{
		// 1) coins ตกเฉพาะ 20 วิท้าย เฉพาะด่านที่เปิดโบนัส
			if (_bonusEnabledThisLevel && _bonus != null && !_coinScheduledLast20)
	{
				int duration = Mathf.CeilToInt(Mathf.Max(0f, TimeLeftSec));
			if (duration > 0)
		{
				_bonus.ApplyLevelTuning(duration: duration);
				_bonus.Start(duration);
				_coinScheduledLast20 = true;
		}
	}

		// 2) (ถ้ามีเพชร L4+) บังคับชมพู 1 ชิ้น
			if (_crystal != null && !_pinkScheduledLast20 && Level >= 4)
	{
				_crystal.ForcePinkOnceInLastWindow();
				_pinkScheduledLast20 = true;
	}
}
				_prevTimeLeft = TimeLeftSec;
if (CountDown && TimeLeftSec <= 0f)
		{
			if (LevelScore >= TargetScore)
			{
				_bonus?.StopNow();
				OnLevelCleared();
			}
			else
			{
				_bonus?.StopNow();
				OnGameOver();
			}
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

	// ===== SCORE SYSTEM =====
	public void AddScore(int baseScore)
	{
		if (IsGameOver || IsLevelCleared) return;
		int add = Math.Max(0, baseScore) * Math.Max(1, _mult);
		LevelScore += add;
		TotalScore += add;

		if (TotalScore > _highScore)
		{
			_highScore = TotalScore;
			SaveHighScore();
		}

		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, _highScore);

		// === Combo / Multiplier ===
		// เริ่มหน้าต่างคอมโบเมื่อกินตัวแรก หรือถ้ายังมีหน้าต่างอยู่ก็ขยายให้อยู่ที่ 20 วิเสมอ
		if (_windowLeft <= 0f)
{
		_windowLeft = ComboWindowSec;
		_fishInWindow = 0;
		_needFish = ComboFishRequired;
}
		else
{
	// ต่อเวลาให้สูงสุดไม่เกิน 20 วิ (กันค้างสั้นเกิน)
		_windowLeft = Math.Max(_windowLeft, ComboWindowSec);
}
		_fishInWindow++;
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft);

		if (_fishInWindow >= ComboFishRequired)
{
		_mult = Math.Min(9, _mult + 1);         // เพิ่มคูณ (เพดาน 9)
		_fishInWindow = 0;                      // รีเซ็ตนับรอบใหม่
		_needFish = ComboFishRequired;
		_windowLeft = ComboWindowSec;           // เริ่มรอบคอมโบใหม่ 20 วิ
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft);
}

	}
	public void AddScore(int baseScore, object _unused) => AddScore(baseScore);

	// ===== LIVES =====
	public void LoseLife(int n = 1)
	{
		if (InfiniteLives || IsGameOver || IsLevelCleared) return;
		_lives -= Math.Max(1, n);
		if (_lives < 0) _lives = 0;
		EmitSignal(SignalName.LivesChanged, _lives);
		if (_lives <= 0) OnGameOver();
	}

	// ===== CLEAR / GAMEOVER =====
	private void OnLevelCleared()
{
	if (IsLevelCleared || IsGameOver) return;
	IsLevelCleared = true;
	GameProgress.IsLevelCleared = true;  // ✅ ผ่านด่าน

	// 🟢 เพิ่มส่วนนี้ ↓↓↓
	GameProgress.LastLevelScore = LevelScore;
	GameProgress.LastBonusScore = _bonusScore;
	GameProgress.LastTotalScore = GetTotalWithBonus();
	GameProgress.LastHighScore = LoadHighScoreForLevel(Level);
	GameProgress.Save();

	_isRunning = false;

	GD.Print($"[ScoreManager] 🎉 Level {Level} cleared!");
	EmitSignal(SignalName.LevelCleared, LevelScore, Level);
}

	private void OnGameOver()
	{
		if (IsGameOver || IsLevelCleared) return;
		IsGameOver = true;
		GameProgress.IsLevelCleared = false;  // ❌ แพ้ = ไม่ผ่าน
		_isRunning = false;

		GD.Print($"[ScoreManager] 💀 Game Over at Level {Level}");
		EmitSignal(SignalName.GameOver, LevelScore, Level);
	}

	public void SetRunning(bool run)
	{
		_isRunning = run && !IsGameOver && !IsLevelCleared;
	}

	public void GoToNextLevel() => StartLevel(Level + 1);

	// ===== SAVE / LOAD =====
	private void LoadHighScore()
	{
		if (!FileAccess.FileExists(SAVE_FILE))
		{
			_highScore = 0;
			return;
		}
		using var f = FileAccess.Open(SAVE_FILE, FileAccess.ModeFlags.Read);
		if (f == null) { _highScore = 0; return; }
		_highScore = (int)f.Get32();
	}

	private void SaveHighScore()
	{
		using var f = FileAccess.Open(SAVE_FILE, FileAccess.ModeFlags.Write);
		f.Store32((uint)_highScore);
	}

	// ===== GETTERS =====
	public float GetTimeLeft() => TimeLeftSec;
	public int GetBonusScore() => _bonusScore;
	public int GetTotalWithBonus() => TotalScore + _bonusScore;
	public int LoadHighScoreForLevel(int _level) => _highScore;

	public void SyncRequestFromHud()
	{
		EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);
		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, _highScore);
		EmitSignal(SignalName.LivesChanged, _lives);
		EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft);
		EmitSignal(SignalName.LevelChanged, Level);
	}

	// ===== MULTIPLIER =====
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

	private CrystalType[] MapColors(CrystalType[] src)
{
		if (src == null || src.Length == 0) return Array.Empty<CrystalType>();
		var dst = new CrystalType[src.Length];
		for (int i = 0; i < src.Length; i++)
	{
			// แปลงโดยอาศัยชื่อ enum ให้เหมือนกัน (Purple, Blue, Green, Yellow, Red, Pink)
			dst[i] = (CrystalType)Enum.Parse(typeof(CrystalType), src[i].ToString());
	}
		return dst;
	}
}
