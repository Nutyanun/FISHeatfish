using Godot;
using System;
using System.Collections.Generic;

// คลาส CrystalSpawner ใช้ควบคุมการสุ่มเกิดและการตกของคริสตัลในเกม
public partial class CrystalSpawner : Node2D
{
	// ===================== EXPORT VARIABLES =====================

	[Export] public NodePath SpawnRootPath { get; set; } = "..";  // path ไปยัง node ที่ใช้เป็นรากสำหรับ spawn คริสตัล
	[Export] public float IntervalSec { get; set; } = 60f;        // ระยะเวลาห่างระหว่างการ spawn (วินาที)
	[Export] public bool UseRandomInterval { get; set; } = false; // ถ้า true → จะสุ่มเวลา spawn ให้ไม่เท่ากันทุกครั้ง
	[Export] public float RandomJitter { get; set; } = 0.25f;     // ค่าความแปรผันของเวลาสุ่ม spawn (เช่น 25%)
	[Export] public int MaxOnScreen { get; set; } = 5;            // จำนวนคริสตัลสูงสุดที่อยู่บนหน้าจอได้พร้อมกัน
	[Export] public float FallSpeed { get; set; } = 70f;          // ความเร็วการตกของคริสตัล
	[Export] public float MinVisibleSec { get; set; } = 2.5f;     // เวลาขั้นต่ำที่คริสตัลต้องอยู่ก่อนจะหายไป

	[Export] public bool SpawnImmediatelyOnStart { get; set; } = true; // ให้ spawn ทันทีตอนเริ่มเกมหรือไม่
	[Export] public bool LogDebug { get; set; } = true;                // เปิด/ปิดข้อความ debug ใน Output

	// Scene ของคริสตัลแต่ละสี (ลากใส่ใน Inspector)
	[Export] public PackedScene PurpleScene { get; set; }
	[Export] public PackedScene BlueScene   { get; set; }
	[Export] public PackedScene GreenScene  { get; set; }
	[Export] public PackedScene RedScene    { get; set; }
	[Export] public PackedScene PinkScene   { get; set; }

	// ===================== PRIVATE VARIABLES =====================

	private Node2D _spawnRoot;                    // node ที่ใช้เป็นรากสำหรับ spawn คริสตัล
	private float _timer;                         // ตัวนับเวลาสำหรับ spawn
	private float _nextInterval;                  // เวลาระหว่างการ spawn รอบต่อไป
	private readonly HashSet<CrystalType> _allowed = new();        // สีที่อนุญาตให้ spawn
	private readonly List<CrystalType> _allowedWithScene = new();  // สีที่มี scene อยู่จริง
	private bool _finalPinkSpawned = false;       // เช็กว่าปล่อยคริสตัลชมพูสุดท้ายแล้วหรือยัง

	// ===================== APPLY RULE =====================

	// ฟังก์ชัน ApplyRule(): ใช้เปลี่ยนการตั้งค่า spawn ตามเงื่อนไข เช่น แต่ละเลเวล
	public void ApplyRule(CrystalType[] colors, float intervalSec, int maxOnScreen)
	{
		_allowed.Clear(); // ล้างสีที่อนุญาตทั้งหมดก่อน
		if (colors != null && colors.Length > 0) // ถ้ามีสีที่ส่งเข้ามา
			_allowed.UnionWith(colors); // รวมเข้าชุดที่อนุญาต

		IntervalSec = Math.Max(0.1f, intervalSec); // ป้องกันไม่ให้เวลาน้อยเกินไป
		MaxOnScreen = Math.Max(0, maxOnScreen);    // ป้องกันค่าติดลบ

		RefreshAllowedWithScene(); // อัปเดตรายชื่อสีที่มี scene อยู่จริง
		ResetPinkForced();         // รีเซ็ตสถานะชมพูบังคับ

		_timer = 0f;                        // รีเซ็ตตัวจับเวลา
		_nextInterval = IntervalWithJitter(); // ตั้งเวลาสุ่มรอบแรก

		if (LogDebug)
			GD.Print($"[CrystalSpawner] ApplyRule OK colors={_allowedWithScene.Count} interval={IntervalSec}s max={MaxOnScreen}");
	}

	// รีเซ็ตธงว่ามีการ spawn ชมพูบังคับแล้วหรือยัง
	public void ResetPinkForced() => _finalPinkSpawned = false;

