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
	[Signal] public delegate void LevelClearedEventHandler(int finalScore, int level);   // ใช้ตอน "เวลาหมดและถึงเป้า"
	[Signal] public delegate void GameOverEventHandler(int finalScore, int level);       // ใช้ตอน "ชีวิตหมด" หรือ "เวลาหมดแต่ไม่ถึงเป้า"

	// ===== Config ด่าน =====
	[Export] public int  BaseTargetScore { get; set; } = 300;   // เป้าด่าน 1
	[Export] public int  BaseTimeSeconds { get; set; } = 240;   // เวลาเริ่มด่าน 1 (4 นาที)
	[Export] public int  TimeIncPerLevel { get; set; } = 120;   // +2 นาทีทุกด่าน
	[Export] public bool AutoAdvanceOnTimeUp { get; set; } = false; // ผ่านด่านแล้วให้ไปด่านถัดไปทันที

	// ===== ค่าเริ่มต้นชีวิตต่อเลเวล =====
	[Export] public int StartingLives { get; set; } = 3; // [NEW] ชีวิตเริ่มต้นของทุกเลเวล

	// ===== สถานะหลัก =====
	[Export] public int Level { get; set; } = 1;   // เริ่มด่าน (default = 1)
	[Export] public int Lives { get; set; } = 3;

	public int TargetScore { get; private set; } = 300; // อัปเดตตามด่าน
	public int LevelScore  { get; private set; } = 0;   // คะแนนของด่านปัจจุบัน
	public int TotalScore  { get; private set; } = 0;   // สะสมตลอดรอบ
	public int HighScore   { get; private set; } = 0;   // สถิติเก็บไฟล์

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

	// โหลดเลเวลจาก GameProgress ถ้ามีค่า
	if (GameProgress.CurrentPlayingLevel > 0)
		Level = GameProgress.CurrentPlayingLevel;

	StartLevel(Level);
	}


	private void StartLevel(int lvl)
	{
		Level = Mathf.Max(1, lvl);

		// รีเซ็ตชีวิตทุกครั้งที่เริ่มเลเวลใหม่ (แนวทางที่ 1)
		Lives = StartingLives; // [NEW]

		TargetScore     = TargetForLevel(Level);
		LevelScore      = 0;
		IsLevelCleared  = false;
		IsGameOver      = false;

		Mult           = Mathf.Clamp(MultMin, MultMin, MultMax);
		_fishInWindow  = 0;
		_windowLeft    = WindowSeconds;

		_timeLeft = TimeForLevel(Level);

		EmitSignal(SignalName.LevelChanged, Level);
		EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);
		EmitSignal(SignalName.TotalScoreChanged, TotalScore, HighScore);
		EmitSignal(SignalName.LivesChanged, Lives); // [CHANGED] เรียกหลังรีเซ็ตชีวิต
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

		// Time countdown per level
		_timeLeft = Mathf.Max(0, _timeLeft - (float)delta);
		EmitSignal(SignalName.TimeLeftChanged, _timeLeft);

		if (_timeLeft <= 0f && !IsLevelCleared)
		{
			// เวลาหมด: เช็กว่าถึงเป้าหรือไม่
			if (LevelScore >= TargetScore)
			{
				// ผ่านด่าน
				FinishCurrentLevel();      // emit LevelCleared
				if (AutoAdvanceOnTimeUp)   // ไปด่านถัดไปทันที
					AdvanceToNextLevel();
			}
			else
			{
				// ไม่ถึงเป้า → แพ้
				FailByTimeUp();            // emit GameOver
			}
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

		// ถึงเป้าก่อนเวลาหมด → แค่แจ้ง/ใส่เอฟเฟกต์ แต่ยังเล่นต่อ
		if (LevelScore >= TargetScore)
			GD.Print($"[ScoreManager] Target reached ({LevelScore}/{TargetScore}). Continue until time runs out.");
	}

	public void LoseLife(int amount = 1)
	{
		if (IsGameOver) return;

		Lives = Mathf.Max(0, Lives - amount);
		EmitSignal(SignalName.LivesChanged, Lives);

		if (Lives == 0)
			DoGameOver();
	}

	// ---------- End-of-level / GameOver ----------
	private void FinishCurrentLevel()
	{
		if (IsLevelCleared) return;

		IsLevelCleared = true;
		SaveHighScore();

		GD.Print($"[ScoreManager] Level {Level} cleared at time-up. Score={{LevelScore}} (Target={{TargetScore}})");
		EmitSignal(SignalName.LevelCleared, LevelScore, Level);
	}

	private void AdvanceToNextLevel()
	{
		int nextLevel = Level + 1;
		StartLevel(nextLevel); // เป้าคูณสอง เวลา +2 นาที และรีเซ็ตชีวิตตาม StartingLives
	}

	private void FailByTimeUp()
	{
		// เวลาหมดแต่ไม่ถึงเป้า => แพ้ด่าน
		IsGameOver = true;
		SaveHighScore();

		GD.Print($"[ScoreManager] Time up but target not met. Score={LevelScore}/{TargetScore} → GAME OVER");
		EmitSignal(SignalName.GameOver, LevelScore, Level);

		// ถ้าต้องหยุดทันทีให้ pause (HUD/ฉากอื่นสามารถเปลี่ยนฉากต่อ)
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

	// ---------- External control (ถ้าต้องเรียกเอง) ----------
	public void ResetForNewLevel(int newLevel, int newTargetIgnored, int lives)
	{
		// ฟังก์ชันนี้ยังคงไว้เพื่อความเข้ากันได้เดิม
		// แต่ตรรกะหลักย้ายไปใน StartLevel() แล้ว
		// หากถูกเรียกจากโค้ดเก่า จะเคารพค่าชีวิตที่ส่งมา
		Lives = lives; // [CHANGED] ให้ค่านี้ถูกแทนก่อน จากนั้น StartLevel จะรีเซ็ตเป็น StartingLives อีกครั้งถ้าต้องการ
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
