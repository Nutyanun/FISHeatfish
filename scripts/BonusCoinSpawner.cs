using Godot;
using System.Collections.Generic;

public partial class BonusCoinSpawner : Node2D
{
	// ===== Signals =====
	// สัญญาณที่ส่งออกจากคลาสนี้ไปยัง Node อื่นเมื่อเกิดเหตุการณ์สำคัญ
	[Signal] public delegate void BonusStartedEventHandler();               // เมื่อเริ่มเฟสโบนัส
	[Signal] public delegate void BonusEndedEventHandler(int totalBonus);   // เมื่อจบเฟสโบนัส พร้อมส่งคะแนนรวม
	[Signal] public delegate void BonusTickEventHandler(int value, int total); // เมื่อเก็บเหรียญแต่ละครั้ง

	// ===== Scenes =====
	// PackedScene คือ Scene ของเหรียญแต่ละชนิดที่สามารถ Instatiate ได้
	[Export] public PackedScene CoinScene { get; set; }     // ซีนเหรียญทั่วไป (ใช้สำรอง)
	[Export] public PackedScene BronzeScene { get; set; }   // ซีนเหรียญทองแดง
	[Export] public PackedScene SilverScene { get; set; }   // ซีนเหรียญเงิน
	[Export] public PackedScene GoldScene { get; set; }     // ซีนเหรียญทอง

	// ===== Tuning =====
	// ค่ากำหนดพฤติกรรมของระบบโบนัส
	[Export] public float SpawnEverySeconds { get; set; } = 0.15f; // ระยะเวลาระหว่างการเกิดเหรียญ
	[Export] public int   MaxAlive         { get; set; } = 40;     // จำนวนเหรียญที่อยู่บนจอพร้อมกันสูงสุด
	[Export] public float BonusDuration    { get; set; } = 5.0f;   // ระยะเวลาของช่วงโบนัส

	// น้ำหนักของการสุ่มเหรียญแต่ละชนิด (ยิ่งมากยิ่งออกบ่อย)
	[Export] public float BronzeWeight { get; set; } = 0.6f;
	[Export] public float SilverWeight { get; set; } = 0.4f;
	[Export] public float GoldWeight   { get; set; } = 0.2f;

	// พื้นที่การเกิดเหรียญบนหน้าจอ
	[Export] public float SpawnXMargin { get; set; } = 24f;  // ระยะห่างจากขอบซ้าย/ขวา
	[Export] public float SpawnYOffset { get; set; } = -40f; // ตำแหน่งเหนือขอบบนเล็กน้อย

	// ===== Runtime =====
	private Timer _spawnTimer;  // ตัวจับเวลาในการ spawn เหรียญ
	private Timer _phaseTimer;  // ตัวจับเวลาในการจบช่วงโบนัส

	private bool _running;      // สถานะว่าขณะนี้อยู่ในช่วงโบนัสหรือไม่
	public bool IsRunning => _running; // getter สำหรับเช็กสถานะจากภายนอก

	private readonly HashSet<Coin> _alive = new(); // เก็บรายการเหรียญที่ยังไม่หายไปจากจอ
	private int _totalBonus;                       // คะแนนโบนัสที่เก็บได้รวมทั้งหมด

	public override void _Ready()
	{
		// สร้างและตั้งค่า Timer สำหรับการ spawn เหรียญ
		_spawnTimer = new Timer { OneShot = false, WaitTime = SpawnEverySeconds };
		AddChild(_spawnTimer);
		_spawnTimer.Timeout += OnSpawnTick; // เมื่อครบเวลา → เรียกฟังก์ชัน OnSpawnTick

		// สร้าง Timer สำหรับกำหนดระยะเวลาของช่วงโบนัส
		_phaseTimer = new Timer { OneShot = true, WaitTime = BonusDuration };
		AddChild(_phaseTimer);
		_phaseTimer.Timeout += OnPhaseTimeout; // เมื่อหมดเวลา → เรียก OnPhaseTimeout
	}