	// ฟังก์ชัน ForcePinkOnceInLastWindow(): spawn คริสตัลชมพูบังคับ 1 ครั้ง (ใช้ใน 20 วิสุดท้าย)
	public void ForcePinkOnceInLastWindow()
	{
		if (_finalPinkSpawned || PinkScene == null) return; // ถ้ามีแล้วหรือไม่มี scene → ข้าม
		if (!PinkExistsOnScreen())                          // ถ้าในจอยังไม่มีชมพู
			SpawnSpecific(PinkScene, force: true);          // สร้างทันที
		_finalPinkSpawned = true;                           // ตั้งค่าว่าทำแล้ว
		if (LogDebug) GD.Print("[CrystalSpawner] Force pink in last 20s.");
	}

	// ===================== READY =====================

	public override void _Ready()
	{
		// กำหนด node รากสำหรับ spawn: ถ้าไม่เจอ → ใช้ parent → ถ้าไม่มี → ใช้ตัวเอง
		_spawnRoot = GetNodeOrNull<Node2D>(SpawnRootPath) ?? GetParent() as Node2D ?? this;

		_timer = 0f;                      // ตั้งค่าเริ่มต้นเป็นศูนย์
		_nextInterval = IntervalWithJitter(); // กำหนดเวลารอบแรกแบบสุ่มได้

		// ถ้าเลือกให้ spawn ทันทีตอนเริ่มเกม
		if (SpawnImmediatelyOnStart && _allowedWithScene.Count > 0)
			TrySpawnOnceNow(); // เรียกฟังก์ชัน spawn ครั้งแรก
	}

	// ===================== PROCESS =====================

	public override void _Process(double delta)
	{
		// ตรวจว่า node ยังอยู่ใน scene tree และยัง valid หรือไม่
		if (!IsInsideTree()) return;
		if (_spawnRoot == null) return;
		if (!GodotObject.IsInstanceValid(_spawnRoot)) return;
		if (_spawnRoot.IsQueuedForDeletion() || !_spawnRoot.IsInsideTree()) return;

		// ถ้าไม่มีสีที่อนุญาต → ข้าม
		if (_allowedWithScene.Count == 0) return;

		_timer += (float)delta; // เพิ่มเวลา
		if (_timer < _nextInterval) return; // ยังไม่ถึงเวลา spawn

		_timer -= _nextInterval;            // รีเซ็ตเวลา spawn
		_nextInterval = IntervalWithJitter(); // ตั้งเวลารอบใหม่

		int onScreen = CountCrystalsOnScreen(); // นับจำนวนที่อยู่บนจอ
		int room = (MaxOnScreen <= 0) ? 0 : Math.Max(0, MaxOnScreen - onScreen); // คำนวณช่องว่างที่เหลือ
		if (room <= 0) return; // ถ้าเต็ม → ไม่ spawn เพิ่ม

		int toSpawn = 1 + (int)(GD.Randi() % (uint)room); // สุ่มจำนวนที่จะ spawn

		// วนสร้างคริสตัลตามจำนวนที่สุ่มได้
		for (int i = 0; i < toSpawn; i++)
		{
			var color = PickAllowedColorWithScene(); // สุ่มเลือกสีที่มี scene
			var scene = SceneOf(color);              // ดึง scene ตามสี
			if (scene != null) SpawnSpecific(scene); // สร้างจริง
		}

		if (LogDebug)
			GD.Print($"[CrystalSpawner] Spawn batch={toSpawn} onScreen={CountCrystalsOnScreen()}");
	}

	// ===================== SPAWN LOGIC =====================

	// ฟังก์ชัน TrySpawnOnceNow() ใช้สำหรับสั่งให้สุ่มคริสตัลเกิดทันทีตอนเริ่มเกม (ไม่ต้องรอเวลา spawn รอบแรก)
	private void TrySpawnOnceNow()
	{
		if (_allowedWithScene.Count == 0) return;          // ถ้าไม่มีสีที่อนุญาตให้ spawn → ออกจากฟังก์ชันทันที
		var color = PickAllowedColorWithScene();           // สุ่มเลือกสีจากรายการสีที่อนุญาตและมี scene จริง
		var scene = SceneOf(color);                        // ดึง PackedScene ของสีที่สุ่มได้ (เช่น BlueScene, GreenScene ฯลฯ)
		if (scene != null)                                 // ถ้า scene ไม่เป็น null (มีจริง)
		{
			SpawnSpecific(scene, force: true);             // สร้างคริสตัลทันที โดยบังคับ spawn (ไม่สน MaxOnScreen)
			if (LogDebug) GD.Print("[CrystalSpawner] SpawnImmediatelyOnStart"); // แสดง log ถ้าเปิด debug
		}
	}

