using Godot;
using System.Collections.Generic;

public partial class BonusCoinSpawner : Node2D
{
	// ===== Signals =====
	[Signal] public delegate void BonusStartedEventHandler();
	[Signal] public delegate void BonusEndedEventHandler(int totalBonus);
	[Signal] public delegate void BonusTickEventHandler(int value, int total);

	// ===== Scenes =====
	[Export] public PackedScene CoinScene { get; set; }
	[Export] public PackedScene BronzeScene { get; set; }
	[Export] public PackedScene SilverScene { get; set; }
	[Export] public PackedScene GoldScene   { get; set; }

	// ===== Tuning =====
	[Export] public float SpawnEverySeconds { get; set; } = 0.15f;
	[Export] public int   MaxAlive         { get; set; } = 40;
	[Export] public float BonusDuration    { get; set; } = 5.0f;

	// Weight (sum ไม่จำเป็นต้อง = 1)
	[Export] public float BronzeWeight { get; set; } = 0.6f;
	[Export] public float SilverWeight { get; set; } = 0.4f;
	[Export] public float GoldWeight   { get; set; } = 0.2f;

	// Spawn bounds
	[Export] public float SpawnXMargin { get; set; } = 24f;
	[Export] public float SpawnYOffset { get; set; } = -40f;

	// ===== Runtime =====
	private Timer _spawnTimer;
	private Timer _phaseTimer;

	private bool _running;
	public  bool IsRunning => _running;

	private readonly HashSet<Coin> _alive = new();
	private int _totalBonus;

	public override void _Ready()
	{
		_spawnTimer = new Timer { OneShot = false, WaitTime = SpawnEverySeconds };
		AddChild(_spawnTimer);
		_spawnTimer.Timeout += OnSpawnTick;

		_phaseTimer = new Timer { OneShot = true, WaitTime = BonusDuration };
		AddChild(_phaseTimer);
		_phaseTimer.Timeout += OnPhaseTimeout;
	}

	// === API ใหม่: ให้ ScoreManager ปรับ tuning ต่อด่านได้ ===
	public void ApplyLevelTuning(float? spawnEvery = null, int? maxAlive = null, float? duration = null)
	{
		if (spawnEvery.HasValue) SpawnEverySeconds = spawnEvery.Value;
		if (maxAlive.HasValue)   MaxAlive         = maxAlive.Value;
		if (duration.HasValue)   BonusDuration    = duration.Value;

		if (_spawnTimer != null) _spawnTimer.WaitTime = SpawnEverySeconds;
		if (_phaseTimer != null) _phaseTimer.WaitTime = BonusDuration;
	}

	/// <summary>เริ่มเฟสโบนัส (durationOverride เป็นวินาที ถ้า null จะใช้ BonusDuration)</summary>
	public void Start(float? durationOverride = null)
	{
		if (_running)
			ForceStopAndClear();

		_phaseTimer.WaitTime = durationOverride ?? BonusDuration;

		_totalBonus = 0;
		_running = true;

		_spawnTimer.WaitTime = SpawnEverySeconds;
		_spawnTimer.Start();
		_phaseTimer.Start();

		EmitSignal(SignalName.BonusStarted);
	}

	/// <summary>หยุดทันทีและเคลียร์เหรียญทั้งหมด</summary>
	public void ForceStopAndClear()
	{
		if (!_running && _alive.Count == 0) return;

		_running = false;
		_spawnTimer.Stop();
		_phaseTimer.Stop();
		CleanupAll();
	}

	public void StopNow()
	{
		if (!_running) return;
		ForceStopAndClear();
		EmitSignal(SignalName.BonusEnded, _totalBonus);
	}

	private void OnPhaseTimeout()
	{
		_running = false;
		_spawnTimer.Stop();
		CleanupAll();
		EmitSignal(SignalName.BonusEnded, _totalBonus);
	}

	private void CleanupAll()
	{
		foreach (var c in _alive)
		{
			if (IsInstanceValid(c))
				c.QueueFree();
		}
		_alive.Clear();
	}

	private void OnSpawnTick()
	{
		if (!_running) return;
		if (_alive.Count >= MaxAlive) return;

		var type = RandomCoinType();
		var coin = InstantiateCoinForType(type);
		if (coin == null) return;

		Rect2 vis = GetViewport().GetVisibleRect();

		float xMin = vis.Position.X + SpawnXMargin;
		float xMax = vis.End.X       - SpawnXMargin;
		float rx   = (float)GD.RandRange(xMin, xMax);

		float ry = vis.Position.Y + SpawnYOffset; // เกิดเหนือขอบบนเล็กน้อย

		coin.GlobalPosition = new Vector2(rx, ry);
		coin.ZIndex = 100;

		coin.Eaten       += OnCoinEaten;
		coin.Despawned   += OnCoinDespawned;
		coin.TreeExiting += () => { _alive.Remove(coin); };

		AddChild(coin);
		_alive.Add(coin);
	}

	private Coin InstantiateCoinForType(Coin.CoinType type)
	{
		PackedScene scene = type switch
		{
			Coin.CoinType.Bronze => BronzeScene ?? CoinScene,
			Coin.CoinType.Silver => SilverScene ?? CoinScene,
			Coin.CoinType.Gold   => GoldScene   ?? CoinScene,
			_ => CoinScene
		};

		if (scene == null)
		{
			GD.PushError("BonusCoinSpawner: No coin scene assigned (set CoinScene or Bronze/Silver/Gold Scene).");
			return null;
		}

		var node = scene.Instantiate();

		var coin = node as Coin ?? node.GetNodeOrNull<Coin>(".");
		if (coin == null)
		{
			GD.PushError("BonusCoinSpawner: The assigned scene does not have a Coin script on the root.");
			node.QueueFree();
			return null;
		}

		coin.Type = type;
		return coin;
	}

	private Coin.CoinType RandomCoinType()
	{
		float sum = BronzeWeight + SilverWeight + GoldWeight;
		if (sum <= 0f) sum = 1f;

		float r = (float)GD.RandRange(0.0, (double)sum);

		if (r < BronzeWeight) return Coin.CoinType.Bronze;
		r -= BronzeWeight;
		if (r < SilverWeight) return Coin.CoinType.Silver;
		return Coin.CoinType.Gold;
	}

	private void OnCoinEaten(int value)
	{
		_totalBonus += value;
		EmitSignal(SignalName.BonusTick, value, _totalBonus);
	}

	private void OnCoinDespawned() { /* no-op */ }

	public int GetTotalBonus() => _totalBonus;
}