	// ฟังก์ชันให้ ScoreManager ปรับค่าการทำงานได้ในแต่ละด่าน
	public void ApplyLevelTuning(float? spawnEvery = null, int? maxAlive = null, float? duration = null)
	{
		// ถ้ามีค่าที่ส่งเข้ามา → ใช้แทนค่าปัจจุบัน
		if (spawnEvery.HasValue) SpawnEverySeconds = spawnEvery.Value;
		if (maxAlive.HasValue)   MaxAlive         = maxAlive.Value;
		if (duration.HasValue)   BonusDuration    = duration.Value;

		// ปรับค่าใน Timer ให้ตรงกับที่แก้ไข
		if (_spawnTimer != null) _spawnTimer.WaitTime = SpawnEverySeconds;
		if (_phaseTimer != null) _phaseTimer.WaitTime = BonusDuration;
	}

	// เริ่มช่วงโบนัส (durationOverride = ระยะเวลาชั่วคราว ถ้าไม่กำหนดจะใช้ค่าเดิม)
	public void Start(float? durationOverride = null)
	{
		// ถ้ามีเฟสก่อนหน้านี้ที่ยังไม่จบ → ล้างออกก่อน
		if (_running)
			ForceStopAndClear();

		// ตั้งระยะเวลาใหม่ (ถ้ามี override)
		_phaseTimer.WaitTime = durationOverride ?? BonusDuration;

		_totalBonus = 0;       // รีเซ็ตคะแนนโบนัส
		_running = true;       // ตั้งสถานะว่าเริ่มแล้ว

		// เริ่มจับเวลา spawn และระยะเวลาโบนัส
		_spawnTimer.WaitTime = SpawnEverySeconds;
		_spawnTimer.Start();
		_phaseTimer.Start();

		// ส่งสัญญาณว่าเริ่มช่วงโบนัสแล้ว
		EmitSignal(SignalName.BonusStarted);
	}

	// หยุดช่วงโบนัสและล้างเหรียญทั้งหมดทันที
	public void ForceStopAndClear()
	{
		if (!_running && _alive.Count == 0) return; // ถ้าไม่มีอะไรให้ล้าง → ออกเลย

		_running = false;
		_spawnTimer.Stop();
		_phaseTimer.Stop();
		CleanupAll(); // ลบเหรียญทั้งหมดที่ยังอยู่ในจอ
	}

	// หยุดทันทีและส่งสัญญาณว่าจบโบนัส
	public void StopNow()
	{
		if (!_running) return;
		ForceStopAndClear();
		EmitSignal(SignalName.BonusEnded, _totalBonus);
	}

	// เรียกเมื่อหมดเวลาโบนัส
	private void OnPhaseTimeout()
	{
		_running = false;
		_spawnTimer.Stop();
		CleanupAll(); // ล้างเหรียญทั้งหมด
		EmitSignal(SignalName.BonusEnded, _totalBonus);
	}

	// ฟังก์ชันล้างเหรียญทั้งหมดที่เหลืออยู่
	private void CleanupAll()
	{
		foreach (var c in _alive)
		{
			if (IsInstanceValid(c))
				c.QueueFree(); // สั่งให้ node ถูกลบออกจาก scene
		}
		_alive.Clear();
	}