	// ฟังก์ชัน IntervalWithJitter() ใช้คืนค่าเวลาระหว่างการ spawn รอบถัดไป โดยสามารถสุ่มให้แปรผันเล็กน้อย
	private float IntervalWithJitter()
	{
		if (!UseRandomInterval || RandomJitter <= 0f) return IntervalSec; // ถ้าไม่เปิดโหมดสุ่ม หรือค่า jitter ≤ 0 → ใช้เวลาเดิมตรง ๆ
		float j = Mathf.Clamp(RandomJitter, 0f, 0.9f);                    // จำกัดค่าสุ่มไม่ให้เกิน 90% ของค่าเดิม
		float low = IntervalSec * (1f - j);                               // คำนวณขอบล่างของช่วงเวลา (ลดลงตาม jitter)
		float high = IntervalSec * (1f + j);                              // คำนวณขอบบนของช่วงเวลา (เพิ่มขึ้นตาม jitter)
		return (float)GD.RandRange(low, high);                            // สุ่มค่าระหว่าง low ถึง high แล้วคืนค่าออกไป
	}

	// อัปเดตรายชื่อสีที่มี scene จริง (กรองเฉพาะสีที่มี PackedScene ให้ spawn ได้)
	private void RefreshAllowedWithScene()
	{
		_allowedWithScene.Clear();                        // เคลียร์รายการสีที่อนุญาตพร้อม scene เดิมออกก่อน
		foreach (var c in _allowed)                       // วนตรวจทุกสีที่อนุญาตไว้ใน _allowed
		if (SceneOf(c) != null)                       // ถ้าสีนั้นมี scene จริง (ไม่เป็น null)
		_allowedWithScene.Add(c);                 // เพิ่มสีนี้ลงในลิสต์ที่สามารถ spawn ได้จริง
	}

	// สุ่มเลือกสีจากชุดที่มี scene พร้อมให้ spawn ได้
	private CrystalType PickAllowedColorWithScene()
	{
		int n = _allowedWithScene.Count;                  // นับจำนวนสีที่ spawn ได้จริง
		int idx = (int)(GD.Randi() % (uint)Math.Max(1, n)); // สุ่ม index จาก 0 ถึง n-1 (กันกรณี n=0 โดยบังคับให้มีค่าอย่างน้อย 1)
		return (n == 0) ? CrystalType.Purple              // ถ้าไม่มีสีใดเลย → คืนค่าเริ่มต้นเป็น Purple (กัน error)
		: _allowedWithScene[idx];         // ถ้ามีสี → คืนค่าสีที่สุ่มได้จากรายการ
	}


	// คืนค่า Scene ของแต่ละสี (ใช้เลือก PackedScene ที่จะ spawn ตามชนิด CrystalType)
	private PackedScene SceneOf(CrystalType c) => c switch  // ใช้ switch expression เพื่อเลือก scene ตามประเภท
	{
		CrystalType.Purple => PurpleScene,  // ถ้าเป็นสีม่วง → คืนค่า scene ที่กำหนดใน Inspector สำหรับ PurpleScene
		CrystalType.Blue   => BlueScene,    // ถ้าเป็นสีน้ำเงิน → คืนค่า BlueScene
		CrystalType.Green  => GreenScene,   // ถ้าเป็นสีเขียว → คืนค่า GreenScene
		CrystalType.Red    => RedScene,     // ถ้าเป็นสีแดง → คืนค่า RedScene
		CrystalType.Pink   => PinkScene,    // ถ้าเป็นสีชมพู → คืนค่า PinkScene
		_ => null                          // ถ้าไม่ตรงกับสีใดเลย → คืนค่า null (หมายถึงไม่มี scene ให้ใช้)
	};


