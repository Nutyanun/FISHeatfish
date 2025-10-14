using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using Game; // ⬅️ ใช้ enum Global: Game.CrystalType

public partial class CrystalSpawner : Node2D
{
	[Export] public float   IntervalSec = 45f;
	[Export] public int     MaxOnScreen = 2;
	[Export] public bool    UsePickupsGroup = true;

	[Export] public bool    UseRandomInterval = false;
	[Export] public Vector2 RandomIntervalRange = new Vector2(50f, 70f);

	// ซีนคริสตัลแต่ละสี
	[Export] public PackedScene RedScene;
	[Export] public PackedScene BlueScene;
	[Export] public PackedScene GreenScene;
	[Export] public PackedScene PinkScene;
	[Export] public PackedScene PurpleScene;

	// เงื่อนไข: 20 วิท้าย (ด่าน >= 4) บังคับมีชมพู 1 ชิ้น
	[Export] public bool  SpawnOnePinkInLastSeconds = true;
	[Export] public float LastSeconds = 20f;

	private bool   _finalPinkSpawned = false;
	private double _accum;
	private Timer  _timer;

	// สีที่อนุญาตในด่านนี้ (null = ใช้ทุกสีที่มีซีน)
	private HashSet<CrystalType> _allowed = null;

	public override void _Ready()
	{
		GD.Seed(Time.GetTicksUsec());
		_timer = new Timer { WaitTime = Math.Max(0.05f, IntervalSec), OneShot = false, Autostart = true };
		AddChild(_timer);
		_timer.Timeout += OnTimeout;
	}

	public override void _Process(double delta)
	{
		if (!SpawnOnePinkInLastSeconds || _finalPinkSpawned || PinkScene == null) return;

		_accum += delta;
		if (_accum < 0.25) return;
		_accum = 0;

		if (TryGetTimeLeftSeconds(out var left) && left <= LastSeconds)
		{
			// ถ้าด่านนี้ไม่ได้เปิด Pink ปกติ ให้ยกเว้นเฉพาะด่าน >= 4
			if (_allowed != null && !_allowed.Contains(CrystalType.Pink))
			{
				if (!TryGetLevel(out int lv) || lv < 4) return;
			}

			if (!PinkExistsOnScreen())
			{
				SpawnSpecific(PinkScene);
				_finalPinkSpawned = true;
			}
		}
	}

	private void OnTimeout()
	{
		string groupName = UsePickupsGroup ? "pickups" : "coins";
		int alive = GetTree().GetNodesInGroup(groupName).Count;

		// เต็มเพดานแล้ว ข้ามรอบนี้
		if (alive >= MaxOnScreen)
		{
			if (UseRandomInterval) ResetTimerRandomInterval();
			return;
		}

		int allowedCount = GetAllowedColorCount();
		if (allowedCount <= 0)
		{
			if (UseRandomInterval) ResetTimerRandomInterval();
			return;
		}

		// สุ่มจำนวนในรอบนี้ = 1..N แต่ไม่เกินช่องว่างบนจอ
		int want    = 1 + (int)(GD.Randi() % (uint)allowedCount);
		int room    = Math.Max(0, MaxOnScreen - alive);
		int toSpawn = Math.Min(want, room);

		SpawnBatch(toSpawn);

		if (UseRandomInterval) ResetTimerRandomInterval();
	}

	private void ResetTimerRandomInterval()
	{
		float min = Mathf.Min(RandomIntervalRange.X, RandomIntervalRange.Y);
		float max = Mathf.Max(RandomIntervalRange.X, RandomIntervalRange.Y);
		_timer.WaitTime = (float)GD.RandRange(min, max);
		_timer.Start();
	}

	// ===== รับกติกาจาก ScoreManager =====
	public void ApplyRule(CrystalType[] colors, float intervalSec, int maxOnScreen)
	{
		_allowed = (colors == null || colors.Length == 0)
			? new HashSet<CrystalType>()          // ไม่อนุญาตสีใดเลย = ไม่สปอว์น
			: new HashSet<CrystalType>(colors);   // ใช้ชุดสีที่อนุญาต

		IntervalSec = Math.Max(0.05f, intervalSec);
		MaxOnScreen = Math.Max(0, maxOnScreen);
		_finalPinkSpawned = false;

		if (_timer != null) { _timer.WaitTime = IntervalSec; _timer.Start(); }
	}

