using Godot;

public partial class ScoreManager : Node
{
	[Signal] public delegate void ScoreChangedEventHandler(int current, int target);
	[Signal] public delegate void LivesChangedEventHandler(int lives);
	[Signal] public delegate void LevelChangedEventHandler(int level);
	[Signal] public delegate void GameOverEventHandler();

	[Export] public int TargetScore { get; set; } = 50;
	[Export] public int Lives      { get; private set; } = 3;
	[Export] public int Level      { get; private set; } = 1;

	[Export] public int  Score      { get; private set; } = 0;
	[Export] public bool IsGameOver { get; private set; } = false; // เผื่อโค้ดอื่นเช็คค่านี้

	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.Always;

		EmitSignal(SignalName.ScoreChanged, Score, TargetScore);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.LevelChanged, Level);

		GD.Print("[SM] ready S/Lv/Sc sent");
	}

	public void AddScore(int points)
	{
		if (IsGameOver) return;
		Score += points;
		EmitSignal(SignalName.ScoreChanged, Score, TargetScore);
		GD.Print($"[SM] AddScore +{points} -> {Score}");
	}

	public void PlayerDied()
	{
		if (IsGameOver) return;

		Lives -= 1;
		EmitSignal(SignalName.LivesChanged, Lives);
		GD.Print($"[SM] PlayerDied -> lives {Lives}");

		if (Lives <= 0)
		{
			IsGameOver = true;
			GD.Print("[SM] GAME OVER emit");
			EmitSignal(SignalName.GameOver);
		}
	}

	public void ResetRun(int startLives = 3, int targetScore = 50, int level = 1)
	{
		Score = 0;
		Lives = startLives;
		TargetScore = targetScore;
		Level = level;
		IsGameOver = false;

		EmitSignal(SignalName.ScoreChanged, Score, TargetScore);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.LevelChanged, Level);
	}

	// alias รองรับโค้ดที่เรียกชื่อเก่า
	public void Add(int points) => AddScore(points);
	public void KillPlayer()    => PlayerDied();
}