	// ฟังก์ชันสร้างคริสตัลตาม scene ที่กำหนด (ใช้ตอนต้องการ spawn คริสตัลใหม่)
	private void SpawnSpecific(PackedScene ps, bool force = false)
	{
		if (ps == null || _spawnRoot == null) return; // ถ้า scene หรือจุด spawn ยังไม่มี → ยกเลิกทันที
		if (!GodotObject.IsInstanceValid(_spawnRoot) || _spawnRoot.IsQueuedForDeletion() || !_spawnRoot.IsInsideTree())
			return; // ถ้าจุด spawn ถูกลบหรือไม่อยู่ใน scene แล้ว → ข้าม
		if (!force && MaxOnScreen > 0 && CountCrystalsOnScreen() >= MaxOnScreen)
			return; // ถ้าคริสตัลบนจอเต็ม และไม่ได้บังคับ spawn → ไม่สร้างเพิ่ม

		var n = ps.Instantiate<Node2D>(); // สร้าง instance ของ Node2D จาก scene ที่ส่งเข้ามา
		if (n == null) return;            // ถ้าสร้างไม่สำเร็จ → ยกเลิก

		Rect2 vp = GetViewportRect(); // ดึงขอบเขตของหน้าจอปัจจุบัน (viewport)
		float x = (float)GD.RandRange(vp.Position.X + 32f, vp.End.X - 32f); // สุ่มตำแหน่ง X ภายในขอบจอ (เผื่อขอบซ้ายขวาไว้ 32px)
		float y = vp.Position.Y + 2f;  // ตั้งตำแหน่ง Y เริ่มต้นให้ใกล้ขอบบนของหน้าจอ
		n.GlobalPosition = new Vector2(x, y); // ตั้งค่าตำแหน่งเริ่มต้นของคริสตัลในโลกจริง

		n.AddToGroup("Crystal"); // เพิ่ม Node นี้เข้าสู่กลุ่ม "Crystal" เพื่อให้ระบบอื่น (เช่น Player) รู้จัก
		if (ps == PinkScene) n.SetMeta("crystal_color", "pink"); // ถ้า scene ที่ใช้คือสีชมพู → ใส่ meta tag ชื่อ "crystal_color" = "pink"

		_spawnRoot.AddChild(n); // เพิ่มคริสตัลนี้เข้าเป็นลูกของ Node ราก (_spawnRoot) เพื่อให้แสดงในฉาก

		StartFall(n); // เรียกให้เริ่มตกลงมาด้วยคลาส FallDriver (ทำให้คริสตัลตกแบบมีแรงโน้มถ่วง)

		if (LogDebug) GD.Print($"[CrystalSpawner] +1 {ps.ResourcePath}"); // ถ้าเปิดโหมด debug → พิมพ์ log แจ้งว่า spawn แล้ว
	}


