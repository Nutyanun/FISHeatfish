using Godot;
using System;
using System.Linq;

/// <summary>
/// สปอว์นคริสตัลแบบสุ่ม: ทุก ๆ IntervalSec จะสุ่มเกิด 1 สี (จำกัดจำนวนบนจอ)
/// วางโหนดนี้ไว้ในฉากหลัก แล้วลากซีนคริสตัล 5 สีลง Inspector ให้ครบ
/// แนะนำให้คริสตัลอยู่กลุ่ม "pickups" (หรือเปลี่ยนเป็น "coins" ได้ใน UsePickupsGroup)
/// </summary>
public partial class CrystalSpawner : Node2D
{
	// --- ตั้งค่า ---
	[Export] public float IntervalSec = 45f;   // เวลาห่างระหว่างการเกิด (วินาที)
	[Export] public int   MaxOnScreen = 2;     // จำนวนคริสตัลบนจอได้สูงสุด
	[Export] public bool  UsePickupsGroup = true; // true => นับจาก "pickups", false => "coins"

	// พื้นที่สุ่ม X บนขอบจอด้านบน
	[Export] public float MarginX = 32f;
	[Export] public float SpawnYOffset = -32f; // เริ่มเหนือขอบบนเล็กน้อย

	// ความเร็วตกแบบ "ค่อยๆ เร็วขึ้น"
	[Export] public float FallStartSpeed = 12f;   // เริ่มช้ามาก
	[Export] public float FallTargetSpeed = 50f;  // เร็วสุดที่อยากได้
	[Export] public float FallEaseTime   = 2.5f;  // ใช้เวลากี่วิจากช้า -> เร็ว

	// ถ้าอยากสุ่มช่วงเวลา ให้ตั้งค่า 2 ค่านี้ แล้วติ๊ก UseRandomInterval = true
	[Export] public bool  UseRandomInterval = false;
	[Export] public Vector2 RandomIntervalRange = new Vector2(50f, 70f);

	// --- ซีนของคริสตัลแต่ละสี (ลากมาวางใน Inspector) ---
	[Export] public PackedScene RedScene;
	[Export] public PackedScene BlueScene;
	[Export] public PackedScene GreenScene;
	[Export] public PackedScene PinkScene;    // โปรเจกต์นี้ใช้ Pink แทน Yellow
	[Export] public PackedScene PurpleScene;

	// ✅ เงื่อนไขพิเศษ: 20 วินาทีสุดท้าย สปอว์นชมพู 1 ชิ้น
	[Export] public bool  SpawnOnePinkInLastSeconds = true;
	[Export] public float LastSeconds = 20f;
	private bool _finalPinkSpawned = false;
	private double _accum; // เวลาสะสมไว้เช็คถี่ๆ

	private Timer _timer;

	public override void _Ready()
	{
		// ตั้งค่า RNG ให้ไม่ซ้ำ
		GD.Seed(Time.GetTicksUsec());

		// --- ตั้ง Timer ---
		_timer = new Timer
		{
			WaitTime = IntervalSec,
			Autostart = true,
			OneShot = false
		};
		_timer.ProcessCallback = Timer.TimerProcessCallback.Idle;  // ทำงานในเฟรม idle
		_timer.ProcessMode = Node.ProcessModeEnum.Inherit;         // หยุดตอน pause (ถ้าอยากให้ทำงานตอน pause ใช้ Always)

		AddChild(_timer);
		_timer.Timeout += OnTimeout;

		// ถ้าอยากให้เกิดทันทีตอนเริ่มด่าน ให้ปลดคอมเมนต์บรรทัดนี้:
		// SpawnOne();
	}

	public override void _Process(double delta)
	{
		// เช็คเงื่อนไข 20 วินาทีสุดท้าย ทุก ๆ ~0.25 วิ เพื่อไม่โพลหนัก
		if (!SpawnOnePinkInLastSeconds || _finalPinkSpawned || PinkScene == null) return;

		_accum += delta;
		if (_accum < 0.25) return;
		_accum = 0;

		if (TryGetTimeLeftSeconds(out var left) && left <= LastSeconds)
		{
			// ถ้าบนจอยังไม่มี "ชมพู" อยู่ค่อยสปอว์น (กันซ้ำ)
			if (!PinkExistsOnScreen())
			{
				SpawnSpecific(PinkScene);
				_finalPinkSpawned = true;
			}
		}
	}

	private void OnTimeout()
	{
		// จำกัดจำนวนคริสตัลบนจอ
		string groupName = UsePickupsGroup ? "pickups" : "coins";
		int count = GetTree().GetNodesInGroup(groupName).Count;
		if (count >= MaxOnScreen)
		{
			if (UseRandomInterval) ResetTimerRandomInterval();
			return;
		}

		SpawnOne();

		if (UseRandomInterval) ResetTimerRandomInterval();
	}

	private void ResetTimerRandomInterval()
	{
		float min = Mathf.Min(RandomIntervalRange.X, RandomIntervalRange.Y);
		float max = Mathf.Max(RandomIntervalRange.X, RandomIntervalRange.Y);
		_timer.WaitTime = (float)GD.RandRange(min, max);
		_timer.Start();
	}

	private void SpawnOne()
	{
		// เลือกซีนแบบสุ่มจาก 5 สี
		var pool = new PackedScene[] { RedScene, BlueScene, GreenScene, PinkScene, PurpleScene }
				   .Where(s => s != null).ToArray();

		if (pool.Length == 0)
		{
			GD.PushWarning("[CrystalSpawner] No crystal scenes assigned in Inspector.");
			return;
		}

		var scene = pool[GD.Randi() % pool.Length];
		SpawnSpecific(scene);
	}

