using Godot;
using System.Collections.Generic;

public partial class BonusCoinSpawner : Node2D
{
	[Signal] public delegate void BonusStartedEventHandler();
	[Signal] public delegate void BonusEndedEventHandler(int totalBonus);

	// ซีนสำรอง (ใช้เมื่อไม่ได้ตั้งทั้ง 3 แบบ)
	[Export] public PackedScene CoinScene { get; set; }

	// ซีนแยกตามชนิด
	[Export] public PackedScene BronzeScene { get; set; }
	[Export] public PackedScene SilverScene { get; set; }
	[Export] public PackedScene GoldScene   { get; set; }

	[Export] public float SpawnEverySeconds { get; set; } = 0.15f;
	[Export] public int   MaxAlive         { get; set; } = 40;
	[Export] public float BonusDuration    { get; set; } = 5.0f;

	// อัตราสุ่มชนิด (รวมกันเท่าไหร่ก็ได้)
	[Export] public float BronzeWeight { get; set; } = 0.6f;
	[Export] public float SilverWeight { get; set; } = 0.4f;
	[Export] public float GoldWeight   { get; set; } = 0.2f;

	// ขอบซ้าย/ขวาที่เว้นไว้ และ offset ให้เกิดเหนือขอบบนเล็กน้อย (ค่าติดลบ)
	[Export] public float SpawnXMargin { get; set; } = 24f;
	[Export] public float SpawnYOffset { get; set; } = -40f;

	private Timer _spawnTimer;
	private Timer _phaseTimer;

	private bool _running;
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

	public void Start(float? durationOverride = null)
	{
		if (durationOverride.HasValue)
			_phaseTimer.WaitTime = durationOverride.Value;

		_totalBonus = 0;
		_running = true;

		_spawnTimer.WaitTime = SpawnEverySeconds;
		_spawnTimer.Start();
		_phaseTimer.Start();

		EmitSignal(SignalName.BonusStarted);
	}

	public void StopNow()
	{
		_running = false;
		_spawnTimer.Stop();
		_phaseTimer.Stop();
		CleanupAll();
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
			if (IsInstanceValid(c)) c.QueueFree();
		_alive.Clear();
	}

	private void OnSpawnTick()
	{
		if (!_running) return;
		if (_alive.Count >= MaxAlive) return;

		var type = RandomCoinType();
		var coin = InstantiateCoinForType(type);
		if (coin == null) return;

		// ใช้ "พื้นที่จอที่มองเห็นจริง" (รองรับกล้อง/พารัลแลกซ์)
		Rect2 vis = GetViewport().GetVisibleRect();

		float xMin = vis.Position.X + SpawnXMargin;
		float xMax = vis.End.X       - SpawnXMargin;
		float x = (float)GD.RandRange(xMin, xMax);

		// เกิดเหนือขอบบนเล็กน้อย แล้วตกลงมา
		float y = vis.Position.Y + SpawnYOffset;

		coin.GlobalPosition = new Vector2(x, y);
		coin.ZIndex = 100; // ให้ทับฉากพื้นหลัง

		coin.Eaten       += OnCoinEaten;
		coin.Despawned   += OnCoinDespawned;
		coin.TreeExiting += () => { _alive.Remove(coin); };

		AddChild(coin);
		_alive.Add(coin);

		// ดีบัก (ถ้าอยากดู)
		// GD.Print($"[Spawner] spawned {coin.Type} at {coin.GlobalPosition}, alive={_alive.Count}");
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
		var coin = node as Coin;

		// เผื่อรากไม่ใช่ประเภท Coin ตรง ๆ
		if (coin == null)
		{
			coin = node.GetNodeOrNull<Coin>(".");
			if (coin == null)
			{
				GD.PushError("BonusCoinSpawner: The assigned scene does not have a Coin script on the root.");
				node.QueueFree();
				return null;
			}
		}

		coin.Type = type; // บังคับชนิดตามที่สุ่มได้
		return coin;
	}

	private Coin.CoinType RandomCoinType()
	{
		float sum = BronzeWeight + SilverWeight + GoldWeight;
		if (sum <= 0f) sum = 1f;

		float r = (float)GD.RandRange(0.0, sum);

		if (r < BronzeWeight) return Coin.CoinType.Bronze;
		r -= BronzeWeight;
		if (r < SilverWeight) return Coin.CoinType.Silver;
		return Coin.CoinType.Gold;
	}

	private void OnCoinEaten(int value)
	{
		_totalBonus += value;
		// GD.Print($"[Spawner] eaten +{value}, total={_totalBonus}");
	}

	private void OnCoinDespawned() { /* noop */ }

	public int GetTotalBonus() => _totalBonus;
}