	// เพิ่มตัวควบคุมการตกให้คริสตัล (สร้างและแนบสคริปต์ให้คริสตัลตกลงมาด้วยความเร็ว)
private void StartFall(Node2D n)
{
	if (n == null) return; // ถ้า node เป็น null → ไม่ทำอะไร
	FallDriver driver = new FallDriver(n, FallSpeed, MinVisibleSec); // สร้างตัวควบคุมการตก (ส่ง Node, ความเร็ว, เวลาขั้นต่ำ)
	n.AddChild(driver); // แนบ driver เข้ากับคริสตัลนี้
}

// ===================== COUNT / CHECK =====================

// นับจำนวนคริสตัลที่อยู่ในจอ
private int CountCrystalsOnScreen()
{
	if (_spawnRoot == null ||                       // ถ้าไม่มี root
		!GodotObject.IsInstanceValid(_spawnRoot) || // หรือ root ไม่ valid
		_spawnRoot.IsQueuedForDeletion() ||         // หรือกำลังถูกลบ
		!_spawnRoot.IsInsideTree())                 // หรือไม่อยู่ใน scene tree
		return 0;                                   // → ไม่มีอะไรให้นับ

	try
	{
		int count = 0; // ตัวนับจำนวนคริสตัล

		foreach (Node child in _spawnRoot.GetChildren()) // วนเช็กลูกทั้งหมดในจุด spawn
		{
			if (child == null) continue;                 // ถ้า null → ข้าม
			if (!GodotObject.IsInstanceValid(child)) continue; // ถ้าโดนลบไปแล้ว → ข้าม
			if (child.IsQueuedForDeletion() || !child.IsInsideTree()) continue; // ถ้ากำลังจะโดนลบ → ข้าม
			if (!child.IsInGroup("Crystal")) continue;   // นับเฉพาะ Node ที่อยู่ในกลุ่ม "Crystal"
			count++;                                    // เจอ 1 ชิ้น → บวก
		}
		return count; // คืนค่าจำนวนทั้งหมดที่เจอ
	}
	catch (ObjectDisposedException)
	{
		return 0; // ถ้ามี error จาก object ที่โดนลบไปแล้ว → คืนค่า 0
	}
}

// ตรวจว่ามีคริสตัลชมพูอยู่ในจอไหม
private bool PinkExistsOnScreen()
{
	if (_spawnRoot == null || 
		!GodotObject.IsInstanceValid(_spawnRoot) || 
		_spawnRoot.IsQueuedForDeletion() || 
		!_spawnRoot.IsInsideTree())
		return false; // ถ้า root ใช้งานไม่ได้ → ไม่มีแน่นอน

	try
	{
		foreach (Node child in _spawnRoot.GetChildren()) // วนดูทุก Node ลูกของจุด spawn
		{
			if (child is not Node2D n2d) continue;       // ถ้าไม่ใช่ Node2D → ข้าม
			if (!GodotObject.IsInstanceValid(n2d)) continue; // ถ้าโดนลบไปแล้ว → ข้าม
			if (n2d.IsQueuedForDeletion() || !n2d.IsInsideTree()) continue; // ถ้ายังไม่อยู่ใน tree → ข้าม

			Variant meta = n2d.GetMeta("crystal_color"); // อ่าน meta ของ node (เช็กว่ามี tag สีหรือไม่)
			string s = null;                             // เตรียมตัวแปรเก็บชื่อสี
			if (meta.VariantType == Variant.Type.String || meta.VariantType == Variant.Type.StringName)
				s = meta.AsString();                     // ถ้า meta เป็น string → แปลงเป็นข้อความ

			if (s == "pink" && IsInsideViewport(n2d.GlobalPosition)) // ถ้าเป็นสีชมพูและอยู่ในจอ
				return true;                            // → แสดงว่ามีคริสตัลชมพูอยู่
		}
	}
	catch (ObjectDisposedException)
	{
		return false; // ถ้าเกิด error จาก object ที่ถูกลบ → คืน false
	}
	return false; // ถ้าไม่มีตรงตามเงื่อนไขเลย → ไม่มีคริสตัลชมพูบนจอ
}

	// ตรวจว่าตำแหน่งอยู่ใน viewport หรือไม่ (ใช้เช็กว่าคริสตัลอยู่ในขอบจอ)
	private bool IsInsideViewport(Vector2 g)
	{
		Rect2 vp = GetViewportRect(); // ดึงขอบเขตของหน้าจอ (viewport)
		return g.X >= vp.Position.X && g.X <= vp.End.X &&  // X อยู่ระหว่างขอบซ้าย-ขวา
			   g.Y >= vp.Position.Y && g.Y <= vp.End.Y;    // Y อยู่ระหว่างขอบบน-ล่าง → แปลว่าอยู่ในจอ
	}

	// คืนค่ารายชื่อสีที่มี Scene จริง (ใช้สร้าง fallback ถ้า _allowed ไม่มีค่า)
	private List<CrystalType> AvailableColors()
	{
		var list = new List<CrystalType>(5); // สร้างลิสต์ใหม่ความจุ 5 สี
		if (PurpleScene != null) list.Add(CrystalType.Purple); // ถ้ามี scene สีม่วง → เพิ่ม
		if (BlueScene   != null) list.Add(CrystalType.Blue);   // ถ้ามี scene สีน้ำเงิน → เพิ่ม
		if (GreenScene  != null) list.Add(CrystalType.Green);  // ถ้ามี scene สีเขียว → เพิ่ม
		if (RedScene    != null) list.Add(CrystalType.Red);    // ถ้ามี scene สีแดง → เพิ่ม
		if (PinkScene   != null) list.Add(CrystalType.Pink);   // ถ้ามี scene สีชมพู → เพิ่ม
		return list; // คืนลิสต์ของสีทั้งหมดที่มี scene จริงใน Inspector
	}