	// เรียกทุกครั้งที่ถึงเวลา spawn เหรียญใหม่
	private void OnSpawnTick()
	{
		if (!_running) return;                     // ถ้ายังไม่ได้เริ่มโบนัส → ออกเลย
		if (_alive.Count >= MaxAlive) return;      // ถ้ามีเหรียญในจอเต็มแล้ว → ไม่ spawn เพิ่ม

		// สุ่มชนิดเหรียญที่จะ spawn
		var type = RandomCoinType();
		var coin = InstantiateCoinForType(type);
		if (coin == null) return; // ถ้า spawn ไม่สำเร็จ → ออกเลย

		// ดึงขนาดหน้าจอเพื่อสุ่มตำแหน่งเกิด
		Rect2 vis = GetViewport().GetVisibleRect();

		// สุ่มตำแหน่ง X ภายในขอบจอ
		float xMin = vis.Position.X + SpawnXMargin;
		float xMax = vis.End.X       - SpawnXMargin;
		float rx   = (float)GD.RandRange(xMin, xMax);

		// กำหนด Y ให้เหนือขอบบนเล็กน้อย
		float ry = vis.Position.Y + SpawnYOffset;

		// ตั้งตำแหน่งและเลเยอร์ของเหรียญ
		coin.GlobalPosition = new Vector2(rx, ry);
		coin.ZIndex = 100;

		// ผูกอีเวนต์เมื่อเหรียญถูกเก็บหรือหายไป
		coin.Eaten       += OnCoinEaten;
		coin.Despawned   += OnCoinDespawned;
		coin.TreeExiting += () => { _alive.Remove(coin); };

		AddChild(coin);  // เพิ่มเหรียญเข้า scene
		_alive.Add(coin); // บันทึกไว้ในลิสต์เหรียญที่มีอยู่
	}

	// สร้างเหรียญตามชนิดที่สุ่มได้
	private Coin InstantiateCoinForType(Coin.CoinType type)
	{
		// เลือก scene ของเหรียญตามประเภท
		PackedScene scene = type switch
		{
			Coin.CoinType.Bronze => BronzeScene ?? CoinScene,
			Coin.CoinType.Silver => SilverScene ?? CoinScene,
			Coin.CoinType.Gold   => GoldScene   ?? CoinScene,
			_ => CoinScene
		};

		// ถ้ายังไม่มี scene ที่กำหนด → แจ้ง error และออก
		if (scene == null)
		{
			GD.PushError("BonusCoinSpawner: No coin scene assigned (set CoinScene or Bronze/Silver/Gold Scene).");
			return null;
		}

		// สร้าง instance จาก scene ที่เลือก
		var node = scene.Instantiate();

		// ตรวจว่ามี script Coin ติดอยู่หรือไม่
		var coin = node as Coin ?? node.GetNodeOrNull<Coin>(".");
		if (coin == null)
		{
			GD.PushError("BonusCoinSpawner: The assigned scene does not have a Coin script on the root.");
			node.QueueFree();
			return null;
		}

		coin.Type = type; // กำหนดชนิดให้เหรียญ
		return coin;
	}

	// สุ่มประเภทเหรียญตามน้ำหนักที่ตั้งไว้
	private Coin.CoinType RandomCoinType()
	{
		float sum = BronzeWeight + SilverWeight + GoldWeight;
		if (sum <= 0f) sum = 1f; // กัน divide by zero

		float r = (float)GD.RandRange(0.0, (double)sum);

		if (r < BronzeWeight) return Coin.CoinType.Bronze;
		r -= BronzeWeight;
		if (r < SilverWeight) return Coin.CoinType.Silver;
		return Coin.CoinType.Gold;
	}

	// เรียกเมื่อเหรียญถูกเก็บ
	private void OnCoinEaten(int value)
	{
		_totalBonus += value; // เพิ่มคะแนนโบนัสรวม
		EmitSignal(SignalName.BonusTick, value, _totalBonus); // แจ้ง HUD ให้แสดงผล

		// อัปเดตไปยัง ScoreManager เพื่อเพิ่มคะแนนในเกม
		var sm = ScoreManager.Instance;
		sm?.AddBonusScore(value);
	}

	// เรียกเมื่อเหรียญหายไปเอง (ยังไม่ต้องทำอะไร)
	private void OnCoinDespawned() { }

	// คืนค่าคะแนนโบนัสรวมทั้งหมด
	public int GetTotalBonus() => _totalBonus;
}