	// สปอว์นเป็น “ชุด” ตามจำนวนที่กำหนด
	private void SpawnBatch(int count)
	{
		if (count <= 0) return;

		var pairs = new List<(CrystalType type, PackedScene scene)>
		{
			(CrystalType.Red,    RedScene),
			(CrystalType.Blue,   BlueScene),
			(CrystalType.Green,  GreenScene),
			(CrystalType.Pink,   PinkScene),
			(CrystalType.Purple, PurpleScene)
		};

		bool allowAll = (_allowed == null);

		var pool = pairs
			.Where(p => p.scene != null && (allowAll || (_allowed.Count > 0 && _allowed.Contains(p.type))))
			.Select(p => p.scene)
			.ToArray();

		if ((!allowAll && _allowed.Count == 0) || pool.Length == 0) return;

		for (int i = 0; i < count; i++)
		{
			var scene = pool[GD.Randi() % (uint)pool.Length];
			SpawnSpecific(scene);
		}
	}

	private int GetAllowedColorCount()
	{
		if (_allowed != null) return _allowed.Count;

		// ถ้าไม่ได้รับ rule → fallback = จำนวน scene ที่ตั้งค่าไว้จริง
		int c = 0;
		if (RedScene    != null) c++;
		if (BlueScene   != null) c++;
		if (GreenScene  != null) c++;
		if (PinkScene   != null) c++;
		if (PurpleScene != null) c++;
		return c;
	}

	private void SpawnSpecific(PackedScene scene)
	{
		if (scene == null) return;

		var node = scene.Instantiate<Node2D>();
		AddChild(node);

		// วางแบบสุ่มแกน X เหนือขอบบนเล็กน้อย
		var vp = GetViewportRect();
		float x = (float)GD.RandRange(vp.Position.X + 32, vp.End.X - 32);
		float y = vp.Position.Y - 48;
		node.GlobalPosition = new Vector2(x, y);

		AddFallController(node);
	}

	private void AddFallController(Node2D node)
	{
		var fall = new FallController
		{
			StartSpeed      = 90f,
			TargetSpeed     = 160f,
			EaseTime        = 8f,
			OffscreenMargin = 64f
		};
		node.AddChild(fall);
	}

	private bool TryGetLevel(out int level)
	{
		level = 0;
		var sm = FindScoreManager() as ScoreManager;
		if (sm == null) return false;
		level = sm.Level; return true;
	}

	private bool PinkExistsOnScreen()
	{
		string groupName = UsePickupsGroup ? "pickups" : "coins";
		foreach (var obj in GetTree().GetNodesInGroup(groupName))
		{
			if (obj is Node n)
			{
				var cp = n as CrystalPickup
					?? n.GetNodeOrNull<CrystalPickup>("Hit")
					?? n.FindChild("Hit", true, false) as CrystalPickup;

				if (cp != null && cp.Type == CrystalType.Pink) return true;

				// กรณีไม่มีสคริปต์ระบุชนิด ดูจากชื่อโหนดแทน
				var nm = n.Name.ToString().ToLower();
				if (nm.Contains("pink") || nm.Contains("yellow")) return true;
			}
		}
		return false;
	}

	private bool TryGetTimeLeftSeconds(out double secs)
	{
		secs = 0;
		var sm = FindScoreManager() as ScoreManager;
		if (sm == null) return false;
		secs = sm.GetTimeLeft(); return true;
	}

	private Node FindScoreManager()
	{
		return GetTree().CurrentScene?.FindChild("ScoreManager", true, false)
			?? GetTree().Root?.FindChild("ScoreManager", true, false);
	}
}

// ตัวควบคุมการตกอย่างง่าย
public partial class FallController : Node
{
	[Export] public float StartSpeed = 90f;
	[Export] public float TargetSpeed = 160f;
	[Export] public float EaseTime = 8f;
	[Export] public float OffscreenMargin = 64f;

	private float _t;

	public override void _Process(double delta)
	{
		if (GetParent() is not Node2D n2d) return;

		_t += (float)delta;
		float k = Mathf.Clamp(_t / Mathf.Max(EaseTime, 0.0001f), 0f, 1f);
		float speed = Mathf.Lerp(StartSpeed, TargetSpeed, k);

		n2d.GlobalPosition += new Vector2(0, speed * (float)delta);

		var vp = n2d.GetViewportRect();
		if (n2d.GlobalPosition.Y > vp.End.Y + OffscreenMargin)
			n2d.QueueFree();
	}
}