		// ถ้าไม่มีสีที่อนุญาต → fallback ให้อนุญาตทุกสีที่มีอยู่จริง
	private void EnsureAllowedFallback()
	{
		if (_allowedWithScene.Count > 0) return; // ถ้ามีสีที่อนุญาตอยู่แล้ว → ไม่ต้องทำอะไร
		var avail = AvailableColors();           // ดึงรายชื่อสีทั้งหมดที่มี scene จริง
		if (avail.Count > 0)                     // ถ้ามีสีที่พร้อมใช้งาน
		{
			_allowed.Clear();                    // ล้างรายการสีที่อนุญาตเก่าออก
			foreach (var c in avail) _allowed.Add(c); // เพิ่มทุกสีใน avail เข้าไปใน _allowed
			_allowedWithScene.Clear();           // ล้างรายการที่มี scene จริงเก่าออก
			_allowedWithScene.AddRange(avail);   // ใส่สีทั้งหมดที่มี scene จริงเข้าไปใหม่
			GD.PushWarning("[CrystalSpawner] Allowed empty → fallback to all available PackedScenes."); // แจ้งเตือนใน Console ว่าทำ fallback
		}
	}

	// ===================== FALL DRIVER =====================

	// คลาสย่อย FallDriver: ควบคุมการตกของคริสตัลแต่ละชิ้น
	private sealed partial class FallDriver : Node
	{
		private readonly Node2D _host;      // Node2D เจ้าของ (คริสตัลที่จะตก)
		private readonly float _fallSpeed;  // ความเร็วในการตก
		private readonly float _minVisible; // เวลาขั้นต่ำที่ต้องอยู่บนจอก่อนจะถูกลบ
		private float _t;                   // ใช้เก็บเวลาเพื่อคำนวณการแกว่งแบบ sine
		private float _life;                // เวลาอายุรวมตั้งแต่เริ่มตก

		// Constructor: กำหนดค่าตอนสร้าง FallDriver
		public FallDriver(Node2D host, float fallSpeed, float minVisible)
		{
			_host = host;                                  // เก็บ node เจ้าของไว้ในตัวแปร _host
			_fallSpeed = Math.Max(20f, fallSpeed);         // ป้องกันไม่ให้ความเร็วตกต่ำกว่า 20f
			_minVisible = Math.Max(0.2f, minVisible);      // ป้องกันไม่ให้เวลาแสดงน้อยเกินไป (< 0.2 วิ)
			ProcessMode = ProcessModeEnum.Always;          // ให้ทำงานเสมอ แม้เกมจะ pause
		}

		// อัปเดตการตกทุกเฟรม
		public override void _Process(double delta)
		{
			if (!IsInstanceValid(_host)) { QueueFree(); return; } // ถ้า host ถูกลบไปแล้ว → ลบตัวเองด้วย

			_life += (float)delta;  // เพิ่มเวลาที่อยู่ในจอ
			_t += (float)delta;     // เพิ่มเวลาสำหรับใช้คำนวณการแกว่ง
			float drift = Mathf.Sin(_t * 2.2f) * 18f; // คำนวณการแกว่งซ้ายขวา (แบบ sine wave)
			_host.GlobalPosition += new Vector2(drift, _fallSpeed) * (float)delta; // ขยับตำแหน่งให้ตกลงพร้อมแกว่ง

			var vp = _host.GetViewportRect();               // ดึงขอบเขตของหน้าจอ
			bool passedBottom = _host.GlobalPosition.Y > vp.End.Y + 80f; // ถ้าตกเลยขอบล่างของหน้าจอไป 80px

			// ถ้าอยู่บนจอนานเกินเวลาขั้นต่ำและตกพ้นจอ หรืออยู่เกิน 12 วินาที → ลบคริสตัลทิ้ง
			if ((_life >= _minVisible && passedBottom) || _life > 12f)
			{
				Node root = _host; // เริ่มจาก host
				while (root != null && !root.IsInGroup("Crystal")) // ไต่ขึ้นไปหาตัว root ที่อยู่ในกลุ่ม "Crystal"
					root = root.GetParent();

				(root ?? (Node)_host).QueueFree(); // ถ้าเจอ root ของคริสตัล → ลบ root; ถ้าไม่เจอ → ลบ host เอง
				QueueFree(); // ลบตัว driver ตัวเองออกจากฉาก
			}
		}
	}

	// ===================== EXIT TREE =====================

	// ฟังก์ชัน _ExitTree() เรียกเมื่อ Node ถูกถอดออกจาก Scene Tree
	public override void _ExitTree()
	{
		SetProcess(false); // ปิดการประมวลผลทั้งหมด (ไม่ให้ _Process ทำงานต่อ)
		_spawnRoot = null; // ล้าง reference ของจุด spawn เพื่อป้องกัน null reference ตอน scene ปิด
	}
}