	// --------- Helpers ----------

	// สปอว์นตำแหน่งบนขอบบน แล้วใส่ FallController ให้ตก
	private void SpawnSpecific(PackedScene scene)
	{
		if (scene == null) return;

		var node = scene.Instantiate<Node2D>();
		if (node == null)
		{
			GD.PushWarning("[CrystalSpawner] Scene root is not Node2D.");
			return;
		}

		// สุ่มตำแหน่ง X บนขอบบน
		var vp = GetViewportRect();
		float xMin = vp.Position.X + MarginX;
		float xMax = vp.End.X     - MarginX;
		float x = (float)GD.RandRange(xMin, xMax);
		float y = vp.Position.Y + SpawnYOffset;
		node.GlobalPosition = new Vector2(x, y);

		// เพิ่มเข้าฉากหลัก
		GetTree().CurrentScene?.AddChild(node);

		// ใส่คอมโพเนนต์ "ตกแบบค่อยๆ เร็วขึ้น" และลบเมื่อหลุดจอ
		var fall = new FallController
		{
			StartSpeed      = FallStartSpeed,
			TargetSpeed     = FallTargetSpeed,
			EaseTime        = FallEaseTime,
			OffscreenMargin = 64f
		};
		fall.ProcessMode = Node.ProcessModeEnum.Inherit;
		node.AddChild(fall);
	}

	// มี Pink อยู่บนจอแล้วหรือยัง (เช็คทั้งจากชนิด CrystalPickup.Type และจากชื่อโหนด)
	private bool PinkExistsOnScreen()
	{
		string groupName = UsePickupsGroup ? "pickups" : "coins";
		foreach (var obj in GetTree().GetNodesInGroup(groupName))
		{
			if (obj is Node n)
			{
				// ถ้ามีสคริปต์ CrystalPickup ให้ดู Type
				var cp = n as CrystalPickup ?? n.GetNodeOrNull<CrystalPickup>("Hit") ?? n.FindChild("Hit", true, false) as CrystalPickup;
				if (cp != null && cp.Type == CrystalType.Pink) return true;

				// เผื่อไม่มีสคริปต์ ให้เดาจากชื่อ
				var name = n.Name.ToString().ToLower();
				if (name.Contains("pink") || name.Contains("yellow")) return true;
			}
		}
		return false;
	}

	// อ่านเวลาที่เหลือจาก ScoreManager แบบยืดหยุ่น (รองรับหลายชื่อพร็อพ/เมธอด)
	private bool TryGetTimeLeftSeconds(out double secs)
	{
		secs = 0;
		var sm = FindScoreManager();
		if (sm == null) return false;

		// ลอง property หลายชื่อ
		foreach (var key in new[] { "TimeLeftSec", "TimeLeft", "TimeRemaining", "RemainingTime", "time_left" })
		{
			var v = sm.Get(key);
			if (v.VariantType != Variant.Type.Nil)
			{
				try { secs = Convert.ToDouble(v); return true; } catch { }
			}
		}
		// ลองเมธอด
		if (sm.HasMethod("GetTimeLeft"))       { secs = Convert.ToDouble(sm.Call("GetTimeLeft")); return true; }
		if (sm.HasMethod("GetRemainingTime"))  { secs = Convert.ToDouble(sm.Call("GetRemainingTime")); return true; }

		return false;
	}

	private Node FindScoreManager()
	{
		return GetTree().CurrentScene?.FindChild("ScoreManager", true, false)
			?? GetTree().Root?.FindChild("ScoreManager", true, false);
	}
}

/// <summary>
/// คอมโพเนนต์ช่วยให้โหนดแม่ "ตกลงมา" ทุกเฟรมแบบค่อยๆ เร็วขึ้น:
/// - ถ้าแม่เป็น CharacterBody2D: ใช้ Physics + MoveAndSlide()
/// - ถ้าแม่เป็น Node2D (ทั่วไป): ขยับตำแหน่งใน _Process()
/// และจะลบตัวเองเมื่อหลุดขอบล่างของจอ
/// </summary>
public partial class FallController : Node
{
	[Export] public float StartSpeed = 12f;    // เริ่มช้า
	[Export] public float TargetSpeed = 50f;   // เร็วสุด
	[Export] public float EaseTime = 2.5f;     // วินาทีจากช้า -> เร็ว
	[Export] public float OffscreenMargin = 64f;

	private float _t; // เวลาที่ผ่านไปสะสม (ใช้คำนวณอีซิง)

	public override void _Ready()
	{
		SetProcess(true);
		SetPhysicsProcess(true);
	}

	private float CurrentSpeed(float delta)
	{
		_t += (float)delta;
		float k = Mathf.Clamp(_t / Mathf.Max(EaseTime, 0.0001f), 0f, 1f); // 0→1
		return Mathf.Lerp(StartSpeed, TargetSpeed, k);
	}

	public override void _Process(double delta)
	{
		var parent = GetParent();
		if (parent is Node2D n2d && parent is not CharacterBody2D)
		{
			float spd = CurrentSpeed((float)delta);
			n2d.GlobalPosition += new Vector2(0, spd * (float)delta);
			DespawnIfOffscreen(n2d);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GetParent() is CharacterBody2D body)
		{
			float spd = CurrentSpeed((float)delta);
			body.Velocity = new Vector2(0, spd);
			body.MoveAndSlide();
			DespawnIfOffscreen(body);
		}
	}

	private void DespawnIfOffscreen(Node2D node)
	{
		var vp = node.GetViewportRect();
		if (node.GlobalPosition.Y > vp.End.Y + OffscreenMargin)
			node.QueueFree();
	}
}
